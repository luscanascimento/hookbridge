# HOOKBRIDGE — MASTER PROJECT CONTEXT & ENGINEERING GOVERNANCE

> **Role & Persona:** Staff Software Engineer — Production Release Candidate Hardening  
> **Repository:** `git@github.com:luscanascimento/hookbridge.git`  
> **Real Stack:** Backend .NET 9 (`net9.0`, C# 13, EF Core / PostgreSQL), Frontend Angular 21 (Strict TS, Zoneless, PNPM), SignalR, EventFlow Data Plane  
> **Current Hardening Progress:** FASE 1 a FASE 5 Concluídas & Pushed (`ac6a131`) — FASE 6 (Logging, auditoria e observabilidade) Próxima  
> **Hardening Governance Rules:**
> 1. Preservar arquitetura Domain → Application → Infrastructure → API sem abstrações artificiais.
> 2. YAGNI, SOLID, DRY e KISS pragmáticos.
> 3. Sem reescritas amplas: pequenos incrementos revisáveis com explicação de problema, risco, solução, arquivos e validação.
> 4. Evidência obrigatória de testes/builds antes de declarar concluído.
> 5. Nunca expor secrets/senhas em logs, traces ou respostas HTTP.
> 6. Nunca remover testes existentes; adicionar novos testes para comportamento de segurança.
> 7. Isolamento de tenant estritamente fail-closed.
> 8. Cada fase é commitada e enviada via push mediante confirmação do usuário.

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
17. **FASE 16 — Payload Inspector & Highlighting** (`feat: add payload inspector`)
    - Backend JSON analysis, JSONPath evaluation, deep structural diffing, and schema validation engine under `HookBridge.Application/ControlPlane/UseCases/Payloads/` and `HookBridge.Api/Endpoints/PayloadEndpoints.cs`:
      - `POST /api/v1/payloads/analyze`: Returns raw/minified/formatted byte sizes, estimated Gzip compression ratio %, non-ASCII / Unicode multi-byte character detection, structure depth, key counts, data types breakdown, and inferred JSON Schema (Draft 2020-12).
      - `POST /api/v1/payloads/jsonpath`: Evaluates JSONPath queries (`$`, `$.prop`, `$.items[*]`, `$..prop`, `*`) returning matched paths, typed values, and match counts.
      - `POST /api/v1/payloads/diff`: Computes deep structural differences between two payloads, tracking Added, Removed, Modified, and Unchanged properties with byte size deltas.
      - `POST /api/v1/payloads/validate`: Validates JSON payloads against schema rules, type constraints, string formats (`uuid`, `date-time`, `uri`, `email`), required properties, and additional property guards.
    - Angular frontend Payload Explorer & Inspector Suite:
      - Interactive Collapsible JSON Tree Viewer (`JsonTreeViewerComponent`) with per-node copy path/value/subtree, item counters, expand/collapse all, and query search highlighting.
      - JSONPath Query Lab (`JsonPathEvaluatorComponent`) with live query execution, preset library (`$.id`, `$.event`, `$..email`, etc.), and jump-to-path navigation.
      - Structural Payload Diff Viewer (`PayloadDiffViewerComponent`) with color-coded diff table (+ Added, - Removed, ~ Modified), byte delta summary, and changes-only filter.
      - Payload Size & Structure Analyzer (`PayloadAnalyzerComponent`) with KPI cards, data type distribution matrix, inferred schema viewer, and interactive schema validator sandbox.
      - Dedicated `/payloads` standalone playground portal (`PayloadInspectorComponent`) with sample presets (Stripe, Shopify, GitHub, Linear, HookBridge) and delivery ID loader.
      - Enhanced `DeliveryInspectorDrawerComponent` with Tree/Code view toggle, attempt-to-attempt diff comparison tab, and deep-link navigation to the Payload Inspector.
    - Full test suite: 196/196 unit and integration tests passing (155 unit + 41 integration); Angular production build clean with 0 errors and 0 warnings.

18. **FASE 17 — Endpoint Health & Reliability Metrics** (`feat: add endpoint health metrics`)
    - Backend Health & Reliability Computation Engine (`GetEndpointHealthMetricsUseCase`, `GetTenantEndpointsHealthSummaryUseCase`) under `HookBridge.Application/ControlPlane/UseCases/Endpoints/` and `HookBridge.Api/Endpoints/EndpointEndpoints.cs`:
      - `GET /api/v1/endpoints/health`: Returns aggregated tenant-level reliability summary including overall health score (0–100%), overall 24h SLA uptime %, healthy/degraded/critical endpoint counts, total open circuit breakers count, active incident count, and per-endpoint health cards.
      - `GET /api/v1/endpoints/{id}/health`: Computes comprehensive endpoint health diagnostics with:
        - Health score calculation: $\text{clamp}\big((successRate \times 0.7) + (latencyScore \times 0.3) - \min(40, consecutiveFailures \times 10), 0, 100\big)$ with rating (`Healthy` $\ge 80$, `Degraded` $\ge 50$, `Critical` $< 50$).
        - 24h SLA Uptime percentage and success rate percentage.
        - Latency quantiles calculation: p50, p90, p95, p99, and average response time in milliseconds over the last 24h attempts.
        - Circuit breaker state determination: `Closed` (normal), `HalfOpen` ($3 \le consecutiveFailures < 5$), `Open` ($consecutiveFailures \ge 5$ or Endpoint status is `Disabled`).
        - Automated incident detection: generates actionable alerts with severity levels (`Info`, `Warning`, `Critical`) for elevated error rates, open circuit breakers, high p99 latency (> 2500ms), consecutive failure spikes, and endpoint disabled states.
    - Angular frontend Endpoint Health & Reliability Suite:
      - `HealthScoreGaugeComponent`: Circular SVG gauge with dynamic color themes (emerald, amber, rose) and animated score display.
      - `CircuitBreakerBadgeComponent`: Color-coded state badge with pulsing indicator dot and state description.
      - `LatencyQuantilesCardComponent`: Responsive quantiles grid (p50, p90, p95, p99, Avg) with micro-bar visualization and SLA threshold indicators.
      - `IncidentAlertsBannerComponent`: Actionable incident cards with severity indicators and remediation recommendations.
      - `EndpointHealthDrawerComponent`: Deep-dive slide-over drawer showing real-time reliability breakdown, latency quantiles, circuit breaker state, active incident banners, and quick links to correlated deliveries and traces.
      - Top KPI summary cards on `EndpointsComponent` (`/endpoints`) displaying overall health score, 24h SLA uptime, health status breakdown, and active circuit state counts.
    - Full test suite: 201/201 unit and integration tests passing (158 unit + 43 integration); Angular production build clean with 0 errors and 0 warnings.

19. **FASE 18 — Event Schemas, Versioning & Compatibility** (`feat: add event schema management`)
    - Backend Event Schema & Versioning Registry (`EventSchema`, `EventSchemaVersion`, `IHookBridgeDbContext`) under `HookBridge.Application/ControlPlane/UseCases/Schemas/` and `HookBridge.Api/Endpoints/EventSchemaEndpoints.cs`:
      - Schema Compatibility Engine (`ISchemaCompatibilityChecker` / `SchemaCompatibilityChecker`): Evaluates JSON Schema (Draft 2020-12) diffs enforcing `Backward`, `Forward`, `Full`, or `None` compatibility modes, detecting breaking changes (type shifts, missing required properties, removed enum values, unexpected additions).
      - Schema Documentation & Code Generator (`ISchemaCodeGenerator` / `SchemaCodeGenerator`): Generates TypeScript interfaces, C# record classes, Markdown specifications, and synthetic valid sample payload JSON from schemas.
      - Contract Drift Detection (`DetectSchemaDriftUseCase`): Audits recent deliveries against registered active schema, reporting total analyzed, conforming vs non-conforming counts, conformance rate %, and detected drift anomalies.
      - Complete REST API at `/api/v1/schemas`: List, Create, Get Details, Update, Delete, Create Version, Activate Version, Deprecate Version, Check Compatibility, Validate Payload, Detect Drift, and Generate Docs.
    - Angular frontend Event Schema Registry & Governance Portal:
      - Top KPI summary cards on `/schemas` (Total Schemas, Active Schemas, Total Versions, Strict Policies).
      - Fast search and filter chips by Compatibility Mode (`Backward`, `Forward`, `Full`, `None`) and Status (`Active`, `Draft`, `Deprecated`, `Archived`).
      - Schema Registration Modal (`SchemaCreateModalComponent`) with presets (`order.created`, `invoice.paid`, `user.registered`, `payment.disputed`, `custom`), JSON schema editor with live syntax check, and auto-formatting.
      - Schema Version Release Modal (`SchemaVersionModalComponent`) with real-time compatibility evaluator, breaking change warning banner, and force override controls.
      - Master Slide-Over Drawer with 4 interactive tabs: Version History & Evolution, Interactive Docs & SDK Typings, Contract Drift Detector with live audit, and Payload Validation Sandbox.
    - Full test suite: 219/219 unit and integration tests passing (174 unit + 45 integration); Angular production build clean with 0 errors and 0 warnings.

20. **FASE 19 — Developer Documentation & Code Snippets (cURL, TS, C#)** (`docs: add developer documentation`)
    - Backend Multi-Language Code Generation & Documentation Engine (`IDocSnippetGenerator` / `DocSnippetGenerator`, `GetApiReferenceUseCase`, `GenerateDocSnippetUseCase`, `DocEndpoints`) under `HookBridge.Application/ControlPlane/Services/`, `HookBridge.Application/ControlPlane/UseCases/Documentation/`, and `HookBridge.Api/Endpoints/DocEndpoints.cs`:
      - `GET /api/v1/docs/reference`: Returns complete structured API reference, architectural guides (Quickstart, Webhook Signing HMAC-SHA256, Anti-Replay Tolerance, Zero-Downtime Secret Rotation, Retries & Idempotency, RFC 7807 Error Handling), endpoint parameter definitions, and SDK integration recipes.
      - `POST /api/v1/docs/snippets`: Dynamically generates executable code snippets (cURL, TypeScript/Node.js, C# .NET 10, Python, Go, PHP) for any endpoint and payload.
      - `GET /api/v1/docs/sdk-recipes/{language}`: Returns dedicated SDK recipe with drop-in cryptographic verification functions and event publishing clients.
    - Angular frontend Developer Documentation & API Portal (`DocsComponent`) under `/docs`:
      - Interactive Sticky Navigation with category tree (Getting Started, Security & Signing, Reliability & Retries, Sandbox, SDK Recipes, REST API Endpoints with HTTP method badges).
      - Global multi-language switcher tabs (cURL, TypeScript / Node.js, C# / .NET 10, Python, Go, PHP) dynamically updating code blocks across all sections.
      - Interactive HMAC-SHA256 Signature Verification Sandbox with live payload presets, Unix timestamp calculation, clock skew validator, canonical string inspector, and online verification.
      - Multi-language SDK integration recipe hub with 1-click install command copy and verification function export.
      - Grouped REST API reference with parameter tables, header definitions, request/response JSON viewers, and deep links to relevant HookBridge portal tools (Payload Inspector, Live Inspector, Schema Registry).
    - Full test suite: 241/241 unit and integration tests passing (192 unit + 49 integration); Angular production build clean with 0 errors and 0 warnings.

21. **FASE 20 — Webhook Sandbox Receiver & Realtime Inspection** (`fa0c64a`)
    - Ephemeral webhook receiver engine (`WebhookSandbox`, `SandboxRequest`) with customizable default status codes, response delay, and response payload body.
    - Public webhook intake endpoint (`/api/v1/sandbox/receiver/{slug}`) allowing third-party services to deliver webhooks without auth.
    - Realtime SignalR notification stream (`ReceiveSandboxRequest`, `SandboxRequestsCleared`).
    - Angular frontend Sandbox portal (`SandboxComponent`) under `/sandbox` with live request inspection drawer and JSON payload viewer.
    - Full test suite: 252/252 unit and integration tests passing (199 unit + 53 integration); Angular production build clean.

22. **FASE 21 — Delivery Failure Simulator (200, 429, 500, Timeout, Chaos)** (`feat: add delivery failure simulator`)
    - Backend Chaos & Failure Simulation Engine (`SimulatorRule`, `SimulatorExecution`, `SimulatorStrategy` enum) under `HookBridge.Application/ControlPlane/UseCases/Simulator/` and `HookBridge.Api/Endpoints/SimulatorEndpoints.cs`:
      - Deterministic status responses (200, 400, 401, 403, 404, 422, 429, 500, 502, 503, 504).
      - Strategy evaluation:
        - `FixedStatus`: Deterministic return of target error or success code.
        - `FailureRate`: Configurable failure probability ($X\%$) for chaos and flakiness verification.
        - `SequentialRetryPattern`: Statefully fails the first $N$ attempts (e.g. 2 times 503) and then recovers with 200 OK to test retry exponential backoff policies. Includes dedicated step counter reset endpoint.
        - `Timeout`: Induces configurable latency delays (e.g. 5000ms) or returns HTTP 504 Gateway Timeout.
        - `ChaosJitter`: Injects random latency jitter and dynamically draws status codes from an error pool (429, 500, 502, 503, 504).
        - `MalformedJson`: Returns invalid/corrupted JSON payloads to test client deserialization error handling.
      - Automated rate limiting headers: Injects `Retry-After: 30`, `X-RateLimit-Limit`, and `X-RateLimit-Remaining: 0` for 429 responses.
      - Public and ad-hoc receiver endpoints (`/api/v1/simulator/receive/{slug}`, `/api/v1/simulator/http/{code}`, `/api/v1/simulator/timeout`, `/api/v1/simulator/chaos`).
      - Realtime SignalR streaming (`ISimulatorRealtimeNotifier`, `ReceiveSimulatorExecution`, `SimulatorExecutionsCleared`).
    - Angular frontend Failure Simulator & Chaos Lab (`SimulatorComponent`) under `/simulator`:
      - Quick Presets Toolbar with 1-click test URL generator and copy buttons.
      - KPI metric cards (Total Invocations, Failure Rate %, Average Latency ms, Active Rules).
      - Chaos Rule Designer with live sliders, presets, and response header/body JSON editors.
      - Live Executions Table with SignalR real-time stream, pause/resume, search, and deep-dive SlideOver inspector.
      - Interactive Test Dispatch Workbench allowing developers to send instant webhooks and observe live execution duration, status codes, and injected faults.
    - Full test suite: 268/268 unit and integration tests passing (209 unit + 59 integration); Angular production build clean with 0 errors and 0 warnings.

23. **FASE 22 — OpenTelemetry Observability (Traces, Metrics, Logs, Jaeger)** (`78e3b57`)
    - Backend OpenTelemetry Observability Engine (`HookBridgeDiagnostics`, `InMemoryTelemetryBuffer`, `PrometheusMetricsFormatter`, `TraceContextEnricherMiddleware`) under `HookBridge.Application/ControlPlane/UseCases/Observability/` and `HookBridge.Api/Endpoints/ObservabilityEndpoints.cs`:
      - 16 registered OpenTelemetry metric instruments (`Meter`, `ActivitySource`, `Counters`, `Histogram`, `UpDownCounters`).
      - In-memory thread-safe ring buffer processor (`InMemoryTelemetryBuffer`) capturing up to 200 distributed spans without requiring external infrastructure.
      - Public Prometheus scrape endpoint (`GET /metrics`) exposing CLR process memory, GC collections, and domain metrics in Prometheus 0.0.4 text format.
      - Programmatic synthetic trace pipeline generator (`POST /api/v1/observability/synthetic-trace`) simulating end-to-end ingestion, HMAC signing, PostgreSQL outbox persistence, and HTTP dispatch.
      - Trace context propagation middleware (`TraceContextEnricherMiddleware`) extracting W3C `traceparent` and attaching `X-Trace-Id` / `X-Correlation-Id` headers.
    - Angular frontend Observability Portal (`ObservabilityComponent`) under `/observability`:
      - 4 KPI metric cards (Working Set MB, Managed Heap MB, GC Collections, Buffered Spans).
      - Tab 1 (Metric Instruments): Filterable table of all 16 metrics + live Prometheus exposition viewer.
      - Tab 2 (Captured Spans Buffer): Realtime list of spans with semantic tags and W3C baggage inspection.
      - Tab 3 (Synthetic Pipeline Tester): Interactive trace generator with instant distributed waterfall DAG visualization.
      - Tab 4 (Collector & Specs): OTLP runtime diagnostics and `prometheus.yml` configuration exporter.
    - Full test suite: 280/280 tests passing; Angular production build clean.

24. **FASE 23 — Adversarial Security Hardening (IDOR, SSRF, XSS, Replay)** (`d6f144d`)
    - Comprehensive SSRF Guard hardening (`SsrfGuard.cs`):
      - Protection against loopback, RFC 1918 private subnets, carrier-grade NAT, test nets, and multicast.
      - Cloud metadata IP protection (AWS `169.254.169.254` & `fd00:ec2::254`, GCP `metadata.google.internal`, Alibaba `100.100.100.200`, Oracle `192.0.0.192`, AWS ECS `169.254.170.2`).
      - Container & cluster hostname blocks (`host.docker.internal`, `gateway.docker.internal`, `*.cluster.local`, `*.internal`, `*.local`, `*.localhost`).
      - Dangerous infrastructure port blocking (FTP 21, SSH 22, SMTP 25, MySQL 3306, Postgres 5432, Redis 6379, MongoDB 27017, K8s 6443, Elasticsearch 9200, Memcached 11211).
      - Embedded userinfo/credentials in URLs disallowed.
    - Webhook HMAC-SHA256 & Anti-Replay hardening (`WebhookSigner.cs`):
      - Constant-time signature verification (`CryptographicOperations.FixedTimeEquals`).
      - Strict timestamp tolerance (default 5 minutes) and future drift mitigation (max 60s future clock skew).
      - Seamless dual-secret rotation grace period verification.
    - Defensive Security Headers (`SecurityHeadersMiddleware.cs`):
      - OWASP-compliant headers (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `X-XSS-Protection: 0`, `Referrer-Policy: strict-origin-when-cross-origin`, `Content-Security-Policy`, `Permissions-Policy`, `Cross-Origin-Opener-Policy`, `Cross-Origin-Resource-Policy`, `HSTS`).
    - Input Sanitization & XSS Neutralization (`InputSanitizer.cs`):
      - Automated XSS vector detection (`<script>`, `javascript:`, `onerror=`, `onload=`, `<iframe>`, `<object>`) and URL-safe slug validation.
    - Cross-Tenant IDOR validation across all queries, mutations, and domain models.
    - Full test suite: 346/346 unit and integration tests passing (277 unit + 69 integration); Angular production build clean.

25. **FASE 24 — Comprehensive Automated Test Suite (Unit, Integration, E2E)** (`test: expand automated test coverage`)
    - Comprehensive End-to-End Pipeline Integration Suite (`EndToEndWebhookLifecycleTests.cs`):
      - Complete multi-tenant lifecycle from registration, authentication, API Key issuance (`hb_live_...`), application and endpoint setup with wildcard subscriptions (`order.*`, `*`), zero-downtime secret rotation (`RotateSecretResponse`), schema creation and versioning, event publishing via API Key, dual delivery scheduling, historical attempt recording with exponential backoff status transitions, delivery details inspection, single replay with lineage chain validation, aggregated statistics, health scores, and full audit trail ledger verification.
    - Concurrency & Race Condition Suite (`ConcurrentOperationsTests.cs`):
      - Thread-safe delivery attempt recording across multiple deliveries simultaneously.
      - High-throughput parallel event publishing with multi-endpoint routing.
      - Concurrent bulk delivery replay execution.
    - Adversarial Boundary & Security Hardening Integration Suite (`AdversarialBoundaryIntegrationTests.cs`):
      - Deeply nested JSON payloads (20+ levels) with multi-byte Unicode emoji glyphs and surrogate pairs analyzed and validated.
      - Malicious JSONPath injection vectors (path traversal, script tags, SQL-like injection strings).
      - Cross-tenant IDOR protection across replay, lineage inspection, attempt recording, and endpoint health diagnostics.
      - Token tampering, invalid signatures, and malformed header rejection.
      - Rapid-fire failure simulator fault injection with automatic `Retry-After` rate limit headers.
    - Boundary & Edge Case Unit Test Suite (`EdgeCaseAndBoundaryUnitTests.cs` & `ObservabilityEdgeCaseTests.cs`):
      - Subscription pattern matching boundary conditions (universal wildcard `*`, prefix wildcards `order.*`, case-insensitivity, special character handling).
      - HMAC-SHA256 signature verification edge cases (exact 300s tolerance limit, 301s expiration, +45s allowed clock drift, +65s rejected drift, dual-secret header verification).
      - Schema compatibility checker boundary tests across Full, Backward, None modes.
      - Schema code generator with C# and TypeScript reserved keywords (`class`, `event`, `namespace`, `interface`).
      - Deeply nested structural payload diffing and JSONPath evaluations.
      - Endpoint health score clamp calculations, 3-consecutive-failure `HalfOpen` and 5-consecutive-failure `Open` circuit breaker transitions.
      - Simulator sequential retry step counter rollover and reset.
      - `InMemoryTelemetryBuffer` 200-item ring buffer eviction policy and thread-safe parallel span recording.
    - Full test suite: 394/394 unit and integration tests passing (312 unit + 82 integration); Angular production build and typecheck clean with 0 errors.

26. **FASE 25 — Distributed Chaos & Failure Testing (Broker/DB Outages)** (`test: add distributed failure scenarios`)
    - Distributed Chaos & Failure Integration Suite (`DistributedFailureIntegrationTests.cs`):
      - Ingestion failure resilience: verified `POST /api/v1/events` handles EventFlow / broker outages with structured RFC 7807 ProblemDetails (`EventFlow.ConnectionError`) and zero database state corruption, followed by seamless recovery when the broker is restored.
      - Replay under outage: verified single replay and bulk replay gracefully reject or report zero replayed deliveries when the broker is down, without corrupting delivery status or parent lineage chains.
      - Dead-Letter Queue failure modes: verified `/api/v1/dlq` Peek, Replay, and Purge endpoints return 500 ProblemDetails during broker DLQ unavailability and recover cleanly upon broker reconnection.
      - Flaky endpoint simulation with `SimulatorStrategy.SequentialRetryPattern`: verified that transient 503 errors across retries correctly increment attempt numbers and record execution timelines before transitioning status to `Success` upon eventual recovery.
      - Circuit breaker degradation and recovery lifecycle: verified transitions from `Closed` (Healthy) to `HalfOpen` (3 consecutive failures) to `Open` (5 consecutive failures with Critical incident generation) and subsequent recovery back to `Closed` upon consecutive successful attempts.
      - High-concurrency chaos fault injection: verified concurrent invocations of rate limit throttling (429 with `Retry-After`), simulated network timeouts (504), and chaos jitter (502/500) execute without race conditions or deadlocks.
    - Chaos & Broker Outage Unit Test Suite (`BrokerAndDbOutageChaosTests.cs`):
      - `EventFlowClient` network failure unit tests: verified handling of `SocketException` (connection refused), `TaskCanceledException` / timeout distinguishing caller cancellation from HttpClient timeout, and DLQ operations during broker outage.
      - `PublishEventUseCase`, `ReplayDeliveryUseCase`, and `BulkReplayDeliveriesUseCase` transactional integrity: ensured delivery entities are only added and persisted to EF Core `DbContext` after EventFlow ingestion succeeds.
      - `GetEndpointHealthMetricsUseCase` extreme failure diagnostics: verified health score degradation (<30), p99 latency calculations, and Critical incident generation under consecutive failure bursts.
    - Full test suite: 407/407 unit and integration tests passing (319 unit + 88 integration); Angular production build clean with 0 errors.

27. **FASE 26 — Performance & Bottleneck Profiling** (`perf: optimize measured bottlenecks`)
    - Profiling & Zero-Allocation Optimization Suite (`PerformanceProfilingUnitTests.cs`):
      - Webhook HMAC-SHA256 signature generator optimized with thread-safe cached key byte arrays and zero-copy spans.
      - Granular subscription wildcard pattern matching optimized with cached regex instances and fast-path exact match bypass.
      - JSON structural diffing and JSONPath evaluation optimized in `AnalyzePayloadUseCase`.
      - Relational database indexing applied to `Deliveries` (`TenantId, Status, CreatedAtUtc`), `Attempts` (`DeliveryId, AttemptNumber`), `AuditEntries` (`TenantId, CreatedAtUtc`), and `Endpoints` (`TenantId, IsActive`).
    - Full test suite: 419/419 unit and integration tests passing.

28. **FASE 27 — Docker & Docker Compose Multi-Service Environment** (`chore: containerize hookbridge`)
    - Multi-stage container builds:
      - Backend `HookBridge.Api/Dockerfile` using `mcr.microsoft.com/dotnet/aspnet:9.0` with non-root security context.
      - Frontend `HookBridge.Web/Dockerfile` with multi-stage Node 22 build and Alpine Nginx reverse proxy with security headers, gzip, and SPA routing.
    - Compose configurations:
      - `docker-compose.yml` for local development.
      - `docker-compose.prod.yml` for production deployments with resource limits and healthcheck definitions.
    - Automated configuration verification suite (`DockerConfigurationTests.cs`).

29. **FASE 28 — CI/CD Pipeline (GitHub Actions, Analyzers, Security Audits)** (`ci: add build test and security pipeline`)
    - GitHub Actions CI/CD workflows:
      - `.github/workflows/ci.yml`: Full automated build, test, and typecheck matrix.
      - `.github/workflows/release.yml`: Release candidate and semantic versioning automation.
      - `.github/workflows/security-audit.yml`: Dependency vulnerability scanner and secret audits.
    - Root `.editorconfig` enforcing strict C# 13 and .NET 10 code style analyzers.
    - Automated CI/CD workflow verification suite (`CiCdWorkflowTests.cs`).

30. **FASE 29 — Final Project Documentation & Architecture Blueprint** (`docs: finalize project documentation`)
    - Comprehensive `README.md` documentation with architectural diagrams, quick start guides, configuration matrix, and developer portal tour.

31. **FASE 30 — Multi-Role Engineering Review (Staff, Security, SRE, Product)** (`refactor: finalize engineering review`)
    - Multi-role review document (`docs/reviews/multi-role-engineering-review.md`) evaluating HookBridge against Staff, Security, SRE, and Product standards.
    - Engineering review compliance test suite (`MultiRoleEngineeringReviewTests.cs`).

32. **FASE 31 — Release Candidate & Production Verification** (`chore: prepare release candidate`)
    - Release candidate versioning metadata configured across `Directory.Build.props`, `CHANGELOG.md`, `RELEASE_NOTES.md`, and frontend `package.json`.
    - Release Candidate verification test suite (`ReleaseCandidateVerificationTests.cs`).
    - Complete test suite: 429/429 automated tests passing (341 unit + 88 integration).

### Final Status (Initial Roadmap)
- **All 32 Initial Phases (0 through 31) Completed and Verified.**

---

## 6. Production Release Candidate Hardening (Current Campaign)

### RC Hardening Overview & Governance
A rigorous 12-phase hardening campaign preparing the repository for a resilient, production-ready Release Candidate:

| Phase | Description | Status | Commit |
| :--- | :--- | :--- | :--- |
| **FASE 1** | Diagnóstico e baseline (stack alignment, vulnerability audit) | ✅ Concluída | Baseline Audit |
| **FASE 2** | Configuração e secrets (fail-fast options, sanitized configs, no plaintext defaults) | ✅ Concluída & Pushed | `1922bc9` |
| **FASE 3** | Banco de dados e persistência (versioned EF migrations, indexes, idempotent DDL) | ✅ Concluída & Pushed | `cfae65a` |
| **FASE 4** | Multi-tenancy e autorização (fail-closed query filter, zero header trust, IDOR defense) | ✅ Concluída & Pushed | `7deb6ad` |
| **FASE 5** | Tratamento de erros e validação (RFC 7807, zero stack trace leak, FluentValidation) | ✅ Concluída & Pushed | `ac6a131` |
| **FASE 6** | Logging, auditoria e observabilidade (structured logging, PII sanitization, OTel) | ⏳ Próxima | - |
| **FASE 7** | Resiliência e chamadas externas (Polly v8, timeout, circuit breaker, SSRF defense) | ⏳ Planejada | - |
| **FASE 8** | Background processing e consistência (Transactional Outbox, DLQ replay) | ⏳ Planejada | - |
| **FASE 9** | API e contratos externos (OpenAPI 3.1, pagination limits, idempotency keys) | ⏳ Planejada | - |
| **FASE 10** | Frontend e developer experience (Angular 21 strict, reactive error handling, UX) | ⏳ Planejada | - |
| **FASE 11** | Testes e validação de ponta a ponta (Cross-tenant security, race conditions, chaos) | ⏳ Planejada | - |
| **FASE 12** | Operabilidade, CI/CD e governança de release (Docker multi-stage, CI matrix, RC check) | ⏳ Planejada | - |

### Detailed Log: FASE 4 — Multi-Tenancy e Autorização
1. **Global Query Filter Estritamente Fail-Closed:**
   - Atualizado `ApplyTenantFilter` em `HookBridgeDbContext.cs` para `HasTenantFilter && e.TenantId == CurrentTenantId`.
   - Qualquer consulta não autenticada ou sem contexto de tenant retorna estritamente 0 registros por padrão (fail-closed).
   - Consultas de sistema e scraping de métricas Prometheus utilizam explicitamente `.IgnoreQueryFilters()`.
2. **Zero Header Trust:**
   - Removida a confiança cega em cabeçalho `X-Tenant-ID` não autenticado em `TenantResolutionMiddleware.cs`.
   - A identidade do tenant é estabelecida exclusivamente a partir de claims criptograficamente validadas do JWT (`tenant_id`, `tid`, `tenant_slug`).
3. **SignalR Tenant Isolation & RBAC Enforcement:**
   - Hardening em `DeliveryHub.cs` para validação de claims e autorização de inscrição de endpoints e aplicações com fail-closed defensivo.
   - Verificada matriz de autorização RBAC (`RequireTenantAdmin`, `RequireDeveloper`, `RequireViewer`, `RequireSystemOperator`).
4. **Testes Automatizados:**
   - Criados `tests/HookBridge.UnitTests/Security/MultiTenancyFailClosedTests.cs` e `tests/HookBridge.IntegrationTests/Security/RbacAndTenantAuthorizationTests.cs`.
   - 449 testes automatizados passando (357 UnitTests + 92 IntegrationTests).

### Detailed Log: FASE 5 — Tratamento de Erros e Validação
1. **Padronização RFC 7807 ProblemDetails:**
   - Atualizado `HttpResults.cs` para emitir cabeçalho `Content-Type: application/problem+json`, URI `type: "https://tools.ietf.org/html/rfc7807#section-3.1"`, `errorCode` canônico e dicionário de erros de validação por propriedade (`errors`).
   - Unificados `AuthEndpoints.cs` e `DocEndpoints.cs` para utilizar a estrutura canônica de `ProblemDetails`.
2. **Integração FluentValidation Aprimorada:**
   - Criada extensão `ValidationResultExtensions.ToDomainError()` em `HookBridge.Application.Common` agrupando falhas por nome de propriedade e preservando retrocompatibilidade total de códigos e mensagens.
   - Atualizados 11 use cases do Control Plane e Auth para emissão estruturada de erros de validação.
3. **Prevenção de Vazamento de Dados Sensíveis:**
   - Hardening em `GlobalExceptionHandler.cs` assegurando que stack traces e mensagens internas de exceção nunca sejam divulgadas em ambiente de Produção, mantendo apenas `traceId` e timestamp correlacionados.
4. **Testes Automatizados:**
   - Criados `tests/HookBridge.UnitTests/Errors/ProblemDetailsAndValidationTests.cs` e `tests/HookBridge.IntegrationTests/Errors/ErrorHandlingAndProblemDetailsIntegrationTests.cs`.
   - 461 testes automatizados passando (365 UnitTests + 96 IntegrationTests).





