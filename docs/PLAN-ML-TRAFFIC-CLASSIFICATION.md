# Plan: ML-Based Traffic Classification for IoTSpy

## Context and Goal

IoTSpy captures HTTP/HTTPS/MQTT/DNS/CoAP traffic from IoT devices. Today analysts review captures manually. The goal of this feature is to close the loop between captured data and real-time interception: a machine-learning pipeline analyzes historical captures, identifies behavioral patterns and risk signals, and generates or updates IoTSpy manipulation rules that are enforced in real-time against new traffic.

The end-to-end flow:
```
Historical Captures → Feature Extraction → ML Model Training
       ↓
Trained Model + Rule Generator → ManipulationRules/ContentReplacementRules
       ↓
Real-time proxy pipeline applies rules → flags, transforms, blocks, or alerts
```

Jupyter notebooks (in `analytics/`) are an exploration and visualization aid — not the deliverable. The deliverable is the ML model driving real-time rule generation and traffic enforcement.

---

## Architecture Decision Summary

| Decision | Choice | Rationale |
|---|---|---|
| ML code location | Python `analytics/` for training; ONNX + new `IoTSpy.Analytics` C# project for in-process inference | ONNX bridges Python training to zero-latency .NET inference with no subprocess boundary |
| Prediction storage | New `TrafficInsight` entity (separate from `CaptureAnnotation`) | `CaptureAnnotation` is session-scoped and human-authored; ML output needs confidence scores, model version, review workflow |
| Rule integration | ML outputs generate/update `ContentReplacementRule` + `ManipulationRule` entries | Reuses the existing rule pipeline, proxy middleware, and UI rather than a parallel enforcement path |
| Realtime scoring | `InsightBatchJob` (background) + on-demand `POST /api/analytics/score/{captureId}` | Batch keeps the proxy hot-path free; on-demand supports interactive investigation |
| UI surface | Inline risk badges on `CaptureRow` + new `analytics` view mode for triage queue | Badges require no navigation change; triage queue is the analyst workflow |

---

## Risk Tag Taxonomy

Implemented as `IoTSpy.Core/Enums/RiskTag.cs`:

```csharp
public enum RiskTag
{
    ExfiltrationRisk,        // large or burst outbound transfers to unknown hosts
    PiiDetected,             // PII in request/response body (names, emails, tokens, geo)
    DataBroker,              // known ad-tech / analytics / tracking domains
    SuspiciousTls,           // weak cipher, deprecated TLS version, anomalous cert
    UnusualPort,             // service on non-standard port for its protocol
    MqttCredentialExposure,  // cleartext MQTT credentials in CONNECT packet
    DnsTunneling,            // high-entropy or overlong DNS query names
    HighEntropyPayload       // encrypted/compressed data on a cleartext channel
}
```

---

## Directory and Project Structure

```
iotspy/
├── analytics/                              ← Python data science workspace
│   ├── requirements.txt
│   ├── data/
│   │   └── data_broker_domains.txt         ← curated ad-tech/tracker blocklist
│   ├── scripts/
│   │   ├── extract_features.py             ← SQLite/Postgres → Parquet
│   │   └── label_bootstrap.py             ← rule-tagged captures → initial label CSV
│   ├── models/                             ← ONNX exports (.gitignore large binaries)
│   └── notebooks/
│       ├── 01_eda.ipynb                    ← distribution analysis, outlier detection
│       ├── 02_feature_engineering.ipynb    ← flow-level feature matrix construction
│       ├── 03_clustering.ipynb             ← behavioral segmentation (HDBSCAN)
│       ├── 04_classification.ipynb         ← multi-label risk classification (LightGBM)
│       ├── 05_nlp_url_headers.ipynb        ← URL/header/payload NLP analysis
│       └── 06_scoring_pipeline.ipynb       ← end-to-end validation + rule generation
│
└── src/
    ├── IoTSpy.Analytics/                   ← NEW C# inference + rule generation project
    ├── IoTSpy.Analytics.Tests/
    ├── IoTSpy.Core/                        ← new enum + model + interface
    ├── IoTSpy.Storage/                     ← new repository + migration
    └── IoTSpy.Api/                         ← new controller + DI registration
```

