## Project Summary
IoT network security platform: transparent MITM proxy + protocol analyzer + pen-test suite.

## Tech Stack
- Backend: .NET 10 / C# (ASP.NET Core 10, minimal API + controllers)
- Real-time: SignalR (traffic streaming)
- Packet capture: SharpPcap / PacketDotNet
- TLS MITM: BouncyCastle
- Storage: SQLite default / PostgreSQL optional (EF Core 10)
- Frontend: Vinext (Cloudflare) - `npx skills add cloudflare/vinext`
- AI: Pluggable (Claude API / OpenAI / local Ollama)

## Architecture Decisions
- Three interception modes: ARP spoof, gateway redirect (iptables), explicit proxy
- Unified capture pipeline regardless of interception mode
- SQLite default, Postgres pluggable via appsettings.json `Database:Provider`
- Single-user JWT password auth (set at startup)
- Hybrid dashboard: split-pane detail view + timeline swimlane view (toggle)

## Project Structure (solution)
See `docs/architecture.md` for full structure.
- `IoTSpy.Core` - domain models, interfaces
- `IoTSpy.Proxy` - interception engine + TLS MITM + CA management
- `IoTSpy.Protocols` - HTTP, MQTT, CoAP, DNS/mDNS, telemetry decoders
- `IoTSpy.Scanner` - port scan, service fingerprinting, cred testing, CVE lookup, config audit
- `IoTSpy.Manipulation` - rules engine, scripted breakpoints, replay/fuzzer, AI mock
- `IoTSpy.Storage` - EF Core repositories, migrations
- `IoTSpy.Api` - ASP.NET Core API + SignalR hub
- `frontend/` - Vinext app

## Phased Delivery
- Phase 1 (CURRENT): Full scaffold + explicit proxy + HTTP/TLS capture + SQLite + basic Vinext dashboard
- Phase 2: ARP spoof + gateway mode + MQTT + DNS + real-time SignalR stream
- Phase 3: Port scan + service fingerprinting + credential testing + CVE lookup
- Phase 4: Rules engine + request replay + scripted breakpoints
- Phase 5: AI mock engine + CoAP + telemetry detection + anomaly detection

## Protocols to Decode
HTTP/HTTPS, MQTT (1883/8883), CoAP (UDP 5683), DNS/mDNS, Telemetry (Datadog, AWS Firehose, Splunk HEC, Azure Monitor)

## Pen-test Features
Port scan + service fingerprinting, default credential testing, CVE lookup (NVD/OSV APIs), config audit (root SSH, Telnet, HTTP admin, UPnP, anon MQTT)

## Active MITM Features
Request/response tampering, declarative rules engine, scripted breakpoints (C# Roslyn + Jint JS), request replay + fuzzer, AI mock engine (schema learning from captured traffic)

## AI Mock Engine
- Pluggable provider: Claude (claude-sonnet-4-6) / OpenAI / local Ollama
- Schema learning from captured historical responses
- User-configurable behavior description per host

## Naming Convention
- Project/namespace name: `IoTSpy` (capital I, o, T, S)
- Solution file: `IoTSpy.sln`
- All C# project names prefixed: `IoTSpy.*`
- Docker image: `iotspy` (lowercase for container names)
- Directory: `iotspy/` (lowercase, matches existing git repo)

## Status
- [ ] Phase 1 implementation in progress
- See `docs/architecture.md` for detailed spec
