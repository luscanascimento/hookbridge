# HookBridge Developer Guide & API Reference

Welcome to the **HookBridge Developer Guide**. This document provides an architectural and practical reference for integrating with the HookBridge Webhook Gateway, generating cryptographic signatures, verifying payloads, handling retries, and configuring zero-downtime secret rotation.

---

## 1. Quickstart (5-Minute Integration)

### Step 1: Authentication
All requests to the HookBridge REST API must be authenticated using an API Key (`hb_live_...` or `hb_test_...`) or JWT Bearer Token:

```http
Authorization: Bearer hb_live_sample_apikey_12345
```

### Step 2: Register a Webhook Endpoint
Register your HTTPS endpoint destination where you want HookBridge to deliver webhooks:

```bash
curl -X POST "https://api.hookbridge.io/api/v1/endpoints" \
  -H "Authorization: Bearer hb_live_sample_apikey_12345" \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://api.yourdomain.com/webhooks/hookbridge",
    "description": "Production Order Webhooks",
    "rateLimitPerMinute": 120,
    "timeoutSeconds": 15,
    "eventTypes": ["order.*", "invoice.paid"]
  }'
```

The response includes your endpoint's cryptographic signing secret (`whsec_...`). **Store this secret securely in your environment variables.**

### Step 3: Publish an Event
Publish an event to HookBridge:

```bash
curl -X POST "https://api.hookbridge.io/api/v1/events/publish" \
  -H "Authorization: Bearer hb_live_sample_apikey_12345" \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "order.created",
    "payload": {
      "id": "ord_1001",
      "amount": 149.99,
      "currency": "USD"
    }
  }'
```

HookBridge matches the event against active subscriptions, signs the payload with HMAC-SHA256, and delivers it with full observability.

---

## 2. Webhook Signature Specification & Verification

### Header Format
Outbound webhook requests contain the `X-HookBridge-Signature` header:

```http
X-HookBridge-Signature: t=1757270400,v1=6a8b9c...f4[,v1=secondarySig]
```

- `t`: Integer Unix timestamp (in seconds) of when the dispatch was initiated.
- `v1`: Lowercase hexadecimal HMAC-SHA256 signature calculated over the canonical payload. Multiple `v1` signatures may be present during secret rotation.

### Canonical Payload
The canonical string to sign or verify is constructed as:

$$\text{canonicalPayload} = \text{timestamp} + \text{"."} + \text{rawPayload}$$

> [!IMPORTANT]
> The raw HTTP request body string/bytes must be used. Never parse or re-serialize JSON before verifying the signature.

### Verification Algorithm
1. Extract `t` and all `v1` signatures from `X-HookBridge-Signature`.
2. Verify that $|\text{current\_time} - t| \le 300\text{ seconds}$ (5 minutes) to protect against replay attacks.
3. Compute $\text{HMAC-SHA256}(\text{secret}, \text{canonicalPayload})$.
4. Use **constant-time string comparison** (e.g. `crypto.timingSafeEqual`, `CryptographicOperations.FixedTimeEquals`, `hmac.compare_digest`, `subtle.ConstantTimeCompare`) against all `v1` signatures.

---

## 3. Drop-in SDK Verification Recipes

### TypeScript / Node.js (Express)
```typescript
import crypto from 'crypto';
import type { Request, Response, NextFunction } from 'express';

export function verifyHookBridgeSignature(
  rawBody: string | Buffer,
  signatureHeader: string | undefined,
  secret: string = process.env.HOOKBRIDGE_SECRET!,
  toleranceSeconds = 300
): boolean {
  if (!signatureHeader || !rawBody) return false;

  const elements = signatureHeader.split(',');
  let timestampStr: string | null = null;
  const signatures: string[] = [];

  for (const element of elements) {
    const [k, v] = element.trim().split('=');
    if (k === 't') timestampStr = v;
    else if (k === 'v1') signatures.push(v);
  }

  if (!timestampStr || signatures.length === 0) return false;

  const timestamp = parseInt(timestampStr, 10);
  if (isNaN(timestamp) || Math.abs(Math.floor(Date.now() / 1000) - timestamp) > toleranceSeconds) {
    return false;
  }

  const canonical = `${timestampStr}.${typeof rawBody === 'string' ? rawBody : rawBody.toString('utf8')}`;
  const computed = crypto.createHmac('sha256', secret).update(canonical, 'utf8').digest('hex');
  const computedBuf = Buffer.from(computed, 'hex');

  for (const sig of signatures) {
    try {
      const sigBuf = Buffer.from(sig, 'hex');
      if (computedBuf.length === sigBuf.length && crypto.timingSafeEqual(computedBuf, sigBuf)) {
        return true;
      }
    } catch {}
  }
  return false;
}
```