---

## 1. Data Models

### `IoTSpy.Core/Enums/RiskTag.cs` — NEW

### `IoTSpy.Core/Models/TrafficInsight.cs` — NEW

Key fields:
- `Id: Guid`
- `CaptureId: Guid` — unique index (one insight per capture)
- `TagsJson: string` — JSON-serialized `RiskTag[]`
- `ConfidenceJson: string` — JSON-serialized `Dictionary<RiskTag, double>`
- `RiskScore: double` — 0.0–1.0 composite, used for triage sort order
- `ModelVersion: string`
- `Source: string` — `"rule"` | `"ml"` | `"hybrid"`
- `IsReviewed: bool`, `IsDismissed: bool`, `ReviewNote: string?`
- `ReviewedByUserId: Guid?`, `ReviewedAt: DateTimeOffset?`
- `CreatedAt: DateTimeOffset`

Uses JSON columns for arrays/dicts — same pattern as `RequestHeaders`/`TlsMetadataJson` on `CapturedRequest`. DB indices on `RiskScore`, `IsReviewed`, `CreatedAt`.

### `IoTSpy.Core/Interfaces/ITrafficInsightRepository.cs` — NEW

```csharp
Task<TrafficInsight?> GetByCaptureIdAsync(Guid captureId, CancellationToken ct);
Task<List<TrafficInsight>> GetTriageQueueAsync(int page, int pageSize, bool unreviewedOnly, CancellationToken ct);
Task<int> CountTriageQueueAsync(bool unreviewedOnly, CancellationToken ct);
Task UpsertAsync(TrafficInsight insight, CancellationToken ct);
Task MarkReviewedAsync(Guid id, Guid userId, bool dismissed, string? note, CancellationToken ct);
Task<List<TrafficInsight>> GetByCaptureIdsAsync(IEnumerable<Guid> captureIds, CancellationToken ct);
```

`GetTriageQueueAsync` orders by `RiskScore DESC` then `CreatedAt DESC`.

**Migration:** `dotnet ef migrations add AddTrafficInsights --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api`

---

## 2. Feature Extraction

### Python: `analytics/scripts/extract_features.py`
SQLAlchemy dual-provider (SQLite + Postgres). Joins `CapturedRequests + Devices + MqttCapturedMessages`. Timestamps: `pd.to_datetime(df['timestamp'], unit='ms', utc=True)` (IoTSpy stores as Unix-ms). Supports `--since` for incremental loading. Outputs Parquet.

### C#: `IoTSpy.Analytics/Features/RequestFeatureExtractor.cs` + `FeatureVector`
Mirrors Python feature schema as `float32` record for ONNX inference.

**Numeric (log-transformed where skewed):**
- `ResponseBodySizeLog`, `RequestBodySizeLog`, `DurationMsLog`
- `Port`, `StatusCode`
- `TlsCipherStrength` — ordinal: 0=known-weak, 1=unknown, 2=modern
- `HourOfDay`, `DayOfWeek`
- `DnsNameLength`, `DnsNameEntropy` — Shannon entropy

**Boolean (0.0/1.0):**
- `IsTls`, `IsStandardPort`, `IsModified`, `HostIsIp`
- `HasUserAgent`, `HasAuthorization`
- `ContentTypeIsJson`, `ContentTypeIsBinary`

**Text-derived (30 floats):**
- Character n-gram TF-IDF on `host + path` → TruncatedSVD(30 components)
- Captures domain segments like `telemetry`, `beacon`, `analytics`

---

## 3. ML Algorithms (Python Training)

