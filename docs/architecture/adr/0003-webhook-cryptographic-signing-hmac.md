# ADR 0003: Webhook Cryptographic Signing with HMAC-SHA256 and Anti-Replay

## Status
Accepted

## Context
Downstream receivers need cryptographic proof that incoming webhooks originated authentically from HookBridge, were not tampered with in transit, and are not replay attacks of previously executed requests.

## Decision
We implement HMAC-SHA256 signature verification following the industry standard:
1. Header: `X-HookBridge-Signature: t=<timestamp>,v1=<signature>`
2. Canonical Payload: `t.<rawJsonPayload>`
3. Hash Algorithm: HMAC-SHA256 using the endpoint's configured signing secret.
4. Replay Prevention: Receiver checks that `|now - timestamp| <= 300 seconds`.
5. Constant-Time Comparison: Enforce `CryptographicOperations.FixedTimeEquals` to prevent side-channel timing attacks.
6. Secret Rotation: Support dual signature emission (`v1=<new_sig>,v1=<old_sig>`) during rotation windows.

## Consequences
### Positive
- Immune to replay attacks and payload tampering.
- Seamless zero-downtime secret rotation.
- Standardized verification compatible with all major programming languages.

### Trade-offs
- Receivers must implement signature verification logic.

