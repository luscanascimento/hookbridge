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
5. **FASE 4 — Control Plane** (`3c5d10e`)
   - CRUD and business use cases for `Applications`, `Endpoints`, `Subscriptions`, `ApiKeys`, `WebhookSecrets`, and `AuditEntries`.
   - Cryptographic API Key issuance (`hb_live_...`, `hb_test_...`) with SHA-256 storage and scope authorization.
   - AES-256-GCM encryption for webhook secrets at rest with versioned secret rotation and revocation.
   - SSRF protection guard blocking private/loopback/metadata destinations.
   - Complete audit trail ledger for all mutating control plane actions.
   - Full test suite: 121/121 unit and integration tests passing.
6. **FASE 5 — Webhook Signing** (`c433848`)
   - HMAC-SHA256 signature generator emitting standardized `X-HookBridge-Signature: t=timestamp,v1=signature` headers.
   - Canonical payload construction (`t.payload`) with UTF-8 encoding.
   - Multi-secret rotation window support with dual-signature emission (`t=...,v1=sigActive,v1=sigRotating`).
   - Anti-replay timestamp tolerance verification ($\le 300\text{s}$) and constant-time verification (`CryptographicOperations.FixedTimeEquals`).
   - Developer portal test endpoints for generating and verifying webhook signatures.
   - Full test suite: 134/134 unit and integration tests passing.
7. **FASE 6 — EventFlow Integration Client & Publishing Pipeline** (`ef6b50c`)
   - HTTP typed integration client (`IEventFlowClient` / `EventFlowClient`) for forwarding events to EventFlow transactional outbox.
   - Ingestion and subscription matching engine provisioning `Delivery` records for active endpoint patterns (`*`, `order.*`, exact).
   - DLQ management use cases and endpoints (Peek, Replay, Purge) for control plane dead-letter governance.
   - W3C TraceContext propagation across HTTP headers and distributed activities (`HookBridge.PublishEvent`).
   - Full test suite: 154/154 unit and integration tests passing.
8. **FASE 7 — Deliveries & Attempt Tracking with DLQ Visibility** (`69cb4e1`)
   - Paginated delivery querying with multidimensional filters (endpoint, status, event type, date range, correlation ID).
   - Detailed delivery inspection endpoint with complete historical attempts execution timeline.
   - Real-time aggregate statistics endpoint (total, success, failed, pending, DLQ count, success rate %, average latency ms).
   - Delivery attempt recorder with status transitions (`Dispatched`, `Success`, `Failed`, `DeadLettered`) and OpenTelemetry metric updates.
   - Full test suite: 158/158 unit and integration tests passing.
9. **FASE 8 — Authorized Delivery Replay Engine** (`feat: add delivery replay`)
   - Single delivery replay (`POST /api/v1/deliveries/{id}/replay`) with endpoint pre-validation and latest payload retrieval.
   - Bulk delivery replay (`POST /api/v1/deliveries/replay`) supporting batch re-execution by status, endpoint, event type, and explicit IDs.
   - Full ancestry and descendant replay lineage tracking (`GET /api/v1/deliveries/{id}/lineage` and `OriginalDeliveryId` link).
   - Integration with EventFlow transactional event ingestion, audit logging (`Delivery.Replayed`, `Delivery.BulkReplayed`), and OpenTelemetry metric updates (`HookBridgeDiagnostics.ReplaysTriggered`).
   - Full test suite: 167/167 unit and integration tests passing (135 unit + 32 integration).
10. **FASE 9 — SignalR Realtime Delivery Hub & Tenant Groups** (`feat: add realtime delivery updates`)
    - SignalR hub for live delivery events strictly scoped to authenticated tenant groups (`/hubs/deliveries`).
    - WebSocket JWT token extraction via query parameter `?access_token=...` during WebSocket handshake.
    - Tenant-isolated and endpoint/application granular group subscription engine (`SubscribeToEndpoint`, `SubscribeToApplication`).
    - `IDeliveryRealtimeNotifier` abstraction in Application layer and `DeliveryRealtimeNotifier` implementation in API layer.
    - OpenTelemetry metrics tracking: `ActiveSignalRConnections` and `RealtimeEventsBroadcasted`.
    - Full test suite: 183/183 unit and integration tests passing (146 unit + 37 integration).
11. **FASE 10 — Angular 22 Foundation (Strict TS, Zoneless, Modern Routing)** (`b7d5716`)
    - Angular 22 SPA architecture under `src/HookBridge.Web` with `provideZonelessChangeDetection()` and standalone components.
    - Signal-driven state management with `AuthService` (`currentUser`, `token`, `tenantId`, `userRole`) and `SignalRService` (`status`, `events`, `latestEvent`).
    - Functional HTTP interceptors (`authInterceptor`, `errorInterceptor` parsing RFC 7807 ProblemDetails) and router guards (`authGuard`, `guestGuard`, `roleGuard`).
    - High-density dark mode developer portal shell layout with sidebar navigation, tenant identifier indicator, and live SignalR status indicator.
12. **FASE 11 — Design System (Tailwind, Tokens, Dark Mode, Skeletons & UI Components)** (`0882737`)
    - Comprehensive UI component suite in `src/HookBridge.Web/src/app/shared/components/ui/`: `ButtonComponent`, `StatusBadgeComponent`, `SkeletonLoaderComponent`, `DataTableComponent` (sorting, pagination, typed cells), `ModalComponent` (backdrop blur, escape dismissal), `SlideOverComponent` (drawer panel), `CodeViewerComponent` (XSS-safe JSON formatter with word wrap & copy), `TabGroupComponent` & `TabComponent` (accessible keyboard navigation), `SearchFilterBarComponent` (debounced search, filter chips, refresh), `MetricCardComponent` (sparkline SVG gradient, trend percentage), and Toast Notification system (`ToastService`, `ToastContainerComponent`, `ToastComponent`).
    - Zero compile errors and clean barrel exports in `shared/components/index.ts`.
