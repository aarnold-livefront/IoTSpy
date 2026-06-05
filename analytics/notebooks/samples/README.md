# IoTSpy Analytics — Sample Notebooks

Reference notebooks for exploring IoT device traffic captured via IoTSpy.
All data used here is **synthetic** — safe to commit to a public repository.

## Quick start

```bash
cd analytics
pip install -r requirements.txt         # install deps
pip install umap-learn                  # optional: 2-D cluster visualisation

# Run in order:
jupyter notebook notebooks/samples/
```

Open notebooks in this order:

| # | Notebook | What it does |
|---|---|---|
| 00 | `00_generate_sample_data.ipynb` | Generates a synthetic Parquet dataset (2,200 rows, 4 device types) |
| 01 | `01_eda_traffic_overview.ipynb` | Exploratory analysis: hosts, burst patterns, TLS posture, risk tags |
| 02 | `02_ml_risk_scorer.ipynb` | LightGBM risk classifier + composite score; saves model to `models/` |
| 03 | `03_behavioral_clustering.ipynb` | HDBSCAN clustering + UMAP; identifies unknown traffic behaviour patterns |

## Using your own captured data

Run the feature extractor against your IoTSpy database, then point any notebook at the output:

```bash
python analytics/scripts/extract_features.py \
  --db sqlite:///src/IoTSpy.Api/iotspy.db \
  --out analytics/data/my_captures.parquet
```

Then in any notebook change:

```python
DATA_PATH = Path("../data/my_captures.parquet")
```

The notebooks handle both the synthetic column names (`host`, `method`, …) and the
DB-derived names (`Host`, `Method`, …) via the `col()` helper, so no other changes
are needed.

## Synthetic device profiles

The generator creates traffic from four archetypal IoT device types:

| Device | Traffic mix | Risk profile |
|---|---|---|
| **smart-tv** | Vendor API + ad SDK + CDN | Moderate — ad-SDK telemetry |
| **voice-assistant** | Frequent cloud sync + ad targeting | High — always-on data collection |
| **security-camera** | Periodic health checks + video upload | Low — mostly first-party |
| **game-console** | Heavy ad-SDK + CDN + game API | High — multiple ad networks |

## Model output

Notebook 02 saves two artifacts to `analytics/models/`:

- `risk_scorer_broker.lgb` — native LightGBM booster (loadable via LightGBM C API or `Microsoft.ML.LightGbm`)
- `risk_scorer_broker_features.json` — ordered list of input feature names

The `.NET` `MlInsightService` expects these at the path configured in `appsettings.json`
under `Ml:ModelPath` and `Ml:FeaturesPath`.
