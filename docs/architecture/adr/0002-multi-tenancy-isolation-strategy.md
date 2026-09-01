# ADR 0002: Multi-Tenancy & Data Isolation Strategy

## Status
Accepted

## Context
HookBridge is a multi-tenant SaaS application where multiple organizations manage endpoints, secrets, and deliveries on shared infrastructure. Cross-tenant data leakage is a critical security vulnerability.

## Decision
We enforce a **Discriminator-Based Partitioning with Row-Level Isolation** strategy:
1. Every tenant-scoped entity implements `ITenantScoped` with a `TenantId` column.
2. EF Core global query filters automatically append `WHERE TenantId = @CurrentTenantId` to all database queries.
3. The `CurrentTenantId` is extracted securely from the verified JWT claims or hashed API Key metadata in backend middleware.
4. SignalR Hub connections are isolated into tenant-specific groups (`tenant:{tenantId}`).

## Consequences
### Positive
- Prevents IDOR and cross-tenant enumeration at both the API and database levels.
- Seamless developer experience: queries naturally filter by current tenant.

### Trade-offs
- Administrative cross-tenant queries require explicit filter disabling with strict elevated permissions.