13. **FASE 12 — Executive Dashboard (Success Rate, Latency, DLQ, Realtime Metrics)** (`f82fa16`)
    - Backend time-series bucket aggregation in `GetDeliveryStatsUseCase` generating 24-hour hourly slots (total, success, failed, deadLettered, avgLatencyMs).
    - Angular frontend Executive Dashboard (`DashboardComponent`) featuring:
      - 4 KPI metric cards with dynamic SVG sparklines (Total Deliveries, Success Rate SLA 99.9%, Average Latency, DLQ count).
      - 24-Hour stacked SVG throughput bar chart with interactive status breakdown tooltips.
      - Active Endpoints summary widget.
      - Live SignalR Realtime Delivery Stream ticker with pulse indicator and event buffer.
      - Slide-Over Drawer for live event inspection (HTTP status, latency, request payload with `CodeViewerComponent`, and one-click replay).
14. **FASE 13 — Endpoint Management Portal & Secret Rotation UI** (`5a16ccb`)
    - Complete Endpoint Management portal (`EndpointsComponent`) under `/endpoints` with:
      - Search and status filter bar (`Active`, `Paused`, `Disabled`).
      - Endpoint registration modal with application selector, HTTPS target URL, rate limit, timeout, and wildcard event subscriptions (`order.*`, `invoice.paid`, `*`).
      - One-time HMAC signing secret modal (`whsec_...`) with one-click clipboard copy.
      - Zero-downtime dual-key secret rotation modal (`RotateSecretResponse`) displaying key history, version, and status (`Active`, `Rotating`, `Revoked`).
      - Endpoint edit modal (URL, rate limit, timeout, description), status toggling, and deletion modal with cascade confirmation.
      - Full integration with `EndpointService` and `ToastService`.
15. **FASE 14 — Live Event Inspector & Realtime Timeline** (`feat: add live event inspector` / `08a24a5`)
    - Full-screen Live Event Inspector and Webhook Delivery Timeline (`DeliveriesComponent`) accessible via `/deliveries`, `/live`, and `/events`.
    - Real-time SignalR live streaming engine with Pause / Resume controls, unread queued events counter, clear buffer, and fast filters (Status, Endpoint, Wildcard Event Type).
    - Historical Log & Explorer mode with multidimensional filtering (Correlation ID, Event Type, Target Endpoint, Status, Time Ranges from 15m to 7d) and server-side pagination.
    - Deep-Dive Slide-Over Inspector Drawer (`DeliveryInspectorDrawerComponent`) with:
      - Interactive vertical attempt execution timeline with status codes, latency badges, and error diagnostics.
      - Request inspector showing `X-HookBridge-Signature` HMAC-SHA256 headers, headers JSON, and formatted payload body with byte size.
      - Response inspector with HTTP status, latency, response headers, and response payload.
      - Copyable cURL CLI command generator for 1-click local debugging.
      - W3C TraceContext breakdown (`traceparent`, Trace ID, Parent Span ID).
      - One-click Webhook Replay and Alternate Destination Endpoint redirection modal.
    - Bulk Replay Modal (`BulkReplayModalComponent`) for batch re-enqueuing failed/dead-lettered deliveries with safety warnings.
    - Full test suite: 183/183 unit and integration tests passing; Angular frontend clean production build with 0 errors/warnings.
16. **FASE 15 — Trace Explorer (Event, Delivery, Trace, Log & Audit Correlation)** (`feat: add trace explorer`)
    - Backend Distributed Trace Correlation engine (`GetTracesUseCase`, `GetTraceDetailUseCase`, `TraceEndpoints`) at `GET /api/v1/traces` and `GET /api/v1/traces/{identifier}`.
    - End-to-end distributed span waterfall DAG synthesizing:
      1. Gateway Ingestion (`hookbridge.gateway.ingest`)
      2. EventFlow Transactional Outbox (`eventflow.transactional_outbox`)
      3. RabbitMQ Topic Exchange Transit (`rabbitmq.broker_publish`)
      4. Consumer Worker Idempotency & Dispatch (`eventflow.consumer_worker`)
      5. Outbound HTTP Dispatches & Attempts (`http.post {targetUrl}`)
      6. Audit Trail Ledger (`audit.ledger_record`)
    - Angular frontend Trace Explorer portal (`TraceExplorerComponent`) under `/traces` featuring:
      - Search by Trace ID, Correlation ID, Delivery ID, or Event Type with time-range filtering.
      - Master-detail view with live timing calculations, total spans counter, duration in milliseconds, and status badges.
      - Interactive waterfall DAG chart with proportional span timing bars, expandable span attributes, and sub-events.
      - Correlated Deliveries tab and Correlated Audit Trail tab.
      - Raw OpenTelemetry JSON export view with one-click clipboard copy.
    - Full test suite: 185/185 unit and integration tests passing (146 unit + 39 integration); Angular production build clean with 0 errors.

### Next Session Objective
- **FASE 16 — Payload Inspector & Highlighting**:
  - Advanced payload inspector with JSON path query evaluator, payload diff comparison between retry attempts, schema validator, and byte size analyzer.
  - Target commit: `feat: add payload inspector`.





