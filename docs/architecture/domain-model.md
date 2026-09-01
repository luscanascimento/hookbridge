# HookBridge — Domain Model & Entity Specifications (Fase 1.2)

> **Document Version:** 1.0.0  
> **Classification:** Domain Engineering & Data Modeling  
> **Status:** Approved

---

## 1. Domain Hierarchy & Bounded Contexts

```
[Tenant] (Root Partition Boundary)
   │
   ├── [User] (Identity, Membership & RBAC)
   ├── [ApiKey] (Machine-to-Machine Ingestion & Management Auth)
   ├── [AuditLog] (Immutable Compliance & Traceability Ledger)
   │
   └── [Application] (Logical grouping of endpoints and events)
         │
         └── [Endpoint] (Destination Webhook URL + Security Config)
               │
               ├── [WebhookSecret] (Active, Rotating, Revoked HMAC Keys)
               ├── [Subscription] (EventType Filters & Wildcard Matchers)
               │
               └── [Delivery] (Orchestration record for an ingested event to an endpoint)
                     │
                     └── [Attempt] (Execution attempt: HTTP headers, payload, status, latency)
```

---

## 2. Core Entities & Aggregate Specifications

### 2.1 Tenant (Aggregate Root)
- `Id`: `Guid` (Primary Key)
- `Identifier`: `string` (Slug, e.g. `acme-corp`, regex: `^[a-z0-9_\-]+$`, Unique)
- `Name`: `string` (e.g. "Acme Corporation")
- `Status`: `TenantStatus` (`Active`, `Suspended`, `Archived`)
- `CreatedAt`: `DateTimeOffset`
- `UpdatedAt`: `DateTimeOffset`

### 2.2 User (Entity)
- `Id`: `Guid`
- `TenantId`: `Guid` (Foreign Key, Tenant Partition)
- `Email`: `string` (Unique per tenant or system-wide)
- `PasswordHash`: `string` (Argon2id or PBKDF2)
- `Role`: `UserRole` (`TenantAdmin`, `Developer`, `Viewer`, `SystemOperator`)
- `Status`: `UserStatus` (`Active`, `Invited`, `Deactivated`)
- `LastLoginAt`: `DateTimeOffset?`

### 2.3 Application (Aggregate Root)
- `Id`: `Guid`
- `TenantId`: `Guid` (Tenant Partition)
- `Name`: `string` (e.g. "Billing System")
- `Description`: `string?`
- `Status`: `ApplicationStatus` (`Active`, `Paused`)
- `CreatedAt`: `DateTimeOffset`

### 2.4 Endpoint (Aggregate Root)
- `Id`: `Guid`
- `TenantId`: `Guid` (Tenant Partition)
- `ApplicationId`: `Guid` (Foreign Key)
- `TargetUrl`: `Uri` (Validated against SSRF Guard)
- `Description`: `string?`
- `Status`: `EndpointStatus` (`Active`, `Paused`, `Disabled`)
- `DisabledReason`: `string?`
- `RateLimitPerMinute`: `int` (Default: 600)
- `TimeoutSeconds`: `int` (Default: 10, Max: 30)
- `CreatedAt`: `DateTimeOffset`
- `UpdatedAt`: `DateTimeOffset`

### 2.5 WebhookSecret (Value Object / Sub-Entity)
- `Id`: `Guid`
- `EndpointId`: `Guid`
- `TenantId`: `Guid`
- `KeyPrefix`: `string` (e.g. `whsec_`)
- `SecretHash`: `string` (SHA-256 hash of secret for identification)
- `EncryptedSecret`: `string` (AES-GCM-256 encrypted payload for signing)
- `Version`: `int` (1, 2, 3...)
- `Status`: `SecretStatus` (`Active`, `Rotating`, `Revoked`)
- `ActivatedAt`: `DateTimeOffset`
- `RevokedAt`: `DateTimeOffset?`

### 2.6 Subscription (Entity)
- `Id`: `Guid`
- `TenantId`: `Guid`
- `EndpointId`: `Guid`
- `EventTypePattern`: `string` (e.g. `payment.settled.v1`, `order.*`, `*`)
- `IsActive`: `bool`
- `CreatedAt`: `DateTimeOffset`

### 2.7 ApiKey (Aggregate Root)
- `Id`: `Guid`
- `TenantId`: `Guid`
- `Name`: `string` (e.g. "Production Ingestion Key")
- `KeyPrefix`: `string` (e.g. `hb_live_9a8b`)
- `KeyHash`: `string` (SHA-256 hash of plaintext key)
- `Scopes`: `ApiKeyScope` (`Events.Ingest`, `Deliveries.Read`, `Deliveries.Replay`, `Admin`)
- `ExpiresAt`: `DateTimeOffset?`
- `RevokedAt`: `DateTimeOffset?`
- `CreatedAt`: `DateTimeOffset`

### 2.8 Delivery (Aggregate Root)
- `Id`: `Guid`
- `TenantId`: `Guid`
- `EventId`: `Guid` (Reference to EventFlow canonical event)
- `EndpointId`: `Guid`
- `SubscriptionId`: `Guid`
- `EventType`: `string`
- `Status`: `DeliveryStatus` (`Pending`, `Dispatched`, `Success`, `Failed`, `DeadLettered`, `Cancelled`)
- `ScheduledAt`: `DateTimeOffset`
- `DeliveredAt`: `DateTimeOffset?`
- `AttemptCount`: `int`
- `TraceParent`: `string` (W3C format)
- `CorrelationId`: `string`
- `OriginalDeliveryId`: `Guid?` (Populated if created via manual replay)

### 2.9 Attempt (Entity)
- `Id`: `Guid`
- `DeliveryId`: `Guid`
- `TenantId`: `Guid`
- `AttemptNumber`: `int` (1, 2, 3...)
- `HttpStatusCode`: `int?`
- `RequestHeadersJson`: `string` (Masked secrets)
- `RequestBody`: `string` (Payload JSON)
- `ResponseHeadersJson`: `string?`
- `ResponseBody`: `string?` (Truncated if $> 64\text{ KB}$)
- `ElapsedMs`: `long`
- `ErrorMessage`: `string?`
- `ExecutedAt`: `DateTimeOffset`

### 2.10 AuditEntry (Entity)
- `Id`: `Guid`
- `TenantId`: `Guid`
- `UserId`: `Guid?` (or ApiKeyId)
- `Action`: `string` (e.g. `Endpoint.Created`, `Secret.Rotated`, `Delivery.Replayed`)
- `ResourceType`: `string` (e.g. `Endpoint`, `ApiKey`, `Delivery`)
- `ResourceId`: `string`
- `DetailsJson`: `string`
- `IpAddress`: `string?`
- `TraceId`: `string?`
- `Timestamp`: `DateTimeOffset`

---

## 3. Invariants & Business Rules

1. **Tenant Inviolability:** No entity can exist without an explicit `TenantId`. EF Core global query filters prevent cross-tenant queries at the data layer.
2. **SSRF Pre-Validation:** An `Endpoint` cannot be created or updated if `TargetUrl` resolves to private, loopback, or cloud metadata IPs.
3. **Secret Immutability:** Once generated, `WebhookSecret` cannot be modified—only rotated or revoked.
4. **Replay Lineage:** Replaying a delivery does not overwrite historical attempts; it provisions a new `Delivery` linking to `OriginalDeliveryId`.

