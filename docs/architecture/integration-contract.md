# HookBridge & EventFlow — Integration Contract & Boundary Specification

> **Document Version:** 1.0.0  
> **Status:** Approved & Implemented  
> **Target Architecture:** HookBridge (Control Plane) $\longleftrightarrow$ EventFlow (Data Plane)  
> **Context Location:** `docs/architecture/integration-contract.md`

---

## 1. Architectural Boundary & Responsibilities (Fase 0.1)

HookBridge and EventFlow form a decoupled, enterprise webhook architecture where concerns are strictly separated between the **Control Plane** and the **Data Plane**:

```
+-----------------------------------------------------------------------------------+
|                            HOOKBRIDGE (CONTROL PLANE)                             |
|                                                                                   |
|  - Multi-Tenant Lifecycle (Tenants, Users, RBAC, API Keys)                        |
|  - Endpoint Registry & Event Subscriptions                                        |
|  - Webhook Secrets & Cryptographic Rotation                                       |
|  - HMAC Signature Generation & Anti-Replay Policies                                |
|  - Delivery Management, Replay Triggers & Audit Trail                             |
|  - Realtime Telemetry Hub (SignalR) & Live Event Inspector                        |
|  - Trace Explorer, Incident Investigation & Endpoint Health Metrics               |
|  - Webhook Sandbox Receiver & Failure Simulator                                   |
|  - Developer Portal & Interactive OpenAPI Documentation                           |
+-----------------------------------------+-----------------------------------------+
                                          |
                         Integration & Tracing Contract
                         (W3C TraceContext + AMQP / HTTP API)
                                          |
                                          v
+-----------------------------------------------------------------------------------+
|                             EVENTFLOW (DATA PLANE)                                |
|                                                                                   |
|  - Transactional Outbox (PostgreSQL ACID boundary)                                |
|  - Distributed Message Broker (RabbitMQ Topic Exchange & Topology)                |
|  - Scalable Asynchronous Consumer Workers (Prefetch QoS backpressure)             |
|  - Distributed Idempotency Ledger (Redis In-flight Lock + PostgreSQL Ledger)      |
|  - Outbound HTTP Resilience (Polly v8: Backoff, Jitter, Circuit Breaker, Timeout) |
|  - Server-Side Request Forgery Defense (SsrfGuard & Cloud Metadata Blocking)      |
|  - Dead Letter Queue Management (DLX / DLQ Peek, Safe Replay, Purge)              |
|  - OpenTelemetry Instrumentation (ActivitySource, Meters & Distributed Spans)     |
+-----------------------------------------------------------------------------------+
```

### 1.1 Invariant Rules
1. **No Duplicate Infrastructure:** HookBridge does not reimplement the Transactional Outbox, RabbitMQ connection topology, or Polly resilience pipelines already provided by EventFlow.
2. **Explicit Authority:** HookBridge acts as the single source of truth for authorization, tenant ownership, endpoint metadata, and signature secrets. EventFlow acts as the resilient transport and delivery engine.
3. **Zero Trust Integration:** All communication between Control Plane and Data Plane carries explicit tenant headers and W3C TraceContext propagation.

---

## 2. EventFlow APIs, Topology & Schemas (Fase 0.2)

Based on direct source code analysis of the EventFlow engine, the following interfaces and contracts are active:

### 2.1 HTTP Ingestion API (`EventEndpoints.cs`)

* **Endpoint:** `POST /api/v1/events`
* **Authentication:** Header `X-Api-Key: <configured-key>`
* **Rate Limiting:** `events-rate-limit` policy active

#### Ingestion Request Schema (`IngestEventCommand`)
```json
{
  "eventId": "01918a22-4a7b-7212-8e2b-7c3e1e9f1a01",
  "eventType": "payment.settled.v1",
  "version": 1,
  "source": "hookbridge-control-plane",
  "occurredAt": "2026-09-01T14:30:00.000Z",
  "correlationId": "corr_8923719823",
  "traceParent": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "tenantId": "tenant_enterprise_01",
  "idempotencyKey": "pay_tx_99182371",
  "payload": {
    "orderId": "ord_99812",
    "amount": 250.75,
    "currency": "BRL",
    "status": "SETTLED"
  },
  "metadata": {
    "environment": "production",
    "region": "sa-east-1"
  }
}
```

#### Ingestion Response Schema (`IngestEventResponse`)
```json
// Status: 202 Accepted
// Header: Location: /api/v1/events/01918a22-4a7b-7212-8e2b-7c3e1e9f1a01
{
  "eventId": "01918a22-4a7b-7212-8e2b-7c3e1e9f1a01",
  "status": "Accepted",
  "ingestedAt": "2026-09-01T14:30:00.125Z"
}
```

