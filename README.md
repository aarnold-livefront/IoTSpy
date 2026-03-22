# IoTSpy

IoT network security platform: transparent MITM proxy, protocol analyzer, pen-test suite, and traffic manipulation engine for IoT device research.

> **Use case:** Point an IoT device at the proxy, capture and inspect its HTTP/HTTPS traffic in real time, decode IoT protocols, run automated security scans, manipulate traffic with rules/scripts, and analyze raw packet captures — all from a single dashboard.

---

## Tech stack

| Layer | Technology |
|---|---|
| Backend | .NET 10 / C# — ASP.NET Core 10, controllers + SignalR |
| Real-time | SignalR (live traffic + packet streaming to dashboard) |
| Packet capture | SharpPcap / PacketDotNet — requires [Npcap](https://npcap.com) on Windows |
| TLS MITM | BouncyCastle (dynamic per-host certificate generation) |
| Resilience | Polly 8 (retry, circuit-breaker, timeout) |
| Storage | SQLite (default) / PostgreSQL (pluggable via appsettings) — EF Core 10 |
| Frontend | Vite 6 + React 19 + TypeScript |
| AI | Pluggable: Claude API / OpenAI / local Ollama |

---

## Features

### Proxy & capture
- **Explicit proxy** (`:8888`) — configure IoT device to use as HTTP proxy
- **Gateway/transparent proxy** (`:9999`) — iptables REDIRECT for network-level interception
- **ARP spoof mode** — automatically redirect device traffic via ARP poisoning (SharpPcap)
- **TLS MITM** — dynamic per-host certificate generation with BouncyCastle CA
- **Real-time dashboard** — SignalR streaming of all captured HTTP/HTTPS traffic

### Protocol analysis
- **MQTT 3.1.1 / 5.0** — full packet decoding (CONNECT, PUBLISH, SUBSCRIBE, etc.)
- **DNS / mDNS** — query/response decoding with label decompression
- **CoAP** — Constrained Application Protocol message decoding
- **OpenRTB 2.5** — bid request/response parsing with PII detection and policy-based redaction
- **Telemetry decoders** — Datadog, AWS Firehose, Splunk HEC, Azure Monitor

### Security scanning
- **TCP port scan** — configurable port ranges and concurrency
- **Service fingerprinting** — banner grab with CPE extraction
- **Default credential testing** — FTP, Telnet, MQTT default login checks
- **CVE lookup** — OSV.dev API integration for known vulnerabilities
- **Config audit** — Telnet access, UPnP exposure, anonymous MQTT, exposed databases, HTTP admin panels
- **Security scoring** — per-device composite score based on scan findings

### Traffic manipulation
- **Declarative rules engine** — match by host/path/method (regex), modify headers/body, override status, delay, or drop
- **Scripted breakpoints** — C# (Roslyn) and JavaScript (Jint) inline scripts for programmatic request/response modification
- **Request replay** — replay captured requests with modifications, record and compare responses
- **Mutation fuzzer** — Random, Boundary, and BitFlip strategies with anomaly detection
- **AI mock engine** — schema learning + LLM-generated responses (Claude, OpenAI, Ollama)

### Packet capture & analysis
- **Live packet capture** — SharpPcap with ring buffer (10k packets), protocol-aware parsing
- **Protocol distribution** — statistical breakdown of captured traffic by protocol
- **Communication patterns** — top N source→destination pairs
- **Suspicious activity detection** — port scan, ARP spoofing, DNS anomaly, retransmission burst detection
- **Freeze frame** — hex dump + layer-by-layer packet analysis
- **PCAP export** — standard format for Wireshark analysis

### Anomaly detection
- **Statistical baseline** — Welford online algorithm for per-host metrics (response time, size, status codes, request rate)
- **Alert types** — ResponseTime, ResponseSize, StatusCode, RequestRate anomalies
- **Real-time alerts** — anomaly alerts streamed via SignalR to the dashboard as they occur

### Observability & production hardening
- **Health checks** — `/health` (liveness + DB check) and `/ready` probes for container orchestration
- **Structured logging** — Serilog with console and rolling-file sinks (configurable)
- **Rate limiting** — ASP.NET Core sliding-window `RateLimiter`, partitioned per user/IP (default 100 req/60 s)
- **Data retention** — background `DataRetentionService` with configurable TTLs per data type (opt-in)
- **Graceful shutdown** — proxy servers drain active connections before stopping
- **Connection pooling** — configurable min/max pool sizes for SQLite and PostgreSQL

---

## Quick start

### Prerequisites

- .NET 10 SDK
- Node.js 22+ (for frontend dev server)
- (Optional) PostgreSQL if switching from SQLite

#### Packet capture (Windows)

Live packet capture and ARP spoofing require **Npcap** (the WinPcap successor) to be installed on the host machine. Without it the API still starts normally — the packet capture feature will be unavailable and a warning will be logged at startup.

1. Download the installer from **[npcap.com](https://npcap.com/#download)**
2. Run the installer — the defaults are fine; "WinPcap API-compatible Mode" is not required
3. Restart the IoTSpy API after installation

On Linux, install `libpcap-dev` instead:

```bash
sudo apt-get install libpcap-dev   # Debian / Ubuntu
sudo yum install libpcap-devel     # RHEL / Fedora
```

On macOS, `libpcap` ships with the OS — no additional install needed.

### Run

```bash
git clone <repo>
cd IoTSpy

# Set a JWT secret (required, minimum 32 characters)
export Auth__JwtSecret="replace-with-a-32+-char-secret"

dotnet run --project src/IoTSpy.Api
```

The API starts at `http://localhost:5000`.
Interactive API docs (Scalar) are available at `http://localhost:5000/scalar` in Development mode.

In a separate terminal, start the frontend dev server:

```bash
cd frontend
npm install
npm run dev
# → http://localhost:3000
```

### Docker

```bash
docker compose up -d
# API: http://localhost:5000
# Proxy: port 8888
```

### First-time setup

Open `http://localhost:3000` in your browser.

1. On first run, you will be redirected to `/setup`. Set an admin password.
2. Log in with username `admin` and the password you set.
3. The dashboard opens; use the **Start Proxy** button in the header to begin capturing traffic.

### Configure an IoT device

Set your IoT device's HTTP proxy to `<host-running-iotspy>:8888`.

Start the proxy:

```http
POST http://localhost:5000/api/proxy/start
Authorization: Bearer <token>
```

For HTTPS interception, download and install the generated CA certificate:

```http
GET http://localhost:5000/api/certificates/root-ca/download
```

(No auth required — this endpoint is public so you can install the CA before logging in.)

---

## Configuration

`src/IoTSpy.Api/appsettings.json` — override via environment variables (double-underscore notation) or `appsettings.Development.json`.

```json
{
  "Database": {
    "Provider": "sqlite",
    "ConnectionString": "Data Source=iotspy.db"
  },
  "Auth": {
    "JwtSecret": "CHANGE_ME_BEFORE_FIRST_RUN_minimum_32_chars",
    "PasswordHash": ""
  },
  "Frontend": {
    "Origin": "http://localhost:3000"
  },
  "Urls": "http://localhost:5000",
  "Resilience": {
    "ConnectTimeoutSeconds": 15,
    "TlsHandshakeTimeoutSeconds": 10,
    "RetryCount": 2,
    "RetryBaseDelaySeconds": 0.5,
    "CircuitBreakerFailureRatio": 0.5,
    "CircuitBreakerSamplingSeconds": 30,
    "CircuitBreakerBreakSeconds": 60
  },
  "Serilog": {
    "MinimumLevel": "Information"
  },
  "RateLimit": {
    "Enabled": true,
    "PermitLimit": 100,
    "WindowSeconds": 60
  },
  "DataRetention": {
    "Enabled": false,
    "CaptureRetentionDays": 30,
    "PacketRetentionDays": 7,
    "ScanJobRetentionDays": 90,
    "OpenRtbEventRetentionDays": 14
  }
}
```

Switch to PostgreSQL:

```json
"Database": {
  "Provider": "postgres",
  "ConnectionString": "Host=localhost;Database=iotspy;Username=iotspy;Password=secret"
}
```

---

## API reference

All endpoints (except `/api/auth/*`, `/api/certificates/root-ca/download`, `/health`, and `/ready`) require `Authorization: Bearer <token>`.

### Health

| Method | Path | Description |
|---|---|---|
| GET | `/health` | Liveness probe — returns JSON health report (DB connectivity check) |
| GET | `/ready` | Readiness probe |

### Auth

| Method | Path | Description |
|---|---|---|
| GET | `/api/auth/status` | Check whether a password is configured |
| POST | `/api/auth/setup` | Set initial password (one-time) |
| POST | `/api/auth/login` | Obtain JWT |

### Proxy

| Method | Path | Description |
|---|---|---|
| GET | `/api/proxy/status` | Current proxy state and settings |
| POST | `/api/proxy/start` | Start the proxy listener |
| POST | `/api/proxy/stop` | Stop the proxy listener |
| PUT | `/api/proxy/settings` | Update proxy settings |

**Proxy settings body:**

```json
{
  "proxyPort": 8888,
  "listenAddress": "127.0.0.1",
  "captureTls": true,
  "captureRequestBodies": true,
  "captureResponseBodies": true,
  "maxBodySizeKb": 1024
}
```

### Captures

| Method | Path | Description |
|---|---|---|
| GET | `/api/captures` | Paginated list with filters |
| GET | `/api/captures/{id}` | Single capture by ID |
| DELETE | `/api/captures/{id}` | Delete one capture |
| DELETE | `/api/captures` | Clear all (optionally filter by deviceId) |

**Query parameters for GET `/api/captures`:**

`deviceId`, `host`, `method`, `statusCode`, `from`, `to`, `q` (full-text), `page`, `pageSize` (max 200)

### Devices

| Method | Path | Description |
|---|---|---|
| GET | `/api/devices` | List discovered devices |
| GET | `/api/devices/{id}` | Device detail |
| PUT | `/api/devices/{id}` | Update device metadata |
| DELETE | `/api/devices/{id}` | Remove device |

### Certificates

| Method | Path | Description |
|---|---|---|
| GET | `/api/certificates` | List generated certificates |
| GET | `/api/certificates/root-ca` | Root CA info |
| GET | `/api/certificates/root-ca/download` | Download root CA as DER (.crt) — no auth required |
| DELETE | `/api/certificates/{id}` | Remove a leaf certificate |

### Scanner

| Method | Path | Description |
|---|---|---|
| POST | `/api/scanner/scan` | Start a new scan job (`{ "targetHost": "...", "portRange": "1-1024" }`) |
| GET | `/api/scanner/jobs` | List all scan jobs |
| GET | `/api/scanner/jobs/{id}` | Get scan job details |
| GET | `/api/scanner/jobs/{id}/findings` | Get findings for a scan job |
| GET | `/api/scanner/jobs/{id}/status` | Get scan job status |
| POST | `/api/scanner/jobs/{id}/cancel` | Cancel a running scan |
| DELETE | `/api/scanner/jobs/{id}` | Delete a scan job and its findings |
| GET | `/api/scanner/device/{deviceId}` | Get all scan results for a device |

### Manipulation

| Method | Path | Description |
|---|---|---|
| GET | `/api/manipulation/rules` | List all manipulation rules |
| POST | `/api/manipulation/rules` | Create a new rule |
| PUT | `/api/manipulation/rules/{id}` | Update a rule |
| DELETE | `/api/manipulation/rules/{id}` | Delete a rule |
| GET | `/api/manipulation/breakpoints` | List all breakpoints |
| POST | `/api/manipulation/breakpoints` | Create a breakpoint (C# or JS) |
| PUT | `/api/manipulation/breakpoints/{id}` | Update a breakpoint |
| DELETE | `/api/manipulation/breakpoints/{id}` | Delete a breakpoint |
| POST | `/api/manipulation/replay` | Replay a captured request with modifications |
| POST | `/api/manipulation/fuzzer` | Start a fuzzer job |
| GET | `/api/manipulation/fuzzer/{id}` | Get fuzzer job status and results |
| DELETE | `/api/manipulation/fuzzer/{id}` | Cancel/delete a fuzzer job |
| POST | `/api/manipulation/ai-mock/generate` | Generate AI mock response for a request |
| DELETE | `/api/manipulation/ai-mock/{host}/cache` | Invalidate AI mock cache for a host |

### Packet Capture

| Method | Path | Description |
|---|---|---|
| GET | `/api/packet-capture/devices` | List network interfaces available for capture |
| GET | `/api/packet-capture/devices/{id}` | Get a specific capture device |
| POST | `/api/packet-capture/start` | Start capturing on a device (`{ "deviceId": "..." }`) |
| POST | `/api/packet-capture/stop` | Stop capturing |
| GET | `/api/packet-capture/status` | Current capture status |
| GET | `/api/packet-capture/packets` | Filtered packet list (protocol, IP, port, time, payload search) |
| GET | `/api/packet-capture/packets/{id}` | Single packet detail |
| POST | `/api/packet-capture/packets/{id}/freeze` | Create freeze frame (hex dump + layer analysis) |
| GET | `/api/packet-capture/packets/{id}/freeze` | Get existing freeze frame |
| POST | `/api/packet-capture/packets/{id}/delete` | Delete a packet |
| GET | `/api/packet-capture/analysis/protocols` | Protocol distribution stats |
| GET | `/api/packet-capture/analysis/patterns` | Top N communication pairs |
| GET | `/api/packet-capture/analysis/suspicious` | Suspicious activity detections |

### OpenRTB

| Method | Path | Description |
|---|---|---|
| GET | `/api/openrtb/events` | List captured OpenRTB events (paginated) |
| GET | `/api/openrtb/events/{id}` | Get a specific OpenRTB event |
| DELETE | `/api/openrtb/events/{id}` | Delete an OpenRTB event |
| GET | `/api/openrtb/pii-policies` | List PII redaction policies |
| POST | `/api/openrtb/pii-policies` | Create a PII policy |
| PUT | `/api/openrtb/pii-policies/{id}` | Update a PII policy |
| DELETE | `/api/openrtb/pii-policies/{id}` | Delete a PII policy |
| GET | `/api/openrtb/pii-audit` | Get PII audit log entries |

### Real-time (SignalR)

#### Traffic hub

Connect to `/hubs/traffic` (pass JWT as `?access_token=<token>` query parameter).

```javascript
const connection = new HubConnectionBuilder()
  .withUrl("/hubs/traffic?access_token=" + token)
  .build();

// All traffic
connection.on("TrafficCapture", capture => { ... });

// Per-device filtering
connection.invoke("SubscribeToDevice", deviceId);
connection.invoke("UnsubscribeFromDevice", deviceId);
```

Payload fields: `id`, `deviceId`, `method`, `scheme`, `host`, `port`, `path`, `query`, `statusCode`, `statusMessage`, `protocol`, `isTls`, `tlsVersion`, `timestamp`, `durationMs`, `clientIp`, `requestBodySize`, `responseBodySize`.

#### Packet capture hub

Connect to `/hubs/packets` (same `?access_token=` pattern). Clients are auto-joined to the `packet-capture-live` group.

```javascript
connection.on("PacketCaptured", packet => { ... });
connection.on("CaptureStatus", status => { ... });
```

Packet fields: `id`, `timestamp`, `protocol`, `sourceIp`, `destinationIp`, `sourcePort`, `destinationPort`, `length`, `payloadPreview`, `tcpFlags`, `isError`, `isRetransmission`.

---

## Solution layout

```
IoTSpy.sln
src/
  IoTSpy.Core/          # Domain models, interfaces, enums
  IoTSpy.Proxy/         # Explicit + transparent proxy, TLS MITM, ARP spoof, resilience
  IoTSpy.Protocols/     # Protocol decoders (MQTT, DNS, CoAP, OpenRTB, telemetry)
  IoTSpy.Scanner/       # Port scan, fingerprinting, CVE lookup, packet capture
  IoTSpy.Manipulation/  # Rules engine, replay, fuzzer, AI mock, OpenRTB PII, packet analysis
  IoTSpy.Storage/       # EF Core DbContext, repositories, migrations
  IoTSpy.Api/           # ASP.NET Core host, 9 controllers, 2 SignalR hubs
frontend/               # Vite 6 + React 19 + TypeScript dashboard
docs/
  architecture.md       # Full architecture spec
  PLAN.md               # Implementation plan, gaps, and roadmap
```

---

## Development

```bash
# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run a single test project
dotnet test src/IoTSpy.Protocols.Tests/IoTSpy.Protocols.Tests.csproj

# Add EF Core migration
dotnet ef migrations add <MigrationName> \
  --project src/IoTSpy.Storage \
  --startup-project src/IoTSpy.Api

# Frontend dev server
cd frontend && npm run dev
```

---

## Development status

See [`docs/PLAN.md`](docs/PLAN.md) for the full implementation plan, identified gaps, and forward-looking roadmap.

| Phase | Scope | Status |
|---|---|---|
| 1 | Scaffold, explicit proxy, HTTP/TLS capture, SQLite, React dashboard | **Complete** |
| 2 | ARP spoof, gateway redirect mode, MQTT, DNS, real-time SignalR stream | **Complete** |
| 3 | Port scan, service fingerprinting, default-credential testing, CVE lookup | **Complete** |
| 4 | Rules engine, request replay, scripted breakpoints (Roslyn + Jint), fuzzer | **Complete** |
| 5 | AI mock engine, CoAP, telemetry decoders, anomaly detection | **Complete** |
| 6 | Packet capture, protocol analysis, communication patterns, suspicious activity | **Complete** |
| — | OpenRTB traffic inspection, PII detection and redaction | **Complete** |
| 7 | Test coverage & CI/CD | **Complete** |
| 8 | Observability & production hardening | **Complete** |
| 9 | Export, reporting, alerting & scheduled scans | **Complete** |
| 10 | Protocol expansion (WebSocket, MQTT proxy, gRPC, Modbus) | **Planned** |
| 11 | UX polish & multi-user support | **Planned** |

---

## Security notes

- IoTSpy is intended for use on networks and devices you own or have explicit authorization to test.
- The root CA private key is stored in the local database. Do not expose the API publicly without proper access controls.
- Change `Auth:JwtSecret` and set a strong password before running in any non-localhost environment.
- `CaptureTls: false` disables MITM and passes HTTPS tunnels through unmodified.

---

## License

See [LICENSE](LICENSE) for details.
