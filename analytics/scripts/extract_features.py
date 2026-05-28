"""
IoTSpy feature extractor: reads CapturedRequests + Devices from the IoTSpy SQLite/Postgres
database and produces a Parquet file suitable for ML training and exploration.

Usage:
    python extract_features.py --db sqlite:///path/to/iotspy.db --out data/features.parquet
    python extract_features.py --db postgresql://user:pass@host/iotspy --out data/features.parquet
    python extract_features.py --db sqlite:///path/to/iotspy.db --since 2025-01-01 --out data/features.parquet
"""

import argparse
import math
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

import pandas as pd
from sqlalchemy import create_engine, text


def shannon_entropy(s: str) -> float:
    if not s:
        return 0.0
    counts = Counter(s)
    length = len(s)
    return -sum((c / length) * math.log2(c / length) for c in counts.values())


def extract(db_url: str, since: datetime | None = None) -> pd.DataFrame:
    engine = create_engine(db_url)

    query = """
        SELECT
            c.Id           AS capture_id,
            c.Host         AS host,
            c.Port         AS port,
            c.Method       AS method,
            c.Scheme       AS scheme,
            c.Path         AS path,
            c.StatusCode   AS status_code,
            c.RequestBodySize  AS request_body_size,
            c.ResponseBodySize AS response_body_size,
            c.DurationMs   AS duration_ms,
            c.IsTls        AS is_tls,
            c.TlsVersion   AS tls_version,
            c.TlsCipherSuite AS tls_cipher_suite,
            c.Protocol     AS protocol,
            c.Timestamp    AS timestamp_ms,
            c.IsModified   AS is_modified,
            c.ClientIp     AS client_ip,
            c.RequestHeaders  AS request_headers,
            c.ResponseHeaders AS response_headers,
            c.RequestBody  AS request_body,
            c.ResponseBody AS response_body,
            d.Vendor       AS device_vendor,
            d.SecurityScore AS device_security_score
        FROM Captures c
        LEFT JOIN Devices d ON c.DeviceId = d.Id
    """

    params = {}
    if since is not None:
        since_ms = int(since.timestamp() * 1000)
        query += " WHERE c.Timestamp > :since_ms"
        params["since_ms"] = since_ms

    with engine.connect() as conn:
        df = pd.read_sql(text(query), conn, params=params)

    # Timestamps are stored as Unix milliseconds
    df["timestamp"] = pd.to_datetime(df["timestamp_ms"], unit="ms", utc=True)
    df = df.drop(columns=["timestamp_ms"])

    # Temporal features
    local = df["timestamp"].dt.tz_convert("America/Chicago")
    df["hour_of_day"] = local.dt.hour
    df["day_of_week"] = local.dt.dayofweek

    # Log transforms (numpy log(1+x))
    df["response_body_size_log"] = (df["response_body_size"] + 1).apply(math.log)
    df["request_body_size_log"] = (df["request_body_size"] + 1).apply(math.log)
    df["duration_ms_log"] = (df["duration_ms"].clip(lower=0) + 1).apply(math.log)

    # Standard port flag
    standard_ports = {80, 443, 8080, 8443, 1883, 8883, 5683, 5684, 53, 5353}
    df["is_standard_port"] = df["port"].isin(standard_ports).astype(int)

    # IP address as host
    import ipaddress
    def is_ip(h: str) -> int:
        try:
            ipaddress.ip_address(str(h))
            return 1
        except ValueError:
            return 0
    df["host_is_ip"] = df["host"].fillna("").apply(is_ip)

    # DNS name entropy (also useful for any host)
    df["dns_name_entropy"] = df["host"].fillna("").apply(shannon_entropy)
    df["dns_name_length"] = df["host"].fillna("").apply(len)

    # TLS cipher strength: 0=weak, 1=unknown, 2=modern
    WEAK_CIPHERS = {"rc4", "3des", "des", "null", "export", "anon", "anonymous"}
    MODERN_CIPHERS = {
        "tls_aes_128_gcm_sha256", "tls_aes_256_gcm_sha384", "tls_chacha20_poly1305_sha256",
        "ecdhe_rsa_aes_128_gcm_sha256", "ecdhe_rsa_aes_256_gcm_sha384",
    }
    def cipher_strength(c: str) -> int:
        if not c:
            return 1
        lower = c.lower()
        if any(w in lower for w in WEAK_CIPHERS):
            return 0
        if any(m in lower for m in MODERN_CIPHERS):
            return 2
        return 1
    df["tls_cipher_strength"] = df["tls_cipher_suite"].fillna("").apply(cipher_strength)

    # Header-derived boolean features
    import json
    def parse_headers(h: str) -> dict:
        if not h:
            return {}
        try:
            return {k.lower(): v for k, v in json.loads(h).items()}
        except Exception:
            return {}

    req_headers = df["request_headers"].fillna("").apply(parse_headers)
    resp_headers = df["response_headers"].fillna("").apply(parse_headers)

    df["has_user_agent"] = req_headers.apply(lambda h: int("user-agent" in h))
    df["has_authorization"] = req_headers.apply(lambda h: int("authorization" in h))

    def content_type(h: dict) -> str:
        return h.get("content-type", "")
    df["content_type"] = resp_headers.apply(content_type)
    df["content_type_is_json"] = df["content_type"].str.contains("json", case=False, na=False).astype(int)
    df["content_type_is_binary"] = df["content_type"].str.contains(
        "octet-stream|protobuf|binary", case=False, na=False).astype(int)

    return df


def main() -> None:
    parser = argparse.ArgumentParser(description="Extract IoTSpy features to Parquet")
    parser.add_argument("--db", required=True, help="SQLAlchemy DB URL")
    parser.add_argument("--out", required=True, help="Output Parquet path")
    parser.add_argument("--since", default=None,
                        help="ISO date to load incrementally, e.g. 2025-01-01")
    args = parser.parse_args()

    since = None
    if args.since:
        since = datetime.fromisoformat(args.since).replace(tzinfo=timezone.utc)

    print(f"Extracting from {args.db}" + (f" since {since}" if since else ""))
    df = extract(args.db, since)
    print(f"Extracted {len(df):,} rows, {len(df.columns)} columns")

    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    df.to_parquet(args.out, index=False)
    print(f"Saved to {args.out}")


if __name__ == "__main__":
    main()