#### Ingestion Error Statuses (RFC 7807 `ProblemDetails`)
- `400 Bad Request` — Validation failures (empty tenant, invalid GUID, invalid event type format, empty payload).
- `409 Conflict` — Duplicate `idempotencyKey` for the specified `tenantId`.
- `500 Internal Server Error` — Transactional Outbox persistence failure.

---

### 2.2 DLQ Management APIs (`DeadLetterEndpoints.cs`)

* **Peek DLQ:** `GET /api/v1/dlq?count=10`
  * Reads unacknowledged dead-letter messages without removing them (`BasicNack` with `requeue: true`).
* **Replay DLQ:** `POST /api/v1/dlq/replay?maxCount=50`
  * Re-publishes dead-letter messages back to the primary topic exchange (`eventflow.events`) with `x-replayed-at` header and acknowledges them from DLQ.
* **Purge DLQ:** `DELETE /api/v1/dlq`
  * Purges all messages from `eventflow.events.dlq`.

---

### 2.3 AMQP Topology (`RabbitMqConsumer.cs` & `RabbitMqOptions.cs`)

| Entity | Name | Configuration / Binding |
| :--- | :--- | :--- |
| **Topic Exchange** | `eventflow.events` | `durable: true, type: topic` |
| **Processing Queue**| `eventflow.events.queue` | `durable: true`, `x-dead-letter-exchange: eventflow.dlx` |
| **Dead Letter Exchange**| `eventflow.dlx` | `durable: true, type: topic` |
| **Dead Letter Queue**| `eventflow.events.dlq` | `durable: true`, bound to `eventflow.dlx` with routing `#` |
| **QoS Prefetch** | `prefetchCount: 10` | Backpressure control on worker channels |

#### Mandatory AMQP Message Headers
- `tenant_id`: Partitioning tenant string.
- `event_type`: Routing event name (e.g. `order.created.v1`).
- `traceparent`: W3C distributed trace string (`00-traceId-spanId-flags`).
- `correlation_id`: Transversal correlation identifier.
- `outbox_id`: Originating outbox message UUID.
- `idempotency_key`: Client idempotency key.

---

### 2.4 Resilience & Security Pipelines (`HttpExternalDispatcher.cs` & `SsrfGuard.cs`)

