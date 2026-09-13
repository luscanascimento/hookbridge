# HookBridge Release Candidate 1 (v1.0.0-rc.1)

We are thrilled to announce **HookBridge Release Candidate 1 (v1.0.0-rc.1)** — an enterprise-grade **Webhook Gateway, Developer Portal, and Observability Platform**.

---

## Key Highlights

1. **Enterprise Control Plane (.NET 10):**
   - Clean Architecture backend with strict multi-tenant data isolation.
   - High-performance zero-allocation HMAC-SHA256 webhook signing and verification.
   - Out-of-the-box SSRF defense and anti-replay tolerance windows.
   - Comprehensive delivery tracking, authorized replay engine, and DLQ management.

2. **Modern Developer Portal (Angular 22 Zoneless):**
   - Ultra-fast, zoneless reactive UI built with Angular 22 Signals and Tailwind CSS.
   - Real-time SignalR streaming for live webhook dispatches and attempts.
   - Full diagnostic observability with Trace Explorer, Payload Inspector, Health Metrics, and Schema Registry.
   - Ephemeral Webhook Sandbox and Delivery Failure Simulator for local and staging developer testing.

3. **Full-Stack Containerization & Observability:**
   - Turnkey Docker Compose multi-service deployment (API, Web, PostgreSQL, RabbitMQ, Redis, Jaeger).
   - W3C TraceContext distributed tracing propagation with OpenTelemetry.

4. **Production Quality & Governance:**
   - Over 565+ automated unit, integration, chaos, and security tests with 100% pass rate.
   - Multi-role engineering sign-off from Staff Software Engineer, Security, SRE, and Product Design leads.

---

## Quickstart

```bash
git clone git@github.com:luscanascimento/hookbridge.git
cd hookbridge
cp .env.example .env
docker compose up -d
```

Visit the Developer Portal at `http://localhost:4200` and the interactive OpenAPI docs at `http://localhost:8080/scalar/v1`.
