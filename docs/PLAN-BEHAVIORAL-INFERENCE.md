# Plan: Behavioral Inference / Privacy-Leakage Module

Applying the 2020 IEEE paper *"IoTSpy: Uncovering Human Privacy Leakage in IoT Networks via Mining Wireless Context"* to the IoTSpy codebase.

## Context

Our repository shares its name with a 2020 IEEE PIMRC paper (Gu, Fang, Abhishek, Mohapatra; DOI 10.1109/PIMRC48278.2020.9217236). The paper demonstrates that a **passive wireless sniffer placed outside a home** can infer human activities, living habits, and installed apps **purely from traffic metadata** — packet sizes, timing, direction, and rates — *even though the payload is WPA2-encrypted*. No decryption is required.

Our platform today does all of its ML/analytics on **decrypted** `CapturedRequest` records produced by the TLS-intercepting proxy. That is the *opposite* premise from the paper. Meanwhile our packet-capture pipeline already records the exact metadata primitives the paper mines (size, timestamp, MAC/IP, direction) but never aggregates, models, or surfaces them.

**Intended outcome:** a new module that consumes packet *metadata* (works below TLS, so encrypted flows are fine), aggregates it per device/flow, segments it into events, and infers device-activity / occupancy / routine patterns. Framed two ways, cleanly separated:
- **Defensive** — a "privacy-leakage risk" finding: *what an external passive sniffer could infer* about this network's occupants. Surfaced in the existing analytics/triage UI.
- **Offensive** — a "demonstrate leakage" view that reconstructs a target's inferred activity timeline during an *authorized* engagement.

Both are driven by one inference engine; only the presentation/report differs.

