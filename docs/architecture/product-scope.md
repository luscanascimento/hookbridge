# HookBridge — Product Scope & Specifications (Fase 1.1)

> **Document Version:** 1.0.0  
> **Classification:** Product Engineering & Solution Architecture  
> **Status:** Approved

---

## 1. Executive Summary

**HookBridge** is a developer-centric **Webhook Gateway, Developer Portal, and Observability Platform**. It provides organizations with a unified Control Plane to manage outbound and inbound webhooks with the same level of Developer Experience (DX), security, and reliability found in world-class platforms like Stripe, Svix, and Hookdeck.

HookBridge operates above **EventFlow** (the distributed asynchronous processing engine and transactional outbox data plane).

---

## 2. Target Personas & Use Cases

### 2.1 Developer (API Consumer / Integrator)
- **Use Case:** Registering webhook endpoints, selecting subscribed event types, retrieving signing secrets, inspecting delivery payloads and headers in real-time, verifying HMAC signatures, and debugging failing attempts using the Live Event Inspector and Sandbox.

### 2.2 DevOps / SRE Engineer
- **Use Case:** Monitoring endpoint health, investigating delivery failures, analyzing P95/P99 latencies, inspecting distributed traces across PostgreSQL, RabbitMQ, and external endpoints, tracking error budgets, and executing bulk replays from DLQ without downtime.

### 2.3 Security Engineer & Platform Administrator
- **Use Case:** Managing tenant boundaries, issuing scoped API keys, enforcing zero-downtime webhook secret rotation, auditing administrative actions (endpoint deletions, replays, key revocations), and protecting internal networks against SSRF and replay attacks.

---

## 3. Functional Requirements

### 3.1 Multi-Tenant Organization & RBAC
- Explicit tenant onboarding with multi-user support.
- Role-Based Access Control (RBAC): `TenantAdmin`, `Developer`, `Viewer`, `SystemOperator`.
- Resource hierarchy: $\text{Tenant} \to \text{Users} \to \text{Applications} \to \text{Endpoints} \to \text{Subscriptions} \to \text{Deliveries} \to \text{Attempts}$.

### 3.2 Endpoint Management & Subscriptions
- Endpoint URL registration with automatic SSRF pre-validation.
- Event filtering and wildcard subscriptions (e.g. `payment.*`, `order.completed.v1`).
- Endpoint states: `Active`, `Paused`, `Disabled` (auto-disabled on consecutive critical failures).
- Rate limits and concurrency thresholds per endpoint.

### 3.3 Security, Signatures & Key Management
- Scoped API Keys with unique recognizable prefix (`hb_live_...`, `hb_test_...`) and SHA-256 cryptographic hashing at rest.
- Webhook signing secrets with HMAC-SHA256 (`X-HookBridge-Signature: t=...,v1=...`).
- Dual-secret rotation window allowing seamless migration without dropped events.
- Strict anti-replay timestamp validation window (tolerance: 300 seconds).

### 3.4 Delivery Orchestration & Realtime Telemetry
- Real-time event streaming into the web portal via ASP.NET Core SignalR.
- Live Event Inspector showing event payloads, HTTP status codes, latencies, and retry timelines.
- Interactive Delivery Timeline: $\text{Ingested} \to \text{Published} \to \text{Attempt \#1 (503)} \to \text{Retry Backoff} \to \text{Attempt \#2 (200)} \to \text{Delivered}$.
- Safe, authorized delivery replay generating fresh timestamps and signatures while maintaining parent event audit lineage.

### 3.5 Observability & Trace Explorer
- OpenTelemetry distributed tracing integrated across all layers.
- Trace Explorer correlating `TenantId`, `EventId`, `DeliveryId`, `AttemptId`, `TraceId`, and `SpanId`.
- Reliability metrics: Success rate, P50/P95/P99 latency, failure breakdown, and error budgets.

### 3.6 Developer Sandbox & Failure Simulator
- Ephemeral webhook receiver (`https://sandbox.hookbridge.local/receiver/{token}`) with auto-expiration (1 hour) and payload inspection.
- Controlled failure simulator generating HTTP 200, 400, 401, 429, 500, timeouts, and configurable failure rates (e.g. 30%) for integration testing without third-party dependencies.

---

## 4. Non-Functional Requirements (NFRs)

| Dimension | Target Specification | Enforcement Mechanism |
| :--- | :--- | :--- |
| **Tenant Isolation** | Zero cross-tenant data leakage | Global query filters, tenant authorization policies, AMQP routing partitioning |
| **API Latency (P95)** | $< 50\text{ ms}$ for ingestion, $< 100\text{ ms}$ for control plane APIs | Asynchronous I/O, EF Core compiled queries, PostgreSQL B-tree indexing |
| **Signature Verification** | Cryptographically constant time | `CryptographicOperations.FixedTimeEquals` |
| **Observability** | 100% distributed trace continuity | W3C TraceContext headers, OpenTelemetry ActivitySource |
| **Frontend Performance** | $< 1.5\text{ s}$ LCP, zero ZoneJS overhead | Angular 22 Zoneless, Signals, `@defer` views, modern control flow |
| **Resilience & Fault Tolerance** | Zero message loss on worker crash | RabbitMQ manual ACKs, transactional outbox in EventFlow, DLQ routing |

