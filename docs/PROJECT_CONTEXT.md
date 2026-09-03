# HOOKBRIDGE — MASTER PROJECT CONTEXT & ENGINEERING GOVERNANCE

> **Role & Persona:** Staff/Principal Software Engineer, Solution Architect & Security Engineer  
> **Repository:** `git@github.com:luscanascimento/hookbridge.git`  
> **Status:** Active Development (Incremental Delivery)  
> **Related System (Data Plane):** EventFlow (`/home/sirbu/projects/eventflow`)

---

## 1. Product Vision & Architecture

**HookBridge** is an enterprise-grade **Webhook Gateway + Developer Portal + Observability Platform**, engineered on top of **EventFlow** (the distributed asynchronous processing engine / Data Plane).

HookBridge is designed not merely as a dashboard, but as a production-ready developer infrastructure product inspired by the DX, reliability, and security standards of Stripe, Svix, Hookdeck, Postman, GitHub, Vercel, and Linear.

### Architectural Division of Responsibilities

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
                │ Real-time SignalR Hub                  │
                │ Trace Explorer & Live Event Inspector  │
                │ Sandbox Webhook Receiver               │
                │ Failure Simulator & Incident View      │
                │ OpenAPI 3.1 & Developer Portal Docs    │
                └────────────────┬───────────────────────┘
                                 │
                                 │ Integration Contract (W3C TraceContext + AMQP/HTTP)
                                 ▼
                ┌────────────────────────────────────────┐
                │                EVENTFLOW                │
                │               DATA PLANE                │
                │                                        │
                │ Transactional Outbox (PostgreSQL)      │
                │ RabbitMQ Broker (Topic Exchange)       │
                │ Consumer Workers                       │
                │ Distributed Idempotency (Redis/PgSQL)  │
                │ Resilience (Polly v8: Backoff/Circuit) │
                │ SSRF Protection (SsrfGuard)            │
                │ Dead Letter Queue (DLQ) & Purge/Replay │
                │ OpenTelemetry Telemetry Pipeline       │
                └────────────────────────────────────────┘
