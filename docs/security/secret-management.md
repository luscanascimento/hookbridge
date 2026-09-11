# HookBridge — Production Secret Management & Key Governance

> **Audience:** DevOps, SRE, Platform Engineers & Security Auditors  
> **Status:** Production Release Candidate Guideline  
> **Classification:** Confidential / Internal Engineering Standard

---

## 1. Zero Plaintext Secrets in Source Control

HookBridge enforces a **Zero Hardcoded Secrets Policy**. In production, all cryptographic secrets, connection strings, and integration keys must be injected at runtime via environment variables, Kubernetes Secrets, or a dedicated Secrets Manager.

If any required secret is missing or weak at startup, HookBridge utilizes ASP.NET Core `.ValidateDataAnnotations().ValidateOnStart()` to fail-fast immediately, preventing vulnerable execution.

---

## 2. Secrets Inventory & Constraints

| Secret Environment Variable | Required in Production | Minimum Constraint | Purpose |
| :--- | :---: | :--- | :--- |
| `ConnectionStrings__DefaultConnection` | **Yes** | Valid PostgreSQL connection string with SSL | Relational storage & state persistence |
| `Jwt__SecretKey` | **Yes** | $\ge 32$ characters / 256 bits | HMAC-SHA256 signing of JWT access tokens |
| `WebhookEncryption__MasterKey` | **Yes** | 64-hex chars (32 bytes) or $\ge 32$ chars | AES-256-GCM envelope encryption for webhook secrets |
| `EventFlow__ApiKey` | **Yes** | $\ge 8$ characters | Machine-to-machine authentication against EventFlow Data Plane |
| `POSTGRES_PASSWORD` | **Yes** | High-entropy string | PostgreSQL database user credential |
| `RABBITMQ_PASSWORD` | **Yes** | High-entropy string | RabbitMQ broker authentication |
| `REDIS_PASSWORD` | **Yes** | High-entropy string | Redis distributed cache password |

---

## 3. Production Deployment Providers

### A. Kubernetes Secrets with External Secrets Operator (ESO)
In Kubernetes clusters, HookBridge is deployed with standard `SecretStore` bindings syncing secrets from Vault, AWS Secrets Manager, or GCP Secret Manager into native Kubernetes Secrets mounted as environment variables.

```yaml
apiVersion: external-secrets.io/v1beta1
kind: ExternalSecret
metadata:
  name: hookbridge-secrets
  namespace: hookbridge
spec:
  refreshInterval: "1h"
  secretStoreRef:
    name: vault-backend
    kind: ClusterSecretStore
  target:
    name: hookbridge-env-secrets
  data:
    - secretKey: Jwt__SecretKey
      remoteRef:
        key: secret/data/hookbridge/production
        property: jwt_secret_key
    - secretKey: WebhookEncryption__MasterKey
      remoteRef:
        key: secret/data/hookbridge/production
        property: master_key
```

### B. HashiCorp Vault Integration
When integrating directly with HashiCorp Vault:
1. Store the master encryption key in Vault KV v2:
   ```bash
   vault kv put secret/hookbridge/production \
     jwt_secret_key="$(openssl rand -hex 32)" \
     master_key="$(openssl rand -hex 32)" \
     eventflow_api_key="ef_live_$(openssl rand -hex 16)"
   ```
2. Leverage the Vault Agent Sidecar or CSI Driver to inject environment variables securely without disk persistence.

### C. AWS Secrets Manager / Azure Key Vault / GCP Secret Manager
- In AWS ECS / EKS: Map `secrets` in Task Definitions directly to AWS Secrets Manager ARNs.
- In Azure Container Apps / AKS: Use Azure Key Vault Provider for Secrets Store CSI Driver.
- In GCP Cloud Run / GKE: Mount secrets directly using Secret Manager integration (`--set-secrets`).

---

## 4. Master Key Rotation Procedure

When rotating the `WebhookEncryption__MasterKey`:
1. Generate the new 256-bit key in the Secret Manager.
2. Deploy the key to HookBridge application pods.
3. Use the HookBridge Secret Re-encryption Utility to re-encrypt existing endpoints and webhook secrets with the new key version.

---

## 5. Security & Redaction Safeguards

- Secrets and tokens are never logged by `ILogger` or written to OpenTelemetry traces.
- `TraceContextEnricherMiddleware` and `InMemoryTelemetryBuffer` explicitly redact sensitive headers (`Authorization`, `X-Api-Key`, `Cookie`, `Set-Cookie`).
- RFC 7807 `ProblemDetails` never expose database connection strings or cryptographic stack traces.