| Task | Algorithm | Rationale |
|---|---|---|
| Multi-label risk classification | **LightGBM** per-tag via `OneVsRest` | Faster and more accurate than vanilla RF on tabular IoT data; native categorical handling; excellent feature importance for interpretability |
| Anomaly detection (unsupervised) | **Isolation Forest** | O(n log n); ideal for high-dimensional sparse traffic; handles non-spherical anomalies better than distance-based methods |
| Behavioral clustering | **HDBSCAN** | No k required; handles variable-density clusters and noise explicitly — essential for heterogeneous IoT device behavior |
| PII detection in payloads | **Microsoft Presidio** + custom recognizers | Production-grade NER-based PII detection (emails, SSNs, phone numbers, tokens, geolocation); far more accurate and maintainable than regex-only |
| URL/header NLP | **Character TF-IDF → LogisticRegression** (baseline) + **distilbert-base-uncased** fine-tuned (if GPU available) | TF-IDF is fast and interpretable; transformer adds +5–10% on ambiguous URLs |
| Probability calibration | **Isotonic regression** via `CalibratedClassifierCV` | Ensures `confidence=0.90` means ~90% precision in practice |
| Class imbalance | **SMOTE + `class_weight='balanced'`** | IoT traffic is typically >95% normal; must prevent the classifier from predicting "normal" for everything |

**ONNX export:** `skl2onnx` for LightGBM, Isolation Forest, LogisticRegression. Presidio PII patterns baked into `RuleBasedTagger` on the C# side.

**Evaluation metrics:** F1, AUC-ROC, precision-recall AUC, confusion matrix per tag — never raw accuracy on imbalanced classes.

---

## 4. Rule Generation — Closing the Loop

After analyst review confirms insights, ML outputs are promoted to IoTSpy-native rules:

### `IoTSpy.Analytics/RuleGenerator/MlRuleGenerator.cs` — NEW

Produces:

1. **`ContentReplacementRule`** entries — for PII/data-broker body patterns:
   - Pattern: regex from Presidio detections (email, SSN, etc.)
   - Action: `Redact` or `Block`
   - Host filter: specific host where PII was detected

2. **`ManipulationRule`** entries — for behavioral patterns:
   - `ExfiltrationRisk` → `RateLimit` or `Block` rule on flagged host + high-volume path
   - `SuspiciousTls` → `LogWarning` or `Block` on weak cipher/version
   - `DataBroker` → `Drop` rule on flagged domain

All ML-generated rules are created with `IsEnabled: false` — analysts activate them after review, preventing automation from silently breaking device functionality.

---

## 5. IoTSpy.Analytics C# Project Structure

```
IoTSpy.Analytics/
├── AnalyticsExtensions.cs          ← DI registration, reads Analytics config section
├── Features/
│   ├── RequestFeatureExtractor.cs
│   └── FeatureVector.cs
├── Rules/
│   └── RuleBasedTagger.cs          ← deterministic rules (no model required)
├── Onnx/
│   ├── OnnxClassifier.cs           ← ML.NET OnnxScoringEstimator wrapper
│   └── ModelManifest.cs            ← version + feature schema + threshold config
├── Services/
│   ├── IInsightService.cs
│   └── InsightService.cs           ← orchestrates RuleBasedTagger + OnnxClassifier
├── Jobs/
│   └── InsightBatchJob.cs          ← BackgroundService (PeriodicTimer, 15 min default)
└── RuleGenerator/
    └── MlRuleGenerator.cs          ← produces ContentReplacementRules + ManipulationRules
```

### `RuleBasedTagger` — deterministic rules

| Tag | Rule |
|---|---|
| `UnusualPort` | port not in `{80, 443, 8080, 8443, 1883, 8883, 5683, 5684, 53, 5353}` |
| `SuspiciousTls` | `IsTls && TlsCipherSuite` contains RC4/3DES/NULL/EXPORT OR `TlsVersion` ≤ 1.1 |
| `MqttCredentialExposure` | MQTT CONNECT with non-empty credentials in `MqttCapturedMessage.PayloadText` |
| `DnsTunneling` | Shannon entropy(`DnsQueryName`) > 3.5 OR `DnsQueryName.Length` > 60 |
| `DataBroker` | host suffix-matches entries in `data_broker_domains.txt` |
| `PiiDetected` | regex set: email pattern, `\b\d{3}-\d{2}-\d{4}\b` (SSN), `"password":`, `"token":`, geolocation JSON keys |
| `ExfiltrationRisk` | `ResponseBodySize` > 500 KB to unknown external host OR >10 requests/60s to same host |
| `HighEntropyPayload` | Shannon entropy(body) > 7.5 bits/byte on cleartext protocol |

