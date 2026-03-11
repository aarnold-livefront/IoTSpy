# IoTSpy

IoT network security platform: transparent MITM proxy, protocol analyzer, and pen-test suite for IoT device research.

> **Use case:** Point an IoT device at the proxy, capture and inspect its HTTP/HTTPS traffic in real time, decode proprietary protocols, and run a lightweight pen-test suite against the device.

---

## Tech stack

| Layer | Technology |
|---|---|
| Backend | .NET 10 / C# — ASP.NET Core 10, minimal API + controllers |
| Real-time | SignalR (live traffic streaming to dashboard) |
| Packet capture | SharpPcap / PacketDotNet |
| TLS MITM | BouncyCastle (dynamic per-host certificate generation) |
| Resilience | Polly 8 (retry, circuit-breaker, timeout) |
| Storage | SQLite (default) / PostgreSQL (pluggable via appsettings) — EF Core 10 |
| Frontend | Vite 6 + React 19 + TypeScript |
| AI | Pluggable: Claude API / OpenAI / local Ollama |

---

## Quick start

### Prerequisites

- .NET 10 SDK
- (Optional) PostgreSQL if switching from SQLite

### Run

```bash
git clone <repo>
cd iotspy

# Set a JWT secret (required)
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

### First-time setup

Open `http://localhost:3000` in your browser.

1. On first run, you will be redirected to `http://localhost:3000/setup`. Set an admin password.
2. You will then be redirected to the login page. Log in with username `admin` and the password you set.
3. The dashboard opens; use the **Start Proxy** button in the header to begin capturing traffic.

Alternatively, you can set up via the raw API:

```http
POST http://localhost:5000/api/auth/setup
{ "password": "your-password" }

POST http://localhost:5000/api/auth/login
{ "username": "admin", "password": "your-password" }
```

Use the returned token as `Authorization: Bearer <token>` on all subsequent API requests.

### Configure an IoT device

Set your IoT device's HTTP proxy to `<host-running-iotspy>:8888`.

Start the proxy:

```http
POST http://localhost:5000/api/proxy/start
Authorization: Bearer <token>
```

HTTPS is intercepted by default. To trust the generated CA, download it:

```http
GET http://localhost:5000/api/certificates/root-ca/download
```

(No auth header required — this endpoint is public so you can download the CA before logging in.)

Install the downloaded `.crt` on the IoT device (or in the OS trust store of the machine you are testing from).

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

All endpoints (except `/api/auth/*`) require `Authorization: Bearer <token>`.

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
| GET | `/api/certificates/root-ca/download` | Download root CA as DER (.crt) |
| DELETE | `/api/certificates/{id}` | Remove a leaf certificate |

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
| POST | `/api/packet-capture/freeze` | Freeze analysis view |
| POST | `/api/packet-capture/unfreeze` | Unfreeze analysis view |
| GET | `/api/packet-capture/freeze/status` | Freeze status |

### Real-time (SignalR)

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
  IoTSpy.Proxy/         # Explicit proxy server, TLS MITM CA, resilience pipelines
  IoTSpy.Protocols/     # Protocol decoders (Phase 2+: MQTT, CoAP, DNS, telemetry)
  IoTSpy.Scanner/       # Port scan, fingerprinting, CVE lookup (Phase 3+)
  IoTSpy.Manipulation/  # Rules engine, replay/fuzzer, AI mock (Phase 4-5+)
  IoTSpy.Storage/       # EF Core DbContext, repositories, migrations
  IoTSpy.Api/           # ASP.NET Core host, controllers, SignalR hub
frontend/               # Vite 6 + React 19 + TypeScript dashboard
docs/
  architecture.md       # Full architecture spec and phase breakdown
  PLAN.md               # Implementation plan and session handoff notes
```

---

## Development status

See [`docs/PLAN.md`](docs/PLAN.md) for the full phased implementation plan and current progress.

| Phase | Scope | Status |
|---|---|---|
| 1 | Scaffold, explicit proxy, HTTP/TLS capture, SQLite, React dashboard | **Complete** |
| 2 | ARP spoof, gateway redirect mode, MQTT, DNS, real-time SignalR stream | **Complete** |
| 3 | Port scan, service fingerprinting, default-credential testing, CVE lookup | **Complete** |
| 4 | Rules engine, request replay, scripted breakpoints (Roslyn + Jint) | **Complete** |
| 5 | AI mock engine, CoAP, telemetry decoders, anomaly detection | **Complete** |
| 6 | Packet capture, protocol analysis, communication patterns, suspicious activity | **Complete** |

---

## Security notes

- IoTSpy is intended for use on networks and devices you own or have explicit authorization to test.
- The root CA private key is stored in the local database. Do not expose the API publicly without proper access controls.
- Change `Auth:JwtSecret` and set a strong password before running in any non-localhost environment.
- `CaptureTls: false` disables MITM and passes HTTPS tunnels through unmodified.
