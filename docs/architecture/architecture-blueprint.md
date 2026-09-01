# HookBridge — Architecture Blueprint & System Design (Fase 1.3)

> **Document Version:** 1.0.0  
> **Classification:** System Architecture & Technical Design  
> **Status:** Approved

---

## 1. System Context & C4 Architecture

### 1.1 C4 Level 1: System Context Diagram

```
                 +-----------------------------------------------+
                 |              DEVELOPER / USER                 |
                 +-----------------------+-----------------------+
                                         |
                                         | HTTPS (UI & SignalR)
                                         v
+----------------------------------------+----------------------------------------+
|                               HOOKBRIDGE PLATFORM                               |
|                                                                                 |
|  +---------------------------------------------------------------------------+  |
|  |             Developer Portal & Observability (Angular 22 SPA)             |  |
|  +-------------------------------------+-------------------------------------+  |
|                                        |                                        |
|                                        | REST API / SignalR Hub                 |
|                                        v                                        |
|  +---------------------------------------------------------------------------+  |
|  |                 HookBridge Control Plane (.NET 10 API)                    |  |
|  +-------------------+--------------------+-------------------+--------------+  |
+----------------------|--------------------|-------------------|-----------------+
                       |                    |                   |
                       | SQL Queries        | AMQP Publish      | HTTP / Ingestion
                       v                    v                   v
             +---------+---------+  +-------+-------+   +-------+-------+
             |    PostgreSQL     |  |   RabbitMQ    |   |   EventFlow   |
             |   Control Store   |  | Event Broker  |   |  Data Plane   |
             +-------------------+  +---------------+   +-------+-------+
                                                                |
                                                                | HTTP Dispatch (Polly v8 + SSRF)
                                                                v
                                                        +---------------+-------+
                                                        |   External Customer   |
                                                        |   Webhook Receivers   |
                                                        +-----------------------+
```

---

## 2. Backend Solution Architecture (.NET 10)

HookBridge follows Clean Architecture with strict dependency directions:

```
src/
├── HookBridge.Domain/            <-- Pure C# (Entities, Value Objects, Domain Errors, Invariants)
├── HookBridge.Application/       <-- Use Cases, Commands, Queries, Interfaces, FluentValidation
├── HookBridge.Infrastructure/    <-- EF Core, PostgreSQL, SignalR Hubs, Key Encryption, Security
└── HookBridge.Api/               <-- Minimal APIs, Auth Middleware, ProblemDetails, OpenAPI 3.1

tests/
├── HookBridge.UnitTests/         <-- Domain logic, Validators, HMAC calculations, Anti-replay
└── HookBridge.IntegrationTests/  <-- Testcontainers (PostgreSQL, Redis, RabbitMQ), API Security
```

### Dependency Rule
$$\text{Api} \longrightarrow \text{Infrastructure} \longrightarrow \text{Application} \longrightarrow \text{Domain}$$

No outer layer concerns (such as EF Core DbContext, RabbitMQ Client, or HTTP Request objects) are allowed inside `HookBridge.Domain` or `HookBridge.Application`.

---

## 3. Frontend Architecture (Angular 22 Zoneless)

The web client is structured using a feature-based architecture utilizing Angular 22 signals and standalone components:

```
src/app/
├── core/                         <-- Singleton infrastructure (Auth, Interceptors, SignalR, Telemetry)
│   ├── auth/                     <-- JWT storage, refresh tokens, role guards
│   ├── http/                     <-- Functional interceptors (Auth header, ProblemDetails handler)
│   ├── signalr/                  <-- Live event SignalR service with Signal-based state
│   └── telemetry/               <-- Client-side trace correlation
│
├── features/                     <-- Isolated feature modules
│   ├── dashboard/                <-- Executive overview, success rate, latency graphs
│   ├── endpoints/                <-- Endpoint registration, secret rotation, subscription rules
│   ├── deliveries/               <-- Live delivery inspector, retry timelines, payload viewer
│   ├── replay/                   <-- Replay execution center, batch replays
│   ├── trace-explorer/           <-- End-to-end distributed trace visualizer
│   ├── sandbox/                  <-- Ephemeral webhook receiver
│   ├── simulator/                <-- Controlled failure simulator
│   ├── api-keys/                 <-- Key management and permission scopes
│   └── audit/                    <-- Compliance and audit log viewer
│
└── shared/                       <-- Reusable UI library
    ├── ui/                       <-- Buttons, Badges, Modals, Tables, Skeletons, Code Highlighters
    ├── forms/                    <-- Form controls, input validators
    └── utilities/                <-- Date formatters, bytes formatters, payload masks
```

---

## 4. Key Architectural Decisions Summary

1. **Zoneless Angular 22:** Pure Signal reactivity removes `zone.js` runtime overhead, optimizing change detection and rendering speed.
2. **PostgreSQL Multi-Tenant Sharding/Partitioning:** Enforced via tenant IDs on all tables and EF Core global query filters.
3. **OpenTelemetry by Design:** Every request and event publishes trace spans to OpenTelemetry collectors for instant root-cause analysis.

