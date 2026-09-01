# HookBridge — Threat Model & Security Architecture (Fase 1.4)

> **Document Version:** 1.0.0  
> **Classification:** Security Engineering & Adversarial Defense  
> **Status:** Approved

---

## 1. Assets, Threat Actors & Trust Boundaries

### 1.1 Protected Assets
1. **Webhook Secrets:** Cryptographic keys used to generate HMAC-SHA256 signatures.
2. **Customer Payloads:** Business events carrying PII, financial information, and transaction data.
3. **API Keys & Credentials:** Authentication tokens granting access to the HookBridge Control Plane.
4. **Internal Network Infrastructure:** Cloud metadata (`169.254.169.254`), databases, Redis, RabbitMQ.
5. **Audit Logs:** Tamper-evident compliance records.

### 1.2 Threat Actors
- **External Attackers:** Malicious actors on the public internet attempting SSRF, brute-forcing API keys, or stealing payloads.
- **Malicious Tenants:** Authenticated users attempting cross-tenant data access (IDOR) or sandbox abuse.
- **Compromised Downstream Endpoints:** External webhook receivers attempting slow-loris attacks, payload injection, or redirect loops.

---

## 2. Threat Scenarios & Mitigations Matrix

| Threat Category | Attack Vector | Potential Impact | HookBridge Defense / Mitigation |
| :--- | :--- | :--- | :--- |
| **SSRF (Server-Side Request Forgery)** | User registers internal IPs (`127.0.0.1`, `10.0.0.1`, `169.254.169.254`) as webhook endpoint. | Internal network reconnaissance, AWS/GCP metadata credential theft. | Pre-validation on endpoint creation + `SsrfGuard` before every HTTP dispatch blocking RFC 1918, link-local, loopback, and IPv6 edge cases. |
| **IDOR / Tenant Escape** | User from Tenant A queries `/api/endpoints/{id_of_tenant_b}`. | Cross-tenant data breach. | Backend tenant authorization handler validates resource ownership against JWT tenant claim on every request. EF Core global query filters enforce isolation. |
| **Webhook Replay Attack** | Intercepted webhook request resent to downstream customer receiver. | Duplicate state transitions (e.g. double balance top-up). | Signature includes UNIX timestamp (`X-HookBridge-Timestamp`); receivers reject requests exceeding 300s drift. |
| **Timing Attack on Signatures** | Attacker measures string comparison duration to forge HMAC signatures. | Webhook spoofing. | Verification instructions and SDK enforce `CryptographicOperations.FixedTimeEquals`. |
| **Secret Leakage in Logs** | Webhook secrets, API keys, or JWT tokens printed in application logs. | Unauthorized access. | Custom logging templates and structured parameters mask all sensitive tokens; payload bodies are redacted. |
| **XSS via Untrusted Payloads** | Malicious script payload injected into event and viewed on developer portal. | Admin session hijacking. | Angular 22 DOM sanitization + payload viewers treat JSON as plain text; zero usage of `innerHTML` or `bypassSecurityTrust`. |
| **Sandbox Receiver Abuse** | Attacker floods sandbox receiver to exhaust storage or use as an open proxy. | Denial of Service (DoS). | Strict rate limiting, 1-hour auto-expiration, payload size cap (256 KB), maximum 100 messages stored per temporary receiver. |
| **Mass Enumeration** | Attacker probes `/auth/login` or `/api/tenants` to discover valid user emails. | Targeted phishing. | Generic authentication error messages ("Invalid credentials"), constant-time response profiles. |

---

## 3. Residual Risks & Ongoing Controls

1. **DNS Rebinding:** Addressed via synchronous IP verification immediately before socket connection during HTTP dispatching.
2. **Secret Rotation Lag:** Customers delaying rotation are protected by dual-secret signing windows (`v1=<new_sig>,v1=<old_sig>`).