**Approach decisions:**
- Build the **foundation first** (aggregation → segmentation → rule/statistical inference surfaced in triage), but document the **full end-to-end pipeline** for future planning.
- Start **rule/statistical** (no ML runtime), document the **migration to ML** (notebook-trained model + net-new ONNX C# inference) as the long-term goal, with rules as the stopgap behind a stable interface.

---

## The paper: findings, conclusions, methodology

**Threat model.** A passive attacker with a Wi-Fi sniffer in promiscuous mode outside the target home. No association, no payload access. Only Layer-2/metadata is visible: **MAC addresses, packet sizes, inter-arrival timing, direction, byte/packet rates**.

**Key finding.** Encrypted wireless IoT traffic still leaks privacy. Per-device metadata forms a behavioral fingerprint; device state changes (motion sensor triggered, camera streaming, lock actuated, hub sync) produce distinctive packet-sequence signatures.

**Methodology / pipeline:**
1. **Capture** packet metadata below the encryption layer.
2. **Feature engineering** per device/flow: packet-size distributions, inter-packet timing, up/down byte volumes, byte-per-sec and packet-per-sec rates.
3. **Event segmentation** — split each device's traffic into discrete events using burst / quiet-period thresholds.
4. **Activity classification** — supervised ML (Random Forest / neural nets) maps event feature-vectors to device activities/states (**>90% accuracy** reported).
5. **Temporal dependency mining** — correlate event sequences over time to recover living habits, occupancy schedules, and installed applications.

**Conclusion.** Encryption alone is insufficient for IoT privacy; metadata-level defenses (padding, traffic shaping, cover traffic) are needed. This maps naturally onto a *defensive detector* that quantifies the leak.

Sources:
- [IEEE Xplore](https://ieeexplore.ieee.org/document/9217236/) · [Semantic Scholar](https://www.semanticscholar.org/paper/6a85e4c84c465c1dbd085c02b0042088c041ac34)
- Related methodology corpus: [Peek-a-Boo (arXiv 1808.02741)](https://arxiv.org/pdf/1808.02741), [Noisy Networks, Nosy Neighbors (arXiv 2510.13822)](https://arxiv.org/pdf/2510.13822)

---

## Applicability to the current codebase

| Paper stage | Existing building block (reuse) | Gap to close |
|---|---|---|
| Capture metadata | `src/IoTSpy.Scanner/PacketCaptureService.cs` `ParseRawPacket` already extracts per-packet `Length`, `Timestamp`, MAC/IP/port, direction, protocol, TCP flags into `CapturedPacket` (`src/IoTSpy.Core/Models/CaptureDevice.cs`). Works below TLS. | `TimeDeltaFromPrevious` field exists but is **unpopulated**. |
| Per-device/flow features | `src/IoTSpy.Protocols/Mqtt/MqttSessionAnalyzer.cs` (thread-safe per-key stat accumulation) is a template. | **No flow aggregation exists** on the packet pipeline. Packets aren't correlated to a logical `Device` — `CapturedPacket.DeviceId` points to the capture NIC (`CaptureDevice`), not `Device`. |
| Event segmentation / baselines | `src/IoTSpy.Protocols/Anomaly/AnomalyDetector.cs` + `src/IoTSpy.Core/Models/HostBaseline.cs` — Welford online mean/variance, 3-sigma flagging, 30-sample warm-up, 60s rate window. **Strongest reuse**; re-key from `host` to `DeviceId`. | Keyed on host + request features today; needs device + packet-metadata features. |
| Feature vector / extractor | `src/IoTSpy.Analytics/Features/RequestFeatureExtractor.cs` + `FeatureVector.cs` (log-sizes, HourOfDay, DayOfWeek, `ShannonEntropy`). | Bound to `CapturedRequest`; need a packet/flow analog. |
| Scoring / triage / review UI | `src/IoTSpy.Analytics/Services/InsightService.cs`, `Jobs/InsightBatchJob.cs`, `TrafficInsight` entity, `TrafficInsightRepository`, `AnalyticsController.cs` (`api/analytics`), `frontend/src/components/analytics/InsightDetailModal.tsx`. Model-agnostic — reusable. | `TrafficInsight.CaptureId` is a **unique, cascade FK to `Captures`** (DbContext lines 400/407-410). Metadata insights key to a *packet-flow event*, not a request → FK must be loosened (nullable) + a `BehaviorEventId` added. |
| Activity classification (ML) | Notebooks `analytics/notebooks/03_behavioral_clustering.ipynb` (HDBSCAN+UMAP), `02_ml_risk_scorer.ipynb` (LightGBM→ONNX via skl2onnx), `analytics/scripts/extract_features.py`. | **ONNX C# inference is empty** (`src/IoTSpy.Analytics/Onnx/` has no code; `OnnxRuntime` not referenced in any csproj; `Source="ml"` unused). Net-new for phase 2. |
| Temporal dependency mining | None. | New miner + persistence. |

**Project-reference constraint (verified):** `IoTSpy.Analytics` → Core + Storage; `IoTSpy.Scanner` → Core only. Analytics does **not** reference Scanner. Therefore all new cross-cutting contracts (`IFlowAggregator`, `IEventSegmenter`, `IDeviceCorrelator`, `IBehaviorInferenceService`) live in **`IoTSpy.Core/Interfaces`**; concrete impls live in Scanner/Analytics; DI wiring happens in the **API composition root** (which references both). Keep `IoTSpy.Core` free of infrastructure deps.

**Conclusion: highly applicable.** ~60% of the plumbing exists. The work is (a) a new metadata-aggregation layer on the packet pipeline and (b) re-pointing the mature statistical/insight machinery at device+packet features instead of host+request features.

---

## Near-term deliverable (foundation first)

Ship a rule/statistical slice end-to-end: capture metadata → per-device/flow aggregation → event segmentation → privacy-leakage risk score + inferred-activity tags → visible in the existing triage UI. Phases 0–6 below. Phases 7–9 are the documented full pipeline for later.

### Phase 0 — Device correlation
Map `CapturedPacket` → logical `Device` by MAC (preferred) / IP (fallback), selecting the LAN-side endpoint.
- New: `src/IoTSpy.Core/Interfaces/IDeviceCorrelator.cs`; `src/IoTSpy.Scanner/DeviceCorrelationService.cs` — `Guid? Resolve(CapturedPacket)`, `ConcurrentDictionary<string,Guid>` MAC→DeviceId cache seeded from `IDeviceRepository`, RFC1918 local/remote split (reuse `CidrHelper` in Scanner), upserts new devices on a background scope (never on the hot path).
- Modify `src/IoTSpy.Core/Models/Device.cs`: add `bool MacIsRandomized` (locally-administered bit) + `string? StableDeviceKey`.
- Tests: `src/IoTSpy.Scanner.Tests/DeviceCorrelationServiceTests.cs` (MAC hit, IP fallback, randomized-MAC detection, local-vs-remote selection).

### Phase 1 — Per-device / per-flow metadata aggregation
Populate `TimeDeltaFromPrevious`; accumulate flow stats **off the hot path**.
- New under `src/IoTSpy.Scanner/Behavioral/`: `FlowKey.cs` (direction-normalized `record struct`), `FlowStats.cs` (Welford mean/M2 for size & inter-arrival, up/down byte+packet counts, fixed-bucket size histogram; pattern from `HostBaseline`/`MqttSessionAnalyzer`), `FlowAggregator.cs` (`IFlowAggregator`: `Observe(...)`, `SnapshotAndOptionallyReset()`).
- New `src/IoTSpy.Core/Interfaces/IFlowAggregator.cs`.
- Modify `src/IoTSpy.Scanner/PacketCaptureService.cs` **only inside `RunConsumerAsync`** (the single-threaded consumer loop that already iterates the batch and calls `_buffer.Add`): compute `TimeDeltaFromPrevious` from a per-device last-timestamp map, resolve the device, call `aggregator.Observe(...)`. Leave `OnPacketArrival` a bare `TryWrite`. Because the delta is set at buffer-insert time, it also persists via `PacketCaptureCheckpointService` with no extra work.
- DI: register `FlowAggregator` + `DeviceCorrelationService` as singletons in `src/IoTSpy.Scanner/ScannerExtensions.cs`.
- Tests: `src/IoTSpy.Scanner.Tests/Behavioral/FlowAggregatorTests.cs`.

### Phase 2 — Event segmentation
- New `src/IoTSpy.Scanner/Behavioral/EventSegmenter.cs` (`IEventSegmenter`) + `BehavioralOptions.cs` (thresholds, warm-up, `Enabled` flag — mirror `AnalyticsOptions`). Per device: rolling inter-arrival baseline (Welford, warm-up 30 like `AnomalyDetector`); event opens after a quiet gap > `max(QuietFloorSeconds, mean + k·std)` and closes after the same threshold; accumulates window into a `SegmentedEvent` record (`src/IoTSpy.Core/Models/SegmentedEvent.cs`, in-memory).
- Tests: `src/IoTSpy.Scanner.Tests/Behavioral/EventSegmenterTests.cs`.

### Phase 3 — Background orchestration
- New `src/IoTSpy.Analytics/Jobs/BehaviorInferenceJob.cs` — `BackgroundService` with `PeriodicTimer` + `TriggerNow()` semaphore (copy `InsightBatchJob` shape). Each tick: `SnapshotAndOptionallyReset()` → feed segmenter → per closed event: extract features → `IBehaviorInferenceService.Infer` → persist `BehaviorEvent`, update `DeviceBehaviorProfile`, upsert a `TrafficInsight` (`Source="metadata"`).
- Register via `AddHostedService(sp => sp.GetRequiredService<BehaviorInferenceJob>())` in `src/IoTSpy.Analytics/AnalyticsExtensions.cs`, gated on `BehavioralOptions.Enabled`.
- Tests: `src/IoTSpy.Analytics.Tests/Jobs/BehaviorInferenceJobTests.cs` (NSubstitute the interfaces).

### Phase 4 — Packet feature vector + insight surfacing
- New `src/IoTSpy.Analytics/Features/PacketFeatureVector.cs` + `PacketFeatureExtractor.cs` (`Extract(SegmentedEvent)`): up/down bytes-log, up/down ratio, packets/sec-log, bytes/sec-log, mean & std inter-arrival, mean & std packet size, histogram-bin fractions, HourOfDay, DayOfWeek, duration-log, distinct remote endpoints, size entropy (reuse `RequestFeatureExtractor.ShannonEntropy` idea).
- **Insight schema change (key decision):** route metadata findings through the existing triage plumbing by loosening `TrafficInsight`:
  - `src/IoTSpy.Core/Models/TrafficInsight.cs`: make `CaptureId` `Guid?`; add `Guid? BehaviorEventId`.
  - `src/IoTSpy.Storage/IoTSpyDbContext.cs` (lines 397-411): drop the unique `CaptureId` index → non-unique/filtered; FK `IsRequired(false)` + `OnDelete(SetNull)`; add optional FK + filtered-unique index on `BehaviorEventId`.
  - Add privacy tags to `src/IoTSpy.Core/Enums/RiskTag.cs`: `OccupancyInference`, `RoutineInference`, `DeviceActivityInference`, `PresenceLeak`; add their weights to `InsightService.TagWeights`.
  - Use `Source="metadata"`, `ModelVersion="metadata-rule-v1"`. Frontend branches on `source==="metadata"`.
- Tests: `PacketFeatureExtractorTests.cs`; update `TrafficInsightRepositoryTests.cs` for nullable `CaptureId`.

### Phase 5 — Rule/statistical inference (privacy-leakage scoring)
- New `src/IoTSpy.Analytics/Behavioral/BehaviorInferenceService.cs` (`IBehaviorInferenceService`) + `src/IoTSpy.Core/Models/DeviceMetadataBaseline.cs` (analog of `HostBaseline`, per-device feature-wise Welford). Rules (reusing `AnomalyDetector` machinery re-keyed to device):
  - Low-variance periodic beacons → `DeviceActivityInference`.
  - Sustained up-byte bursts at consistent times → `OccupancyInference` / `PresenceLeak`.
  - Recurring daily event clusters → feed `RoutineInference` (finalized Phase 7).
  - Composite `RiskScore` (weighted tags, clamp 0-1) — same shape as `InsightService.ComputeRiskScore`.
- Return type `InferenceResult { Tags, Confidences, RiskScore, Source }` — **stable contract so the ML path (Phase 9) slots in behind it unchanged.**
- Tests: `src/IoTSpy.Analytics.Tests/Behavioral/BehaviorInferenceServiceTests.cs` (model on `AnomalyDetectorTests.cs`).

### Phase 6 — Persistence + migration
- New entities in `IoTSpy.Core/Models/`: `BehaviorEvent` (Device FK SetNull, start/end, packet count, up/down bytes, rates, mean inter-arrival, dominant endpoint, `HistogramJson`, `FeatureVectorJson`, `InferredActivity`, `CreatedAt`); `DeviceBehaviorProfile` (unique Device FK, `RoutineJson` 7×24 occupancy, `InferredHabitsJson`, `TopActivitiesJson`, `PrivacyRiskScore`, counts, `ModelVersion`).
- New repos + Core interfaces following `TrafficInsightRepository` patterns; add `DbSet`s + configs (indexes on `DeviceId`, `StartedAt`, `CreatedAt`) to `IoTSpyDbContext` (DateTimeOffset converters are applied globally by the loop at lines 423-430).
- Migration: `dotnet ef migrations add AddBehavioralInference --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api` (covers Phases 4 + 6). **Inspect the generated `Up`** — the `CaptureId` nullability change triggers a SQLite table rebuild; confirm existing `TrafficInsights` rows survive and the dropped unique index doesn't orphan data.
- Tests: `src/IoTSpy.Storage.Tests/BehaviorEventRepositoryTests.cs`, `DeviceBehaviorProfileRepositoryTests.cs` (EF Core SQLite in-memory).

**First end-to-end shippable slice = Phases 0-6:** rule-based privacy-leakage inferences appear in the existing triage queue.

---

## Full end-to-end pipeline (future phases)

### Phase 7 — Temporal dependency mining
- New `src/IoTSpy.Analytics/Behavioral/TemporalProfileMiner.cs` (`ITemporalProfileMiner`): aggregate a device's `BehaviorEvent`s into a 7×24 occupancy heatmap, recurring event-transition chains (routines), and inter-device dependencies (e.g. motion → hub). Produces/updates `DeviceBehaviorProfile`; runs on a slower cadence from `BehaviorInferenceJob`. Emits a summary `TrafficInsight` (tag `RoutineInference`).
- Tests: `TemporalProfileMinerTests.cs` (synthetic multi-day events → assert heatmap + detected sequence).

### Phase 8 — API + frontend surface (defensive + offensive, separated)
- New `src/IoTSpy.Api/Controllers/BehaviorController.cs` (`api/behavior`): `GET profiles`, `profiles/{deviceId}`, `events?deviceId=&from=&to=`, `events/{id}`, `occupancy/{deviceId}`, `POST analyze` (admin-gated `TriggerNow()`). `[Authorize]`.
- Frontend (Vite/React 19/TS): `frontend/src/components/behavior/DeviceBehaviorPanel.tsx` (occupancy heatmap + inferred routines + privacy risk), `BehaviorEventList.tsx`; modify `InsightDetailModal.tsx` to branch on `source==="metadata"` and render event/profile context.
- **Defensive view:** "what an external passive sniffer could infer." **Offensive view:** "reconstructed activity timeline" for authorized engagements — same data, engagement-scoped presentation.
- Tests: component tests + Playwright E2E (metadata insight opens behavior view); `BehaviorControllerTests.cs` + integration test.

### Phase 9 — ML migration (rules → ONNX), long-term goal
- Extend `analytics/scripts/extract_features.py` to emit `BehaviorEvent` features → Parquet. New `analytics/notebooks/04_behavioral_metadata_inference.ipynb` (HDBSCAN+UMAP for unsupervised activity discovery, then supervised classifier → ONNX via skl2onnx into `analytics/models/behavior_classifier.onnx` + `_features.json`, mirroring notebook `02`).
- Add `Microsoft.ML.OnnxRuntime` to `src/IoTSpy.Analytics/IoTSpy.Analytics.csproj` (net-new). New `src/IoTSpy.Analytics/Onnx/OnnxBehaviorClassifier.cs` — loads model, `Predict(PacketFeatureVector)`, validates feature order against the `_features.json` sidecar.
- `BehaviorInferenceService` gains hybrid mode: model present → `Source="ml"`/`"hybrid"`, `ModelVersion` from file; absent → **falls back to Phase-5 rules** (rules are the stopgap). Same `IBehaviorInferenceService` contract → no downstream change.
- Tests: `OnnxBehaviorClassifierTests.cs` (tiny committed test model, deterministic output + feature-order guard; graceful fallback if model absent).

**Shipping order:** 0 → 1 → 2 → 6(entities+migration) → 3 → 4 → 5 → 7 → 8 → 9.

---

## Risks & mitigations
- **Hot-path performance:** all aggregation runs in the single-threaded consumer loop (O(1) dict + Interlocked/Welford per packet — same profile as `MqttSessionAnalyzer`); `OnPacketArrival` stays a bare `TryWrite`; segmentation/inference/persistence run in `BehaviorInferenceJob`. Add a high-rate synthetic test asserting no channel drops.
- **Packet persistence volume:** module reads the in-memory ring buffer and persists only aggregated events/profiles (orders of magnitude fewer rows than `Packets`). Add retention/pruning to `BehaviorEventRepository`.
- **MAC randomization:** detect locally-administered/randomized MACs (`MacIsRandomized`), fall back to IP-based (DHCP-stable) correlation, expose `StableDeviceKey` + a low-confidence flag on profiles; surface randomization status in the UI so inferences aren't over-trusted.
- **SQLite migration:** the `CaptureId`-nullability change rebuilds the table — inspect `Up`/`Down` before applying.
- **Ethics/framing:** every metadata insight is authored as a defensive privacy-risk finding; the offensive view is gated to authorized engagements. Keeps the tool's authorized-pentest posture intact.

---

## Verification
- Per phase: `dotnet test <touched test project>`; keep all backend tests green.
- Before any commit: full `dotnet test` + `dotnet ef database update --project src/IoTSpy.Storage --startup-project src/IoTSpy.Api` against a scratch DB.
- End-to-end (after Phase 6): run the API (`dotnet run --project src/IoTSpy.Api`), start a packet capture, confirm `TimeDeltaFromPrevious` is populated via `GET /api/packetcapture/.../packets`, then `POST /api/behavior/analyze` and confirm a `Source="metadata"` insight appears in `GET /api/analytics/triage`.
- After Phase 8: Playwright E2E opening a metadata insight → behavior view; frontend `npm test`.
- After Phase 9: `OnnxBehaviorClassifierTests` pass; confirm graceful rule-based fallback when no model file is present.
