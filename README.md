# HookBridge — Webhook Gateway, Developer Portal & Observability Platform

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]()
[![Target Framework](https://img.shields.io/badge/.NET-10.0-purple.svg)]()
[![Frontend](https://img.shields.io/badge/Angular-22-red.svg)]()
[![Docker](https://img.shields.io/badge/Docker-Ready-blue.svg)]()
[![License](https://img.shields.io/badge/license-MIT-blue.svg)]()

> **HookBridge** is an enterprise-grade **Webhook Gateway, Developer Portal, and Observability Platform** built on top of the **EventFlow** distributed asynchronous processing engine.

---

## 1. System Overview & Architecture

HookBridge acts as the **Control Plane, Developer Experience, and Observability Layer**, while EventFlow serves as the **Data Plane and Processing Engine**.

```
                ┌────────────────────────────────────────┐
                │               HOOKBRIDGE               │
                │             CONTROL PLANE              │
                │                                        │
                │ Angular 22 (Zoneless, Signals)          │
                │ ASP.NET Core 10 / .NET 10               │
                │ Authentication / Authorization (RBAC)  │
                │ Multi-Tenancy & Resource Isolation     │
                │ Endpoint Registry & Subscriptions      │
                │ API Keys & Webhook Secrets             │
                │ Delivery Management & Replay           │
                │ Real-time SignalR Delivery Hub         │
                │ Trace Explorer & Live Event Inspector  │
                │ Webhook Sandbox Receiver               │
                │ Failure Simulator & Incident View      │
                │ OpenAPI 3.1 & Developer Documentation  │
                └────────────────┬───────────────────────┘
                                 │
                                 │ W3C TraceContext + AMQP/HTTP
                                 ▼
                ┌────────────────────────────────────────┐
                │                EVENTFLOW                │
                │               DATA PLANE                │
                │                                        │
                │ Transactional Outbox (PostgreSQL)      │
                │ RabbitMQ Broker (Topic Exchange)       │
                │ Scalable Consumer Workers              │
                │ Distributed Idempotency (Redis/PgSQL)  │
                │ Resilience (Polly v8: Backoff/Circuit) │
                │ SSRF Protection (SsrfGuard)            │
                │ Dead Letter Queue (DLQ) & Purge/Replay │
                │ OpenTelemetry Telemetry Pipeline       │
                └────────────────────────────────────────┘
```

---

## 2. Core Capabilities

- 🔐 **Multi-Tenancy & RBAC:** Strict tenant data isolation from database queries to real-time SignalR streams.
- 🔏 **Cryptographic Webhook Signatures:** Zero-allocation HMAC-SHA256 signature verification (`X-HookBridge-Signature: t=...,v1=...`) with constant-time equality checks and zero-downtime secret rotation.
- 🛡️ **SSRF & Anti-Replay Defense:** Built-in validation blocking private IPs (RFC 1918), link-local, cloud metadata (`169.254.169.254`), and timestamp tolerance drift checks ($\le 300\text{s}$).
- 📡 **Real-time Live Event Inspector:** SignalR-powered live streaming of incoming events, delivery dispatches, and retry attempts with tenant filtering.
- 🔍 **Trace Explorer:** End-to-end distributed trace correlation connecting `TenantId`, `EventId`, `DeliveryId`, `AttemptId`, `TraceId`, and `SpanId` across PostgreSQL, RabbitMQ, and external webhooks.
- 🔄 **Safe Delivery Replay:** Authorized replay engine generating fresh timestamps and signatures while preserving original audit lineages and ancestor tracking.
- 🧪 **Webhook Sandbox & Failure Simulator:** Ephemeral webhook receivers and controllable failure generators (200, 429, 500, timeouts, chaos) for automated testing and demonstration.
- 📊 **Endpoint Health & Reliability Metrics:** Real-time reliability scoring (0-100), uptime percentage, 24-hour hourly health buckets, and P50/P90/P95/P99 latency percentiles.

---

## 3. Quickstart with Docker Compose

### Prerequisites
- Docker Engine 24+ & Docker Compose v2+
- Node.js 22+ (for local frontend development)
- .NET 9 or 10 SDK (for local backend development)

### Launch Full Multi-Service Stack
```bash
# 1. Clone repository
git clone git@github.com:luscanascimento/hookbridge.git
cd hookbridge

# 2. Copy environment template
cp .env.example .env

# 3. Start all services (API, Web UI, PostgreSQL, RabbitMQ, Redis, Jaeger)
docker compose up -d

# 4. Open Developer Portal
open http://localhost:4200
```

### Services & Port Mappings

| Service | Port | Description |
| :--- | :--- | :--- |
| **HookBridge Web UI** | `http://localhost:4200` | Angular 22 Developer Portal & Dashboard |
| **HookBridge API** | `http://localhost:8080` | Control Plane REST API & SignalR Hub |
| **Scalar OpenAPI Docs** | `http://localhost:8080/scalar/v1` | Interactive OpenAPI 3.1 Explorer |
| **PostgreSQL** | `localhost:5432` | Multi-Tenant Database |
| **RabbitMQ Management**| `http://localhost:15672` | AMQP Message Broker UI (`guest`/`guest`) |
| **Redis** | `localhost:6379` | Distributed State & Idempotency Cache |
| **Jaeger APM** | `http://localhost:16686` | Distributed Tracing UI |

---

## 4. Technology Stack

### Control Plane Backend
- **Framework:** C# / .NET 10 / ASP.NET Core 10
- **Persistence:** Entity Framework Core 10 & PostgreSQL (Global Multi-Tenant Filters, AsSplitQuery, Zero-Allocation aggregations)
- **Realtime:** ASP.NET Core SignalR (Tenant-isolated groups, binary & JSON protocols)
- **Observability:** OpenTelemetry .NET SDK (`ActivitySource`, `Meter`), `ILogger<T>` structured logging, W3C TraceContext
- **API Specification:** OpenAPI 3.1 & RFC 7807 `ProblemDetails`

### Developer Portal Frontend
- **Framework:** Angular 22 (Strict TypeScript, Zoneless compatibility)
- **Reactivity:** Angular Signals (`signal`, `computed`, Signal-based feature services)
- **Control Flow:** Modern `@if`, `@for`, `@switch`, `@defer`
- **Styling:** Tailwind CSS & Angular CDK
- **Realtime Client:** `@microsoft/signalr`

### Data Plane & Infrastructure (EventFlow)
- **Message Broker:** RabbitMQ (Topic Exchange, DLX, QoS Prefetch)
- **Distributed Cache & Locks:** Redis 7.4
- **Resilience:** Polly v8 (Exponential backoff, Jitter, Circuit Breaker, Timeout)
- **Tracing APM:** Jaeger / OpenTelemetry Collector

---

## 5. Development & Testing

```bash
# Run backend test suite (420+ Unit, Integration, Chaos & Security Tests)
dotnet test

# Run frontend production build
cd src/HookBridge.Web
npm install
npm run build -- --configuration production
```

---

## 6. Architecture & Documentation Directory

- [Master Project Context & Governance](docs/PROJECT_CONTEXT.md)
- [Roadmap & Progress Tracker](docs/ROADMAP_PROGRESS.md)
- [Architecture Blueprint & Design](docs/architecture/architecture-blueprint.md)
- [EventFlow Integration Contract](docs/architecture/integration-contract.md)
- [Developer Guide & SDK Recipes](docs/development/developer-guide.md)
- [Domain Model & Entity Relationships](docs/architecture/domain-model.md)
- [Threat Model & Security Hardening](docs/security/threat-model.md)

---

## 7. Roadmap Status

| Phase | Milestone | Status |
| :---: | :--- | :---: |
| 0 | EventFlow Contract & Boundary Analysis | ✅ Completed |
| 1 | Product Scope, Domain Architecture & Initial Threat Model | ✅ Completed |
| 2 | Backend Foundation (.NET 10 Solution, ProblemDetails, HealthChecks) | ✅ Completed |
| 3 | Authentication & Multi-Tenant Authorization | ✅ Completed |
| 4 | Control Plane (Apps, Endpoints, Subscriptions, API Keys, Secrets) | ✅ Completed |
| 5 | Webhook Signing (HMAC-SHA256, Secret Rotation, Anti-Replay) | ✅ Completed |
| 6 | EventFlow Integration Client & Publishing Pipeline | ✅ Completed |
| 7 | Deliveries & Attempt Tracking with DLQ Visibility | ✅ Completed |
| 8 | Authorized Delivery Replay Engine | ✅ Completed |
| 9 | SignalR Realtime Delivery Hub & Tenant Groups | ✅ Completed |
| 10 | Angular 22 Foundation (Strict TS, Zoneless, Modern Routing) | ✅ Completed |
| 11 | Design System (Tailwind, Tokens, Dark Mode, Skeletons) | ✅ Completed |
| 12 | Executive Dashboard (Success Rate, Latency, DLQ, Metrics) | ✅ Completed |
| 13 | Endpoint Management Portal & Secret Rotation UI | ✅ Completed |
| 14 | Live Event Inspector & Realtime Timeline | ✅ Completed |
| 15 | Trace Explorer (Event, Delivery, Trace, Log & Audit Correlation) | ✅ Completed |
| 16 | Payload Inspector & Highlighting | ✅ Completed |
| 17 | Endpoint Health & Reliability Metrics | ✅ Completed |
| 18 | Event Schemas, Versioning & Compatibility | ✅ Completed |
| 19 | Developer Documentation & Code Snippets (cURL, TS, C#) | ✅ Completed |
| 20 | Webhook Sandbox Receiver & Realtime Inspection | ✅ Completed |
| 21 | Delivery Failure Simulator (200, 429, 500, Timeout, Chaos) | ✅ Completed |
| 22 | OpenTelemetry Observability (Traces, Metrics, Logs, Jaeger) | ✅ Completed |
| 23 | Adversarial Security Hardening (IDOR, SSRF, XSS, Replay) | ✅ Completed |
| 25 | Distributed Chaos & Failure Testing (Broker/DB Outages) | ✅ Completed |
| 26 | Performance & Bottleneck Profiling | ✅ Completed |
| 27 | Docker & Docker Compose Multi-Service Environment | ✅ Completed |
| 28 | CI/CD Pipeline (GitHub Actions, Analyzers, Security Audits) | ✅ Completed |
| 29 | Final Project Documentation & Architecture Blueprint | ✅ Completed |
| 30 | Multi-Role Engineering Review (Staff, Security, SRE, Product) | ⏳ Next |
| 31 | Release Candidate & Production Verification | ⬜ Pending |

---

## 8. License

Licensed under the [MIT License](LICENSE).

