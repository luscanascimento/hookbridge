# HookBridge — Webhook Gateway, Developer Portal & Observability Platform

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]()
[![Target Framework](https://img.shields.io/badge/.NET-10.0-purple.svg)]()
[![Frontend](https://img.shields.io/badge/Angular-22-red.svg)]()
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
- 🔏 **Cryptographic Webhook Signatures:** HMAC-SHA256 signature verification (`X-HookBridge-Signature`) with constant-time equality checks and zero-downtime secret rotation.
- 🛡️ **SSRF & Anti-Replay Defense:** Built-in validation blocking private IPs, link-local, cloud metadata (`169.254.169.254`), and timestamp tolerance drift checks.
- 📡 **Real-time Live Event Inspector:** SignalR-powered live streaming of incoming events, delivery dispatches, and retry attempts.
- 🔍 **Trace Explorer:** End-to-end distributed trace correlation connecting `TenantId`, `EventId`, `DeliveryId`, `AttemptId`, `TraceId`, and `SpanId` across PostgreSQL, RabbitMQ, and external webhooks.
- 🔄 **Safe Delivery Replay:** Authorized replay engine generating fresh timestamps and signatures while preserving original audit lineages.
- 🧪 **Webhook Sandbox & Failure Simulator:** Ephemeral webhook receivers and controllable failure generators (200, 429, 500, timeouts) for automated testing and demonstration.
- 📊 **Endpoint Health & SLOs:** Real-time reliability scoring, success rate metrics, and P95/P99 latency percentiles.

---

## 3. Technology Stack

### Control Plane Backend
- **Framework:** .NET 10 / ASP.NET Core 10 (C#)
- **Persistence:** Entity Framework Core 10 & PostgreSQL
- **Realtime:** ASP.NET Core SignalR
- **Observability:** OpenTelemetry .NET SDK (`ActivitySource`, `Meter`), `ILogger<T>` structured logging
- **API Specification:** OpenAPI 3.1 & RFC 7807 `ProblemDetails`

### Developer Portal Frontend
- **Framework:** Angular 22 (Strict TypeScript, Zoneless compatibility)
- **Reactivity:** Angular Signals (`signal`, `computed`, Signal-based services)
- **Control Flow:** Modern `@if`, `@for`, `@switch`, `@defer`
- **Styling:** Tailwind CSS & Angular CDK
- **Realtime Client:** `@microsoft/signalr`

### Data Plane & Infrastructure (EventFlow)
- **Message Broker:** RabbitMQ (Topic Exchange, DLX, QoS Prefetch)
- **Distributed Cache & Locks:** Redis
- **Resilience:** Polly v8 (Exponential backoff, Jitter, Circuit Breaker, Timeout)
- **Tracing APM:** Jaeger / OpenTelemetry Collector

---

## 4. Documentation & Governance

- [Master Project Context & Engineering Standards](docs/PROJECT_CONTEXT.md)
- [Roadmap & Progress Tracker](docs/ROADMAP_PROGRESS.md)
- [EventFlow Integration Contract](docs/architecture/integration-contract.md)

---

## 5. Development Roadmap

The project is developed incrementally following the 32-phase master roadmap:

- [x] **FASE 0 — EventFlow Contract & Boundary Analysis**
- [ ] **FASE 1 — Product Scope, Domain Architecture & Initial Threat Model**
- [ ] **FASE 2 — Backend Foundation (.NET 10 Solution, ProblemDetails, HealthChecks)**
- [ ] **FASE 3 — Authentication & Multi-Tenant Authorization**
- [ ] *... (See [ROADMAP_PROGRESS.md](docs/ROADMAP_PROGRESS.md) for the complete roadmap)*

---

## 6. License

Licensed under the [MIT License](LICENSE).
