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
| Frontend | Vinext (Cloudflare) |
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

### First-time setup

1. **Set a password** via the setup endpoint (only works once, before any password is configured):

   ```http
   POST http://localhost:5000/api/auth/setup
   { "password": "your-password" }
   ```

2. **Log in** to receive a JWT:

   ```http
   POST http://localhost:5000/api/auth/login
   { "username": "admin", "password": "your-password" }
   ```

3. Use the returned token as `Authorization: Bearer <token>` on all subsequent requests.

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
Authorization: Bearer <token>
```

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
frontend/               # Vinext dashboard (Phase 1+)
docs/
  architecture.md       # Full architecture spec and phase breakdown
  PLAN.md               # Implementation plan and session handoff notes
```

---

## Development status

See [`docs/PLAN.md`](docs/PLAN.md) for the full phased implementation plan and current progress.

| Phase | Scope | Status |
|---|---|---|
| 1 | Scaffold, explicit proxy, HTTP/TLS capture, SQLite, basic dashboard | In progress |
| 2 | ARP spoof, gateway redirect mode, MQTT, DNS, real-time SignalR stream | Planned |
| 3 | Port scan, service fingerprinting, default-credential testing, CVE lookup | Planned |
| 4 | Rules engine, request replay, scripted breakpoints (Roslyn + Jint) | Planned |
| 5 | AI mock engine, CoAP, telemetry decoders, anomaly detection | Planned |

---

## Security notes

- IoTSpy is intended for use on networks and devices you own or have explicit authorization to test.
- The root CA private key is stored in the local database. Do not expose the API publicly without proper access controls.
- Change `Auth:JwtSecret` and set a strong password before running in any non-localhost environment.
- `CaptureTls: false` disables MITM and passes HTTPS tunnels through unmodified.