1. **SSRF Guard Invariants:**
   - Schemes permitted: `http://` and `https://` only.
   - Prohibited targets: `localhost`, loopback (`127.0.0.0/8`, `::1`), RFC 1918 private subnets (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`), Link-Local (`169.254.0.0/16`), Cloud Metadata (`169.254.169.254`), Multicast (`224.0.0.0/4`), IPv6 link/site local.
2. **Polly v8 Resilience Pipeline:**
   - Total Timeout: 10s.
   - Attempt Timeout: 5s per request.
   - Exponential Backoff with Jitter: 3 retries (500ms base, factor 2.0).
   - Circuit Breaker: 50% failure rate over 30s sampling window, minimum 5 requests, 30s break duration.

---

## 3. HookBridge $\longleftrightarrow$ EventFlow Integration Contract (Fase 0.3)

### 3.1 Webhook Delivery Pipeline Flow

```
[HookBridge Control Plane]
       │
       ▼ (1. Ingest Event via POST /api/v1/events or AMQP eventflow.events)
[EventFlow Ingestion / Outbox]
       │
       ▼ (2. OutboxDispatcherWorker publishes to RabbitMQ)
[Topic Exchange: eventflow.events]
       │
       ▼ (3. EventConsumerWorker consumes with Prefetch QoS)
[HookBridge Delivery Worker / Dispatcher Handler]
       │
       ├─► (4. Lookup Subscriptions for Tenant & EventType)
       ├─► (5. Fetch Endpoint Secrets & Generate HMAC-SHA256 Signature)
       ├─► (6. Execute HttpExternalDispatcher via Polly v8 + SsrfGuard)
       │
       ▼
[External Webhook Receiver] (HTTP 200 OK or 4xx/5xx Failure)
       │
       ▼ (7. Record DeliveryAttempt & Broadcast State via SignalR)
[HookBridge Realtime Delivery Hub]
```

### 3.2 Webhook Signature Specification (HMAC-SHA256)

When dispatching webhooks to customer endpoints, HookBridge enforces the standard signature headers:

```http
POST https://customer.api.com/webhooks HTTP/1.1
Host: customer.api.com
Content-Type: application/json; charset=utf-8
X-HookBridge-Event: payment.settled.v1
X-HookBridge-Delivery: 01918a30-9b8c-7324-9f3c-8d4e2f0a2b02
X-HookBridge-Timestamp: 1788273000
X-HookBridge-Signature: t=1788273000,v1=5d41402abc4b2a76b9719d911017c592
traceparent: 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01
```

#### Canonical Signature Computation
$$\text{SignedPayload} = \text{Timestamp} + \text{"."} + \text{RawJsonPayload}$$
$$\text{Signature} = \text{HMAC-SHA256}(\text{SignedPayload}, \text{SecretKey})$$

* **Constant-time Comparison:** Verification must use `CryptographicOperations.FixedTimeEquals` to prevent timing attacks.
* **Timestamp Tolerance Window:** 300 seconds (5 minutes) maximum drift allowed to block replay attacks.
* **Secret Rotation Support:** Header may contain multiple signatures during rotation: `t=...,v1=<new_sig>,v1=<prev_sig>`.

---

## 4. Distributed Tracing, IDs & Correlation Schema (Fase 0.4)

To guarantee seamless correlation across the entire lifecycle (Browser $\to$ HookBridge API $\to$ PostgreSQL $\to$ EventFlow $\to$ RabbitMQ $\to$ Consumers $\to$ Webhook Endpoint $\to$ Audit Trail), every log, span, and record adheres to the unified ID schema:

| Identifier | Type | Format | Ownership | Description |
| :--- | :--- | :--- | :--- | :--- |
| `TenantId` | `string` | Regex `^[a-zA-Z0-9_\-]+$` | HookBridge | Multi-tenant partition boundary. |
| `EventId` | `Guid` | UUID v4 / UUID v7 | EventFlow | Immutable canonical domain event identifier. |
| `DeliveryId` | `Guid` | UUID v4 / UUID v7 | HookBridge | Unique delivery orchestration record for a specific subscription. |
| `AttemptId` | `Guid` | UUID v4 / UUID v7 | HookBridge | Specific HTTP transmission execution attempt. |
| `TraceParent` | `string` | W3C TraceContext | OpenTelemetry | Distributed tracing context (`00-{traceId}-{spanId}-{flags}`). |
| `CorrelationId` | `string` | String (1-128 chars) | Transversal | Cross-system tracking identifier. |
| `OutboxId` | `Guid` | UUID v4 | EventFlow | Transactional outbox ledger row ID. |

### 4.1 End-to-End Trace Propagation Matrix

```
1. Browser / Client API
   └─ Activity: "HookBridge.PublishEvent"
      ├─ TraceId: 4bf92f3577b34da6a3ce929d0e0e4736
      └─ SpanId: 00f067aa0ba902b7

2. HookBridge API / Ingestion
   └─ Activity: "EventFlow.IngestEvent" (ParentSpanId: 00f067aa0ba902b7)
      ├─ Tag: eventflow.tenant_id = "tenant_enterprise_01"
      ├─ Tag: eventflow.event_id = "01918a22-4a7b-7212-8e2b-7c3e1e9f1a01"
      └─ Outbox Record: traceparent = "00-4bf92f35...-01"

3. RabbitMQ Broker Dispatch
   └─ AMQP BasicProperties.Headers["traceparent"] = "00-4bf92f35...-01"
   └─ AMQP BasicProperties.Headers["tenant_id"] = "tenant_enterprise_01"

4. Consumer Worker Execution
   └─ Activity: "EventFlow.ConsumerProcess" (Kind: Consumer)
      ├─ Tag: eventflow.scope = "WebhookDeliveryHandler"
      └─ SpanId: a9812bc3de092817

5. Outbound HTTP Webhook Dispatch
   └─ Activity: "HookBridge.WebhookDispatch"
      ├─ Header: traceparent = "00-4bf92f3577b34da6a3ce929d0e0e4736-a9812bc3de092817-01"
      ├─ Tag: http.status_code = 200
      └─ Tag: hookbridge.delivery_id = "01918a30-9b8c-7324-9f3c-8d4e2f0a2b02"

6. Realtime SignalR Notification
   └─ Hub Group: "tenant:tenant_enterprise_01"
      └─ Broadcast: DeliveryUpdatedEvent (DeliveryId, Status, AttemptCount, Latency)
```

---

## 5. Security & Boundary Verification Summary

- **SSRF Hardening:** Validated via `SsrfGuard` blocking cloud metadata (`169.254.169.254`) and internal subnets.
- **Tenant Isolation:** Enforced on every database query and AMQP routing key/header filter.
- **Trace Continuity:** 100% compliant with W3C TraceContext standards.