### `InsightService` orchestration

1. Run `RuleBasedTagger` → tags + confidences
2. Run `OnnxClassifier` if loaded → tags + confidences
3. Merge: union of tags, highest confidence per tag
4. `RiskScore` = weighted average (`ExfiltrationRisk` and `PiiDetected` at 1.5×)
5. `UpsertAsync` to `TrafficInsights`

### `InsightBatchJob`
`BackgroundService` + `PeriodicTimer`. Queries captures with no `TrafficInsight` (LEFT JOIN IS NULL), processes up to `Analytics:BatchSize` per tick. Registration pattern: `AddHostedService(sp => sp.GetRequiredService<InsightBatchJob>())`.

**`appsettings.json` additions:**
```json
"Analytics": {
  "Enabled": true,
  "BatchIntervalMinutes": 15,
  "BatchSize": 1000,
  "ModelPath": "models/traffic_classifier_v1.onnx"
}
```

---

## 6. API Endpoints (`AnalyticsController`) — NEW

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/analytics/triage` | All | Paginated triage queue, `RiskScore DESC` |
| GET | `/api/analytics/insights/{captureId}` | All | Single insight |
| GET | `/api/analytics/insights/bulk` | All | Bulk fetch by capture IDs (inline badges) |
| POST | `/api/analytics/insights/{id}/review` | All | Confirm/dismiss with note |
| POST | `/api/analytics/score/{captureId}` | Operator+ | On-demand score a capture |
| POST | `/api/analytics/batch-score` | Admin | Trigger batch job |
| GET | `/api/analytics/stats` | All | Tag counts, coverage %, unreviewed count |
| POST | `/api/analytics/generate-rules` | Admin | Generate rules from reviewed insights |
| GET | `/api/analytics/pending-rules` | All | List ML-generated rules awaiting activation |
| POST | `/api/analytics/pending-rules/{id}/activate` | Admin | Activate a generated rule |

---

## 7. Frontend

### New files
- `frontend/src/types/analytics.ts` — `RiskTag` union type, `TrafficInsight`, `InsightTriageItem`, `AnalyticsStats`
- `frontend/src/api/analytics.ts` — API client (mirrors `captures.ts` pattern)
- `frontend/src/hooks/useInsights.ts` — debounced bulk insight fetch
- `frontend/src/components/analytics/RiskTagBadge.tsx` — colored chip per tag
- `frontend/src/components/analytics/InsightTriagePanel.tsx` — triage queue: risk score bars, tag chips, Confirm/Dismiss, split-pane capture detail
- `frontend/src/components/analytics/PendingRulesPanel.tsx` — review and activate ML-generated rules

### Modified files
- `frontend/src/pages/DashboardPage.tsx` — add `'analytics'` to `ViewMode`; render `<InsightTriagePanel />`
- `frontend/src/components/captures/CaptureRow.tsx` — render up to 3 `<RiskTagBadge>` per row via `useInsights` bulk fetch; new `capture-row__risk-tags` CSS class

### Tag color mapping
- `ExfiltrationRisk` / `MqttCredentialExposure` → red
- `PiiDetected` / `DnsTunneling` → orange
- `DataBroker` → purple
- `SuspiciousTls` / `HighEntropyPayload` → amber
- `UnusualPort` → gray

---

## 8. Bootstrapping Ground Truth

**Phase A — Rule tagger first:**
Deploy `RuleBasedTagger` without an ML model. High-precision rules produce reliable positive examples immediately. Untagged captures become the "normal" training class. Export via `analytics/scripts/label_bootstrap.py`.

**Phase B — Analyst review (triage queue):**
Analysts confirm or dismiss rule-tagged captures. Target: 200–500 confirmed positives per tag before first training run. `CaptureAnnotation` (human) and `TrafficInsight` (ML) remain distinct tables throughout.

**Phase C — Active learning iteration:**
After ONNX model deploys, `InsightBatchJob` scores all unscored captures. High-confidence ML predictions surface in triage for rapid confirm/dismiss. Each review cycle grows the training corpus.

**Note:** `DnsTunneling` and `HighEntropyPayload` may remain purely rule-based long-term — their entropy threshold signals are already high-precision.

---

## 9. Implementation Sequence

Sub-agents can parallelize within each phase; phases must be sequential.

### Phase 1 — Data model + storage (prerequisite for everything)
1. `IoTSpy.Core/Enums/RiskTag.cs`
2. `IoTSpy.Core/Models/TrafficInsight.cs`
3. `IoTSpy.Core/Interfaces/ITrafficInsightRepository.cs`
4. `IoTSpy.Storage/Repositories/TrafficInsightRepository.cs`
5. `IoTSpyDbContext.cs` — add `DbSet<TrafficInsight>` + index config
6. EF Core migration `AddTrafficInsights`
7. `TrafficInsightRepositoryTests` (in-memory SQLite)

### Phase 2 — Analytics C# project (can parallelize internals after Phase 1)
8. `IoTSpy.Analytics.csproj` + add to solution
9. `FeatureVector.cs` + `RequestFeatureExtractor.cs` + extractor tests
10. `RuleBasedTagger.cs` + tagger tests (one positive + one negative per tag)
11. `InsightService.cs` + service tests
12. `InsightBatchJob.cs`
13. `AnalyticsExtensions.cs` — DI registration
14. Wire into `Program.cs` + `StorageExtensions.cs`

### Phase 3 — API controller
15. `AnalyticsController.cs` + controller tests
16. Ensure `JsonStringEnumConverter` serializes `RiskTag` correctly (already configured in `Program.cs`)

### Phase 4 — Frontend (can parallelize with Phase 2 once types are defined)
17. `analytics.ts` types + API client
18. `useInsights.ts` hook
19. `RiskTagBadge.tsx` + badge tests
20. `CaptureRow.tsx` augmentation
21. `InsightTriagePanel.tsx` + triage panel tests
22. `DashboardPage.tsx` — `'analytics'` view mode

### Phase 5 — Python analytics workspace
23. `analytics/requirements.txt` + `extract_features.py`
24. `analytics/data/data_broker_domains.txt`
25. Notebooks 01–06

### Phase 6 — ONNX integration (after ML model is trained)
26. `OnnxClassifier.cs` + `ModelManifest.cs`
27. Wire into `InsightService`
28. Integration test: score → insight → ONNX tags appear

### Phase 7 — Rule generation
29. `MlRuleGenerator.cs` + generator tests
30. `generate-rules` + `pending-rules` endpoints
31. `PendingRulesPanel.tsx`

---

## 10. Verification

### Backend (target: ~40 new tests, zero regressions)
- `RequestFeatureExtractorTests` — each feature computed correctly for synthetic captures
- `RuleBasedTaggerTests` — positive + negative per tag (16 test cases minimum)
- `InsightServiceTests` — merge logic, risk score weighting, `UpsertAsync` called
- `TrafficInsightRepositoryTests` — upsert idempotency, ordering, bulk fetch
- `AnalyticsControllerTests` — pagination, 404 on missing, review endpoint, rule generation

### Frontend (Vitest)
- `RiskTagBadge.test.tsx`
- `InsightTriagePanel.test.tsx` — loading/empty/item render, confirm/dismiss
- `PendingRulesPanel.test.tsx` — activate rule calls correct API

### Integration
Seed a capture → `POST /api/analytics/score/{id}` → `GET /api/analytics/insights/{id}` → assert insight exists with `RiskScore > 0` when a rule matches.

### Python notebooks
Each ends with assertions: no NaN/Inf in feature matrix; ≥100 positive examples per tag before training; LightGBM AUC-ROC > 0.75 before ONNX export; F1 per tag printed.

### End-to-end
`dotnet test` — all 825+ existing tests green. Verify "ML Insights" tab renders, risk badges appear on flagged captures in capture list, ML-generated rules appear in Pending Rules panel.