```

* **EventFlow (Data Plane):** Handles transactional outbox persistence, message brokering via RabbitMQ, asynchronous worker consumption, distributed idempotency locks, resilient HTTP dispatching with exponential backoff and circuit breaking, SSRF prevention, and DLQ routing.
* **HookBridge (Control Plane):** Manages multi-tenant entities (Tenants, Users, Applications, Endpoints, Subscriptions, API Keys, Webhook Secrets), HMAC-SHA256 signature generation and rotation, delivery tracking and attempt lifecycles, live event inspection via SignalR, distributed trace explorer, audit logging, failure simulation, sandbox receiver, and developer documentation.

---

## 2. Technology Stack & Architectural Standards

### Frontend
- **Framework:** Angular 22 (Strict TypeScript mode, zoneless compatibility).
- **Core Primitives:** Standalone components, Signals (`signal`, `computed`), modern control flow (`@if`, `@for`, `@switch`, `@defer`), Functional HTTP Interceptors, Reactive Forms, `@microsoft/signalr`.
- **State Management:** Pragmatic Signal-based feature services / facades. No indiscriminate NgRx/global state without technical justification.
- **UI & Styling:** Tailwind CSS, Angular CDK, custom semantic components tailored for high information density, dark mode, accessible keyboard navigation, loading skeletons, empty and reconnecting states.

### Backend
- **Framework & Runtime:** C# / .NET 10 / ASP.NET Core 10.
- **Data & Persistence:** Entity Framework Core 10 with PostgreSQL (Async I/O, `CancellationToken`, strict indexing, foreign keys, concurrency tokens).
- **APIs & Realtime:** Minimal APIs / Controllers (justified by complexity), OpenAPI 3.1, RFC 7807 `ProblemDetails`, SignalR Hubs scoped to tenant groups.
- **Observability:** OpenTelemetry .NET SDK (`Activity`, `ActivitySource`, `Meter`), structured logging with `ILogger<T>` (zero string interpolation in log templates), W3C TraceContext propagation.

---

## 3. Security & Multi-Tenancy Principles

1. **Strict Multi-Tenancy:**
   - Hierarchy: $\text{Tenant} \to \text{Users} \to \text{Applications} \to \text{Endpoints} \to \text{Subscriptions} \to \text{Deliveries} \to \text{Attempts}$.
   - Every read, update, delete, replay, and SignalR stream is strictly filtered and validated against the authenticated tenant context. Zero trust in client-sent tenant IDs.
2. **SSRF Guarding:**
   - Outbound dispatch validation against private IP ranges (RFC 1918), loopback (`127.0.0.1`, `::1`), link-local (`169.254.0.0/16`), cloud metadata endpoints, internal DNS resolution, and unsafe redirect chains.
3. **Webhook HMAC-SHA256 Signatures:**
   - Format: `X-HookBridge-Signature: t=<timestamp>,v1=<hmac>`.
   - Canonical payload: `t.<rawPayload>`.
   - Constant-time comparison (`CryptographicOperations.FixedTimeEquals`).
   - Secret rotation window support (dual-signing / dual-acceptance during transition).
4. **Anti-Replay & Tolerance Windows:**
   - Timestamp validation within strict tolerance (e.g., 5 minutes) combined with unique delivery identifiers.
5. **Data Protection & Sanitization:**
   - Zero leakage of secrets, passwords, or full API keys in logs or error responses.
   - Comprehensive XSS prevention: JSON payloads and traces treated as untrusted text, never rendered via `innerHTML`.

---

## 4. Development Process & Definition of Done

- **Incremental Execution:** Develop phase by phase according to the Master Roadmap.
- **Commit Quality:** Every commit must be functional, accompanied by tests, security review, architecture review, and documentation.
- **Git Remote:** `git@github.com:luscanascimento/hookbridge.git`.
- **Definition of Done:**
  - Feature operational and verified.
  - Multi-tenant isolation verified with regression tests.
  - Error handling with RFC 7807 `ProblemDetails`.
  - Structured logging with correlation IDs and W3C TraceContext.
  - UI states handled (Loading, Empty, Error, Reconnecting).
  - Documented in ADRs / Technical Specs.


---

## 5. Current State & Handoff (Last Updated: 2026-09-03)

### Completed Milestones
1. **FASE 0 — EventFlow Contract & Boundary Analysis** (`3490ab7`)
   - Data plane analysis of EventFlow (`/home/sirbu/projects/eventflow`).
   - Contract definition: [`docs/architecture/integration-contract.md`](file:///home/sirbu/projects/hookbridge/docs/architecture/integration-contract.md).
2. **FASE 1 — Product Scope, Domain Architecture & Threat Model** (`aeaaab7`)
   - Product scope, domain models, C4 diagrams, initial threat model, and ADRs (0001 to 0004).
3. **FASE 2 — Backend Foundation** (`896a013`)
   - .NET 10 solution setup with Clean Architecture (`HookBridge.sln`).
   - EF Core 10 PostgreSQL configurations with global multi-tenant query filters.
   - Middlewares: `GlobalExceptionHandler` (RFC 7807 ProblemDetails), `SecurityHeadersMiddleware`, `TenantResolutionMiddleware`.
   - Health probes (`/health/live`, `/health/ready`) and diagnostics endpoints.
4. **FASE 3 — Authentication & Multi-Tenant Authorization** (`aca43e1`)
   - PBKDF2 HMAC-SHA256 password hashing and JWT token issuance with dynamic configuration.
   - Refresh token entity, secure rotation, and compromise detection.
   - RBAC policies: `RequireTenantAdmin`, `RequireDeveloper`, `RequireViewer`, `RequireSystemOperator`.
   - Full test suite: 56/56 unit and integration tests passing.
5. **FASE 4 — Control Plane** (`feat: implement webhook control plane`)
   - CRUD and business use cases for `Applications`, `Endpoints`, `Subscriptions`, `ApiKeys`, `WebhookSecrets`, and `AuditEntries`.
   - Cryptographic API Key issuance (`hb_live_...`, `hb_test_...`) with SHA-256 storage and scope authorization.
   - AES-256-GCM encryption for webhook secrets at rest with versioned secret rotation and revocation.
   - SSRF protection guard blocking private/loopback/metadata destinations.
   - Complete audit trail ledger for all mutating control plane actions.
   - Full test suite: 121/121 unit and integration tests passing (95 unit + 26 integration).

### Next Session Objective
- **FASE 5 — Webhook Signing**:
  - Implement HMAC-SHA256 signature generator (`X-HookBridge-Signature: t=timestamp,v1=signature`).
  - Secret rotation tolerance windows (dual verification against Active and Rotating secrets).
  - Anti-replay timestamp verification.
  - Target commit: `feat: implement webhook signing`.
