# ADR 0001: Modular Clean Architecture for HookBridge Control Plane

## Status
Accepted

## Context
HookBridge requires a scalable, maintainable, and highly testable backend to handle multi-tenant control plane operations, integration with EventFlow, real-time SignalR notifications, and cryptographic key management without coupling domain logic to external frameworks or database providers.

## Decision
We adopt Clean Architecture organized as a .NET 10 modular solution:
- **`HookBridge.Domain`**: Pure C# enterprise domain entities, value objects, and business invariants.
- **`HookBridge.Application`**: Use cases, CQRS commands/queries, validation, and domain abstractions.
- **`HookBridge.Infrastructure`**: EF Core 10 PostgreSQL persistence, SignalR Hubs, AES-256 encryption, and EventFlow adapters.
- **`HookBridge.Api`**: Minimal APIs, JWT authentication, OpenAPI 3.1, and RFC 7807 ProblemDetails middleware.

## Consequences
### Positive
- Strict separation of business rules from database or transport technologies.
- High testability: domain logic and use cases can be unit tested without database dependencies.
- Clear boundaries prevent architectural erosion.

### Trade-offs
- Requires mapping between domain models and DTOs/persistence models.

