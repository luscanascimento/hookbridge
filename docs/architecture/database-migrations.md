# HookBridge — Database Persistence & Production Migration Governance

> **Audience:** DevOps, Database Administrators, SREs & Platform Engineers  
> **Target Database:** PostgreSQL 16+  
> **ORM / Framework:** Entity Framework Core 9.0 (`Npgsql.EntityFrameworkCore.PostgreSQL`)  
> **Classification:** Technical Reference / Deployment SOP

---

## 1. Zero Auto-Migration on Multi-Replica Startup

In production environments running multiple horizontal pods/replicas of `HookBridge.Api`, executing `Database.Migrate()` automatically during application startup introduces severe race conditions, distributed deadlocks on `__EFMigrationsHistory`, and unpredictable schema lockouts.

HookBridge mandates a **Decoupled Migration Pipeline**:
1. **Pre-Deployment Migration Job (Kubernetes Job / CI/CD Release Phase):** Migrations are applied once before new application pods receive traffic.
2. **Idempotent SQL Scripts (`init_schema.sql`):** Migrations are compiled to idempotent SQL scripts checked into version control and executed via migration pipelines with transaction rollback guarantees.

---

## 2. Migration Execution Options

### Option A: Kubernetes Pre-Upgrade Job (Recommended)
```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: hookbridge-db-migrate
  namespace: hookbridge
  annotations:
    "helm.sh/hook": pre-install,pre-upgrade
    "helm.sh/hook-delete-policy": before-hook-creation,hook-succeeded
spec:
  template:
    spec:
      restartPolicy: OnFailure
      containers:
        - name: db-migrator
          image: hookbridge-api:1.0.0
          command: ["dotnet", "ef", "database", "update"]
          envFrom:
            - secretRef:
                name: hookbridge-env-secrets
```

### Option B: Idempotent SQL Execution
Run the idempotent SQL script directly against PostgreSQL during release automation:
```bash
psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -f src/HookBridge.Infrastructure/Persistence/Migrations/init_schema.sql
```

---

## 3. Entity Constraints & Indexing Strategy

HookBridge persistence is optimized for multi-tenant query filtering, high-throughput delivery lookups, and chronological event streams.

### Key Indexes & Unique Constraints
- **Tenants (`tenants`):**
  - Unique Index on `Identifier` (slug).
- **Users (`users`):**
  - Unique Composite Index on `(TenantId, Email)`.
- **Applications (`applications`):**
  - Composite Index on `(TenantId, Name)`.
- **Endpoints (`endpoints`):**
  - Composite Indexes on `(TenantId, Status)` and `(TenantId, ApplicationId, Status)`.
- **Subscriptions (`subscriptions`):**
  - Unique Composite Index on `(EndpointId, EventTypePattern)`.
- **API Keys (`api_keys`):**
  - Unique Index on `KeyHash`.
  - Prefix Lookup Index on `KeyPrefix` and `TenantId`.
- **Refresh Tokens (`refresh_tokens`):**
  - Unique Index on `TokenHash`.
  - Composite Index on `(TenantId, UserId)`.
- **Deliveries (`deliveries`):**
  - Composite Partition Indexes:
    - `(TenantId, Status, CreatedAt)`
    - `(TenantId, EndpointId, CreatedAt)`
    - `(TenantId, EventType, CreatedAt)`
    - `(TenantId, CorrelationId)`
- **Attempts (`attempts`):**
  - Unique Composite Index on `(DeliveryId, AttemptNumber)`.
  - Chronological Composite Index on `(TenantId, ExecutedAt)`.
- **Audit Entries (`audit_entries`):**
  - Chronological Partition Index on `(TenantId, Timestamp)`.

---

## 4. Encryption of Sensitive Data at Rest

- All webhook signing secrets (`webhook_secrets.EncryptedSecret`) are encrypted using **AES-256-GCM envelope encryption** prior to database persistence.
- API keys (`api_keys.KeyHash`) and user passwords (`users.PasswordHash`) are stored exclusively as non-reversible cryptographic hashes (PBKDF2 HMAC-SHA256 with 100,000 iterations and salt).

---

## 5. Resilience & Connection Retry Policy

HookBridge configures Npgsql connection resiliency with automatic transient failure retry:
```csharp
npgsqlOptions.EnableRetryOnFailure(
    maxRetryCount: 3,
    maxRetryDelay: TimeSpan.FromSeconds(5),
    errorCodesToAdd: null);
```