### C# / .NET 10 (ASP.NET Core Minimal API)
```csharp
using System.Security.Cryptography;
using System.Text;

public static class WebhookVerifier
{
    public static bool VerifySignature(string rawBody, string? signatureHeader, string secret, int toleranceSeconds = 300)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrEmpty(rawBody)) return false;

        string? timestampStr = null;
        var signatures = new List<string>();

        foreach (var part in signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kvp = part.Split('=', 2);
            if (kvp.Length != 2) continue;
            if (kvp[0] == "t") timestampStr = kvp[1];
            else if (kvp[0] == "v1") signatures.Add(kvp[1]);
        }

        if (timestampStr == null || signatures.Count == 0 || !long.TryParse(timestampStr, out var timestamp))
            return false;

        if (Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp) > toleranceSeconds)
            return false;

        var canonical = $"{timestampStr}.{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHex = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
        var computedBytes = Encoding.UTF8.GetBytes(computedHex);

        foreach (var sig in signatures)
        {
            var sigBytes = Encoding.UTF8.GetBytes(sig.ToLowerInvariant());
            if (CryptographicOperations.FixedTimeEquals(computedBytes, sigBytes))
                return true;
        }
        return false;
    }
}
```

### Python (FastAPI)
```python
import hmac
import hashlib
import time
from typing import Optional

def verify_hookbridge_signature(
    raw_body: str,
    signature_header: Optional[str],
    secret: str,
    tolerance_seconds: int = 300
) -> bool:
    if not signature_header or not raw_body:
        return False

    timestamp_str = None
    signatures = []
    for item in signature_header.split(','):
        k, _, v = item.strip().partition('=')
        if k == 't':
            timestamp_str = v
        elif k == 'v1':
            signatures.append(v)

    if not timestamp_str or not signatures:
        return False

    try:
        ts = int(timestamp_str)
        if abs(int(time.time()) - ts) > tolerance_seconds:
            return False
    except ValueError:
        return False

    canonical = f"{timestamp_str}.{raw_body}".encode('utf-8')
    computed = hmac.new(secret.encode('utf-8'), canonical, hashlib.sha256).hexdigest()

    for sig in signatures:
        if hmac.compare_digest(computed, sig):
            return True
    return False
```

### Go
```go
package hookbridge

import (
	"crypto/hmac"
	"crypto/sha256"
	"crypto/subtle"
	"encoding/hex"
	"fmt"
	"math"
	"strconv"
	"strings"
	"time"
)

func VerifySignature(rawBody, signatureHeader, secret string, toleranceSeconds int64) bool {
	if signatureHeader == "" || rawBody == "" {
		return false
	}
	if toleranceSeconds <= 0 {
		toleranceSeconds = 300
	}

	var timestampStr string
	var signatures []string
	for _, item := range strings.Split(signatureHeader, ",") {
		parts := strings.SplitN(strings.TrimSpace(item), "=", 2)
		if len(parts) == 2 {
			if parts[0] == "t" {
				timestampStr = parts[1]
			} else if parts[0] == "v1" {
				signatures = append(signatures, parts[1])
			}
		}
	}

	if timestampStr == "" || len(signatures) == 0 {
		return false
	}

	ts, err := strconv.ParseInt(timestampStr, 10, 64)
	if err != nil || math.Abs(float64(time.Now().Unix()-ts)) > float64(toleranceSeconds) {
		return false
	}

	canonical := fmt.Sprintf("%s.%s", timestampStr, rawBody)
	mac := hmac.New(sha256.New, []byte(secret))
	mac.Write([]byte(canonical))
	computedHex := hex.EncodeToString(mac.Sum(nil))

	for _, sig := range signatures {
		if subtle.ConstantTimeCompare([]byte(computedHex), []byte(sig)) == 1 {
			return true
		}
	}
	return false
}
```

---

## 4. Retries & Idempotency Governance

### Retry Backoff Schedule
HookBridge automatically retries transient dispatch failures using Polly exponential backoff with jitter:
1. **Attempt 1:** Immediate
2. **Attempt 2:** ~5 seconds
3. **Attempt 3:** ~30 seconds
4. **Attempt 4:** ~5 minutes
5. **Attempt 5:** ~30 minutes
6. **Exhaustion:** Enqueued in Dead Letter Queue (DLQ)

### Idempotency Headers
Each delivery payload is accompanied by the delivery ID:
```http
X-HookBridge-Delivery-Id: 7b3f5e92-1234-4567-89ab-cdef01234567
X-HookBridge-Event-Type: order.created
```
Receivers should store the `X-HookBridge-Delivery-Id` in a fast transactional store (Redis/Postgres) and deduplicate repeated attempts.

---

## 5. Zero-Downtime Secret Rotation Workflow

1. Trigger rotation: `POST /api/v1/endpoints/{id}/rotate-secret`.
2. HookBridge enters the **dual-signing rotation window**: all dispatches will be signed with both the old and new secrets (`v1=oldSig,v1=newSig`).
3. Deploy the new secret to your receivers.
4. Verify healthy verification in the Live Inspector (`/live`).
5. Revoke the retired secret: `POST /api/v1/webhook-secrets/{secretId}/revoke`.
