using System.Globalization;
using HookBridge.Application.ControlPlane.Services;

namespace HookBridge.Application.ControlPlane.UseCases.Documentation;

public sealed class GetApiReferenceUseCase
{
    private static readonly string[] EndpointEventPatterns = ["order.*", "invoice.paid"];
    private readonly IDocSnippetGenerator _snippetGenerator;

    public GetApiReferenceUseCase(IDocSnippetGenerator snippetGenerator)
    {
        _snippetGenerator = snippetGenerator;
    }

    public ApiReferenceResponse Execute(string? baseUrl = null, string? apiKey = null)
    {
        var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.hookbridge.io" : baseUrl.TrimEnd('/');
        var effectiveApiKey = string.IsNullOrWhiteSpace(apiKey) ? "hb_live_sample_apikey_12345" : apiKey;
        var sampleSecret = "whsec_live_example_secret_key_8f9e";

        var guides = BuildGuides(effectiveBaseUrl, effectiveApiKey, sampleSecret);
        var endpoints = BuildEndpoints(effectiveBaseUrl, effectiveApiKey);
        var sdkRecipes = BuildSdkRecipes(effectiveBaseUrl, effectiveApiKey, sampleSecret);

        return new ApiReferenceResponse(
            Version: "1.0.0",
            Environment: "Production-Ready Control Plane",
            BaseUrl: effectiveBaseUrl,
            Guides: guides,
            Endpoints: endpoints,
            SdkRecipes: sdkRecipes
        );
    }

    private List<DocSectionDto> BuildGuides(string baseUrl, string apiKey, string secret)
    {
        return
        [
            new(
                Id: "quickstart",
                Title: "5-Minute Quickstart Guide",
                Category: "Getting Started",
                Summary: "Learn how to register a webhook destination endpoint, publish your first event, and inspect deliveries in HookBridge.",
                ContentMarkdown: @"### 1. Authenticate with an API Key
All requests to the HookBridge REST API require authentication via an API Key or Bearer token passed in the `Authorization` header:
```http
Authorization: Bearer hb_live_...
```

### 2. Register Your Webhook Endpoint
Register your HTTPS endpoint URL where you want HookBridge to dispatch events. You will receive an endpoint ID and a cryptographically secure HMAC secret (`whsec_...`).

### 3. Publish an Event
Publish an event to HookBridge using `POST /api/v1/events/publish`. HookBridge matches the `eventType` against registered endpoint subscriptions (with wildcard pattern support like `order.*`) and securely delivers the payload with HMAC-SHA256 signatures.

### 4. Verify Signatures in Your Webhook Receiver
Always verify the `X-HookBridge-Signature` header to guarantee that payloads originate from HookBridge and have not been tampered with or replayed.",
                CodeSnippets: new Dictionary<string, string>
                {
                    ["curl"] = _snippetGenerator.GenerateEventPublishingSnippet("curl", baseUrl, apiKey, "order.created", "{\"orderId\": \"ord_9981\", \"total\": 129.50, \"currency\": \"USD\"}"),
                    ["typescript"] = _snippetGenerator.GenerateEventPublishingSnippet("typescript", baseUrl, apiKey, "order.created", "{\"orderId\": \"ord_9981\", \"total\": 129.50, \"currency\": \"USD\"}"),
                    ["csharp"] = _snippetGenerator.GenerateEventPublishingSnippet("csharp", baseUrl, apiKey, "order.created", "{\"orderId\": \"ord_9981\", \"total\": 129.50, \"currency\": \"USD\"}"),
                    ["python"] = _snippetGenerator.GenerateEventPublishingSnippet("python", baseUrl, apiKey, "order.created", "{\"orderId\": \"ord_9981\", \"total\": 129.50, \"currency\": \"USD\"}"),
                    ["go"] = _snippetGenerator.GenerateEventPublishingSnippet("go", baseUrl, apiKey, "order.created", "{\"orderId\": \"ord_9981\", \"total\": 129.50, \"currency\": \"USD\"}")
                }
            ),
            new(
                Id: "webhook-signing",
                Title: "Cryptographic HMAC-SHA256 Signatures",
                Category: "Security",
                Summary: "Deep dive into HookBridge webhook signing, canonical payload formatting, anti-replay tolerance windows, and dual-secret rotation.",
                ContentMarkdown: @"### Webhook Signature Header Specification
HookBridge computes an HMAC-SHA256 signature for every outbound webhook dispatch and attaches it via the `X-HookBridge-Signature` header:
```http
X-HookBridge-Signature: t=1757270400,v1=6a8b9c...f4
```

### Canonical Payload Construction
To prevent delimiter injection or serialization discrepancy attacks:
1. Extract the integer Unix timestamp string `t` from the header.
2. Form the canonical string:
```text
canonicalPayload = `${timestamp}.${rawBody}`
```
3. Compute `HMAC-SHA256(secret, canonicalPayload)` and format as a lowercase hex string.

### Anti-Replay Protection Window
Receivers **must** check that `|current_time - timestamp| <= 300 seconds` (5 minutes) before proceeding with HMAC verification. Any timestamp older than 300s or in the future must be rejected with HTTP 401.

### Zero-Downtime Secret Rotation
During secret rotation, HookBridge signs payloads with **both** the old and new secrets, emitting multiple `v1` tags:
```http
X-HookBridge-Signature: t=1757270400,v1=oldSigHex,v1=newSigHex
```
The receiver matches any of the `v1` values using constant-time comparison.",
                CodeSnippets: new Dictionary<string, string>
                {
                    ["typescript"] = _snippetGenerator.GenerateSignatureVerificationSnippet("typescript", secret),
                    ["csharp"] = _snippetGenerator.GenerateSignatureVerificationSnippet("csharp", secret),
                    ["python"] = _snippetGenerator.GenerateSignatureVerificationSnippet("python", secret),
                    ["go"] = _snippetGenerator.GenerateSignatureVerificationSnippet("go", secret),
                    ["php"] = _snippetGenerator.GenerateSignatureVerificationSnippet("php", secret)
                }
            ),
            new(
                Id: "retries-and-idempotency",
                Title: "Retries, Backoff & Idempotency",
                Category: "Reliability",
                Summary: "Understand HookBridge retry schedule, exponential backoff, circuit breaking, idempotency headers, and Dead Letter Queue routing.",
                ContentMarkdown: @"### Delivery Retries & Exponential Backoff
HookBridge automatically retries failed deliveries (e.g. 5xx status codes, timeouts, connection drops) with exponential backoff and jitter:
- **Attempt 1:** Immediate
- **Attempt 2:** ~5 seconds
- **Attempt 3:** ~30 seconds
- **Attempt 4:** ~5 minutes
- **Attempt 5:** ~30 minutes
- **Final Failure:** Routed to Dead Letter Queue (DLQ)

### Idempotency
Every webhook delivery contains a unique delivery identifier header:
```http
X-HookBridge-Delivery-Id: 7b3f5e92-1234-4567-89ab-cdef01234567
X-HookBridge-Event-Type: order.created
```
Receivers should store processed `X-HookBridge-Delivery-Id` values in Redis or a relational database for at least 24 hours to prevent duplicate processing on retries.",
                CodeSnippets: new Dictionary<string, string>
                {
                    ["curl"] = _snippetGenerator.GenerateDeliveryReplaySnippet("curl", baseUrl, apiKey, Guid.Parse("7b3f5e92-1234-4567-89ab-cdef01234567")),
                    ["typescript"] = _snippetGenerator.GenerateDeliveryReplaySnippet("typescript", baseUrl, apiKey, Guid.Parse("7b3f5e92-1234-4567-89ab-cdef01234567")),
                    ["csharp"] = _snippetGenerator.GenerateDeliveryReplaySnippet("csharp", baseUrl, apiKey, Guid.Parse("7b3f5e92-1234-4567-89ab-cdef01234567"))
                }
            ),
            new(
                Id: "secret-rotation",
                Title: "Zero-Downtime Secret Rotation",
                Category: "Security",
                Summary: "Step-by-step instructions for rolling webhook signing secrets without dropping webhook events.",
                ContentMarkdown: @"### Secret Rotation Workflow
1. Call `POST /api/v1/endpoints/{id}/rotate-secret`.
2. HookBridge enters the **dual-signing rotation window**: events will now be signed with both the previous secret and the new secret.
3. Deploy the new secret to your production webhook receiver.
4. Verify in the HookBridge Dashboard or Live Inspector that incoming webhooks are successfully verified.
5. Once deployed, call `POST /api/v1/webhook-secrets/{secretId}/revoke` to revoke the previous secret.",
                CodeSnippets: new Dictionary<string, string>
                {
                    ["curl"] = string.Create(CultureInfo.InvariantCulture, $@"curl -X POST ""{baseUrl}/api/v1/endpoints/3fa85f64-5717-4562-b3fc-2c963f66afa6/rotate-secret"" \
  -H ""Authorization: Bearer {apiKey}"""),
                    ["typescript"] = string.Create(CultureInfo.InvariantCulture, $@"import axios from 'axios';

async function rotateEndpointSecret(endpointId: string) {{
  const res = await axios.post(`{baseUrl}/api/v1/endpoints/${{endpointId}}/rotate-secret`, {{}}, {{
    headers: {{ 'Authorization': `Bearer {apiKey}` }}
  }});
  console.log('New Signing Secret:', res.data.newSecret);
  return res.data;
}}"),
                    ["csharp"] = string.Create(CultureInfo.InvariantCulture, $@"using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(""Bearer"", ""{apiKey}"");

var response = await client.PostAsync(""{baseUrl}/api/v1/endpoints/3fa85f64-5717-4562-b3fc-2c963f66afa6/rotate-secret"", null);
var json = await response.Content.ReadAsStringAsync();
Console.WriteLine($""Secret rotated: {{json}}"");")
                }
            )
        ];
    }

    private List<DocEndpointDto> BuildEndpoints(string baseUrl, string apiKey)
    {
        return
        [
            new(
                Id: "publish-event",
                Category: "Event Publishing",
                Method: "POST",
                Path: "/api/v1/events/publish",
                Summary: "Publish an event to HookBridge",
                Description: "Ingests an event payload, matches it against all active endpoint subscriptions, persists transactional outbox records, and dispatches HMAC-signed webhooks.",
                PathParameters: [],
                QueryParameters: [],
                RequestHeaders:
                [
                    new("Authorization", true, "Bearer API Key (hb_live_...) or JWT Token", "Bearer hb_live_sample_apikey_12345"),
                    new("Content-Type", true, "Must be application/json", "application/json"),
                    new("X-Correlation-Id", false, "Optional custom distributed correlation identifier", "corr_78910")
                ],
                SampleRequestBody: @"{
  ""eventType"": ""order.created"",
  ""payload"": {
    ""id"": ""ord_1001"",
    ""customer"": {
      ""id"": ""cust_55"",
      ""email"": ""alex@example.com""
    },
    ""amount"": 199.99,
    ""currency"": ""USD"",
    ""items"": [
      { ""sku"": ""PROD-1"", ""quantity"": 2, ""price"": 99.99 }
    ]
  }
}",
                ExpectedStatusCode: 200,
                SampleResponseBody: @"{
  ""id"": ""e3b0c442-98fc-1c14-9afb-4c8996fb9242"",
  ""eventType"": ""order.created"",
  ""matchedEndpointsCount"": 2,
  ""deliveryIds"": [
    ""7b3f5e92-1234-4567-89ab-cdef01234567"",
    ""8c4a6f03-2345-5678-90bc-def012345678""
  ],
  ""publishedAt"": ""2026-09-07T22:30:00Z"",
  ""traceId"": ""4bf92f3577b34da6a3ce929d0e0e4736""
}",
                CodeSnippets: new Dictionary<string, string>
                {
                    ["curl"] = _snippetGenerator.GenerateEventPublishingSnippet("curl", baseUrl, apiKey, "order.created", "{\"id\": \"ord_1001\", \"amount\": 199.99, \"currency\": \"USD\"}"),
                    ["typescript"] = _snippetGenerator.GenerateEventPublishingSnippet("typescript", baseUrl, apiKey, "order.created", "{\"id\": \"ord_1001\", \"amount\": 199.99, \"currency\": \"USD\"}"),
                    ["csharp"] = _snippetGenerator.GenerateEventPublishingSnippet("csharp", baseUrl, apiKey, "order.created", "{\"id\": \"ord_1001\", \"amount\": 199.99, \"currency\": \"USD\"}"),
                    ["python"] = _snippetGenerator.GenerateEventPublishingSnippet("python", baseUrl, apiKey, "order.created", "{\"id\": \"ord_1001\", \"amount\": 199.99, \"currency\": \"USD\"}"),
                    ["go"] = _snippetGenerator.GenerateEventPublishingSnippet("go", baseUrl, apiKey, "order.created", "{\"id\": \"ord_1001\", \"amount\": 199.99, \"currency\": \"USD\"}")
                }
            ),
            new(
                Id: "list-endpoints",
                Category: "Endpoints",
                Method: "GET",
                Path: "/api/v1/endpoints",
                Summary: "List all webhook endpoints",
                Description: "Retrieves a paginated list of webhook endpoints registered for the authenticated tenant.",
                PathParameters: [],
                QueryParameters:
                [
                    new("applicationId", "uuid", false, "Filter endpoints by Application ID", null),
                    new("status", "string", false, "Filter by status: Active, Paused, Disabled", "Active"),
                    new("search", "string", false, "Search URL or description", "billing"),
                    new("page", "integer", false, "Page number (1-indexed, default: 1)", "1"),
                    new("pageSize", "integer", false, "Page size (default: 20, max: 100)", "20")
                ],
                RequestHeaders:
                [
                    new("Authorization", true, "Bearer API Key or JWT Token", "Bearer hb_live_sample_apikey_12345")
                ],
                SampleRequestBody: null,
                ExpectedStatusCode: 200,
                SampleResponseBody: @"{
  ""items"": [
    {
      ""id"": ""3fa85f64-5717-4562-b3fc-2c963f66afa6"",
      ""applicationId"": ""a1b2c3d4-e5f6-7890-1234-56789abcdef0"",
      ""url"": ""https://api.example.com/webhooks/orders"",
      ""description"": ""Production Order Webhooks"",
      ""status"": ""Active"",
      ""rateLimitPerMinute"": 120,
      ""timeoutSeconds"": 15,
      ""subscriptions"": [
        { ""id"": ""sub_1"", ""eventTypePattern"": ""order.*"" }
      ],
      ""createdAt"": ""2026-09-01T10:00:00Z""
    }
  ],
  ""totalCount"": 1,
  ""page"": 1,
  ""pageSize"": 20
}",
                CodeSnippets: new Dictionary<string, string>
                {
                    ["curl"] = string.Create(CultureInfo.InvariantCulture, $@"curl -X GET ""{baseUrl}/api/v1/endpoints"" \
  -H ""Authorization: Bearer {apiKey}"""),
                    ["typescript"] = string.Create(CultureInfo.InvariantCulture, $@"import axios from 'axios';

async function listEndpoints() {{
  const res = await axios.get('{baseUrl}/api/v1/endpoints', {{
    headers: {{ 'Authorization': `Bearer {apiKey}` }}
  }});
  return res.data;
}}"),
                    ["csharp"] = string.Create(CultureInfo.InvariantCulture, $@"using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(""Bearer"", ""{apiKey}"");

var response = await client.GetAsync(""{baseUrl}/api/v1/endpoints"");
var json = await response.Content.ReadAsStringAsync();
Console.WriteLine(json);")
                }
            ),
            new(
                Id: "create-endpoint",
                Category: "Endpoints",
                Method: "POST",
                Path: "/api/v1/endpoints",
                Summary: "Register a new webhook endpoint",
                Description: "Creates a new webhook destination with SSRF validation, generates a cryptographic HMAC signing secret, and binds event subscriptions.",
                PathParameters: [],
                QueryParameters: [],
                RequestHeaders:
                [
                    new("Authorization", true, "Bearer API Key or JWT Token", "Bearer hb_live_sample_apikey_12345"),
                    new("Content-Type", true, "Must be application/json", "application/json")
                ],
                SampleRequestBody: @"{
  ""applicationId"": ""a1b2c3d4-e5f6-7890-1234-56789abcdef0"",
  ""url"": ""https://api.example.com/webhooks/orders"",
  ""description"": ""Primary Webhook Receiver"",
  ""rateLimitPerMinute"": 120,
  ""timeoutSeconds"": 15,
  ""eventTypes"": [""order.*"", ""invoice.paid""]
}",
                ExpectedStatusCode: 201,
                SampleResponseBody: @"{
  ""id"": ""3fa85f64-5717-4562-b3fc-2c963f66afa6"",
  ""applicationId"": ""a1b2c3d4-e5f6-7890-1234-56789abcdef0"",
  ""url"": ""https://api.example.com/webhooks/orders"",
  ""description"": ""Primary Webhook Receiver"",
  ""status"": ""Active"",
  ""signingSecret"": ""whsec_live_9f8e7d6c5b4a3210fe"",
  ""createdAt"": ""2026-09-07T22:30:00Z""
}",
                CodeSnippets: new Dictionary<string, string>
                {
                    ["curl"] = _snippetGenerator.GenerateEndpointRegistrationSnippet("curl", baseUrl, apiKey, "https://api.example.com/webhooks/orders", "Primary Webhook Receiver", EndpointEventPatterns),
                    ["typescript"] = _snippetGenerator.GenerateEndpointRegistrationSnippet("typescript", baseUrl, apiKey, "https://api.example.com/webhooks/orders", "Primary Webhook Receiver", EndpointEventPatterns),
                    ["csharp"] = _snippetGenerator.GenerateEndpointRegistrationSnippet("csharp", baseUrl, apiKey, "https://api.example.com/webhooks/orders", "Primary Webhook Receiver", EndpointEventPatterns),
                    ["python"] = _snippetGenerator.GenerateEndpointRegistrationSnippet("python", baseUrl, apiKey, "https://api.example.com/webhooks/orders", "Primary Webhook Receiver", EndpointEventPatterns),
                    ["go"] = _snippetGenerator.GenerateEndpointRegistrationSnippet("go", baseUrl, apiKey, "https://api.example.com/webhooks/orders", "Primary Webhook Receiver", EndpointEventPatterns)
                }
            ),
            new(
                Id: "endpoint-health",
                Category: "Endpoints",
                Method: "GET",
                Path: "/api/v1/endpoints/{id}/health",
                Summary: "Get endpoint health & latency metrics",
                Description: "Calculates real-time health score (0-100), 24-hour SLA uptime %, latency quantiles (p50, p90, p95, p99), circuit breaker status, and active incident alerts.",
                PathParameters:
                [
                    new("id", "uuid", true, "The unique Endpoint ID", "3fa85f64-5717-4562-b3fc-2c963f66afa6")
                ],
                QueryParameters: [],
                RequestHeaders:
                [
                    new("Authorization", true, "Bearer API Key or JWT Token", "Bearer hb_live_sample_apikey_12345")
                ],
                SampleRequestBody: null,
                ExpectedStatusCode: 200,
                SampleResponseBody: @"{
  ""endpointId"": ""3fa85f64-5717-4562-b3fc-2c963f66afa6"",
  ""url"": ""https://api.example.com/webhooks/orders"",
  ""healthScore"": 98.5,
  ""rating"": ""Healthy"",
  ""uptimePercentage24h"": 99.95,
  ""successRatePercentage24h"": 99.8,
  ""latencyQuantiles"": {
    ""p50Ms"": 85.0,
    ""p90Ms"": 142.0,
    ""p95Ms"": 198.0,
    ""p99Ms"": 310.0,
    ""avgMs"": 94.2
  },
  ""circuitBreaker"": {
    ""state"": ""Closed"",
    ""consecutiveFailures"": 0,
    ""lastStateChangeAt"": ""2026-09-01T00:00:00Z""
  },
  ""activeIncidents"": []
}",
                CodeSnippets: new Dictionary<string, string>
                {
                    ["curl"] = string.Create(CultureInfo.InvariantCulture, $@"curl -X GET ""{baseUrl}/api/v1/endpoints/3fa85f64-5717-4562-b3fc-2c963f66afa6/health"" \
  -H ""Authorization: Bearer {apiKey}"""),
                    ["typescript"] = string.Create(CultureInfo.InvariantCulture, $@"import axios from 'axios';

async function getEndpointHealth(endpointId: string) {{
  const res = await axios.get(`{baseUrl}/api/v1/endpoints/${{endpointId}}/health`, {{
    headers: {{ 'Authorization': `Bearer {apiKey}` }}
  }});
  return res.data;
}}"),
                    ["csharp"] = string.Create(CultureInfo.InvariantCulture, $@"using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(""Bearer"", ""{apiKey}"");

var response = await client.GetAsync(""{baseUrl}/api/v1/endpoints/3fa85f64-5717-4562-b3fc-2c963f66afa6/health"");
var json = await response.Content.ReadAsStringAsync();
Console.WriteLine(json);")
                }
            ),
            new(
                Id: "list-deliveries",
                Category: "Deliveries",
                Method: "GET",
                Path: "/api/v1/deliveries",
                Summary: "Query webhook deliveries",
                Description: "Multidimensional search and filter across historical deliveries and attempts by endpoint, status, event type, date range, or correlation ID.",
                PathParameters: [],
                QueryParameters:
                [
                    new("endpointId", "uuid", false, "Filter by target endpoint ID", null),
                    new("status", "string", false, "Status: Pending, Dispatched, Success, Failed, DeadLettered", "Success"),
                    new("eventType", "string", false, "Filter by event type", "order.created"),
                    new("correlationId", "string", false, "Filter by correlation ID", null),
                    new("page", "integer", false, "Page number (default: 1)", "1"),
                    new("pageSize", "integer", false, "Page size (default: 20)", "20")
                ],
                RequestHeaders:
                [
                    new("Authorization", true, "Bearer API Key or JWT Token", "Bearer hb_live_sample_apikey_12345")
                ],
                SampleRequestBody: null,
                ExpectedStatusCode: 200,
                SampleResponseBody: @"{
  ""items"": [
    {
      ""id"": ""7b3f5e92-1234-4567-89ab-cdef01234567"",
      ""endpointId"": ""3fa85f64-5717-4562-b3fc-2c963f66afa6"",
      ""endpointUrl"": ""https://api.example.com/webhooks/orders"",
      ""eventType"": ""order.created"",
      ""status"": ""Success"",
      ""attemptsCount"": 1,
      ""lastAttemptStatus"": ""Success"",
      ""lastAttemptStatusCode"": 200,
      ""lastAttemptLatencyMs"": 92,
      ""createdAt"": ""2026-09-07T22:25:00Z"",
      ""updatedAt"": ""2026-09-07T22:25:01Z""
    }
  ],
  ""totalCount"": 1,
  ""page"": 1,
  ""pageSize"": 20
}",
                CodeSnippets: new Dictionary<string, string>
                {
                    ["curl"] = string.Create(CultureInfo.InvariantCulture, $@"curl -X GET ""{baseUrl}/api/v1/deliveries?status=Success&eventType=order.created"" \
  -H ""Authorization: Bearer {apiKey}"""),
                    ["typescript"] = string.Create(CultureInfo.InvariantCulture, $@"import axios from 'axios';

async function listDeliveries() {{
  const res = await axios.get('{baseUrl}/api/v1/deliveries', {{
    params: {{ status: 'Success', eventType: 'order.created' }},
    headers: {{ 'Authorization': `Bearer {apiKey}` }}
  }});
  return res.data;
}}"),
                    ["csharp"] = string.Create(CultureInfo.InvariantCulture, $@"using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(""Bearer"", ""{apiKey}"");

var response = await client.GetAsync(""{baseUrl}/api/v1/deliveries?status=Success"");
var json = await response.Content.ReadAsStringAsync();
Console.WriteLine(json);")
                }
            ),
            new(
                Id: "replay-delivery",
                Category: "Deliveries",
                Method: "POST",
                Path: "/api/v1/deliveries/{id}/replay",
                Summary: "Replay a single delivery",
                Description: "Re-triggers a webhook delivery with the exact original event payload, recording full ancestry and descendant lineage.",
                PathParameters:
                [
                    new("id", "uuid", true, "The delivery ID to replay", "7b3f5e92-1234-4567-89ab-cdef01234567")
                ],
                QueryParameters: [],
                RequestHeaders:
                [
                    new("Authorization", true, "Bearer API Key or JWT Token", "Bearer hb_live_sample_apikey_12345")
                ],
                SampleRequestBody: null,
                ExpectedStatusCode: 200,
                SampleResponseBody: @"{
  ""originalDeliveryId"": ""7b3f5e92-1234-4567-89ab-cdef01234567"",
  ""newDeliveryId"": ""9a1b2c3d-4e5f-6789-0123-456789abcdef"",
  ""status"": ""Pending"",
  ""replayedAt"": ""2026-09-07T22:30:00Z""
}",
                CodeSnippets: new Dictionary<string, string>
                {
                    ["curl"] = _snippetGenerator.GenerateDeliveryReplaySnippet("curl", baseUrl, apiKey, Guid.Parse("7b3f5e92-1234-4567-89ab-cdef01234567")),
                    ["typescript"] = _snippetGenerator.GenerateDeliveryReplaySnippet("typescript", baseUrl, apiKey, Guid.Parse("7b3f5e92-1234-4567-89ab-cdef01234567")),
                    ["csharp"] = _snippetGenerator.GenerateDeliveryReplaySnippet("csharp", baseUrl, apiKey, Guid.Parse("7b3f5e92-1234-4567-89ab-cdef01234567")),
                    ["python"] = _snippetGenerator.GenerateDeliveryReplaySnippet("python", baseUrl, apiKey, Guid.Parse("7b3f5e92-1234-4567-89ab-cdef01234567")),
                    ["go"] = _snippetGenerator.GenerateDeliveryReplaySnippet("go", baseUrl, apiKey, Guid.Parse("7b3f5e92-1234-4567-89ab-cdef01234567"))
                }
            ),
            new(
                Id: "get-trace",
                Category: "Observability",
                Method: "GET",
                Path: "/api/v1/traces/{identifier}",
                Summary: "Inspect distributed trace waterfall",
                Description: "Correlates ingestion, outbox persistence, RabbitMQ transit, consumer dispatch, HTTP attempts, and audit ledger spans into an end-to-end waterfall DAG.",
                PathParameters:
                [
                    new("identifier", "string", true, "Trace ID, Delivery ID, or Correlation ID", "4bf92f3577b34da6a3ce929d0e0e4736")
                ],
                QueryParameters: [],
                RequestHeaders:
                [
                    new("Authorization", true, "Bearer API Key or JWT Token", "Bearer hb_live_sample_apikey_12345")
                ],
                SampleRequestBody: null,
                ExpectedStatusCode: 200,
                SampleResponseBody: @"{
  ""traceId"": ""4bf92f3577b34da6a3ce929d0e0e4736"",
  ""rootSpanName"": ""hookbridge.gateway.ingest"",
  ""totalDurationMs"": 114.5,
  ""spansCount"": 6,
  ""spans"": [
    {
      ""spanId"": ""00f067aa0ba902b7"",
      ""parentSpanId"": null,
      ""name"": ""hookbridge.gateway.ingest"",
      ""serviceName"": ""HookBridge.Api"",
      ""startTimeOffsetMs"": 0.0,
      ""durationMs"": 18.2,
      ""status"": ""Ok""
    }
  ]
}",
                CodeSnippets: new Dictionary<string, string>
                {
                    ["curl"] = string.Create(CultureInfo.InvariantCulture, $@"curl -X GET ""{baseUrl}/api/v1/traces/4bf92f3577b34da6a3ce929d0e0e4736"" \
  -H ""Authorization: Bearer {apiKey}"""),
                    ["typescript"] = string.Create(CultureInfo.InvariantCulture, $@"import axios from 'axios';

async function getTraceWaterfall(traceId: string) {{
  const res = await axios.get(`{baseUrl}/api/v1/traces/${{traceId}}`, {{
    headers: {{ 'Authorization': `Bearer {apiKey}` }}
  }});
  return res.data;
}}"),
                    ["csharp"] = string.Create(CultureInfo.InvariantCulture, $@"using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(""Bearer"", ""{apiKey}"");

var response = await client.GetAsync(""{baseUrl}/api/v1/traces/4bf92f3577b34da6a3ce929d0e0e4736"");
var json = await response.Content.ReadAsStringAsync();
Console.WriteLine(json);")
                }
            ),
            new(
                Id: "verify-signature",
                Category: "Security",
                Method: "POST",
                Path: "/api/v1/webhook-signatures/verify",
                Summary: "Test signature verification algorithm",
                Description: "Developer testing endpoint to verify whether a given signature header correctly matches a secret and raw payload.",
                PathParameters: [],
                QueryParameters: [],
                RequestHeaders:
                [
                    new("Authorization", true, "Bearer API Key or JWT Token", "Bearer hb_live_sample_apikey_12345"),
                    new("Content-Type", true, "Must be application/json", "application/json")
                ],
                SampleRequestBody: @"{
  ""secret"": ""whsec_live_example_secret_key_8f9e"",
  ""signatureHeader"": ""t=1757270400,v1=6a8b9c..."",
  ""rawPayload"": ""{\""orderId\"": \""ord_9981\""}"",
  ""toleranceSeconds"": 300
}",
                ExpectedStatusCode: 200,
                SampleResponseBody: @"{
  ""isValid"": true,
  ""parsedTimestamp"": 1757270400,
  ""clockSkewSeconds"": 2,
  ""matchedSignatureVersion"": ""v1""
}",
                CodeSnippets: new Dictionary<string, string>
                {
                    ["curl"] = string.Create(CultureInfo.InvariantCulture, $@"curl -X POST ""{baseUrl}/api/v1/webhook-signatures/verify"" \
  -H ""Authorization: Bearer {apiKey}"" \
  -H ""Content-Type: application/json"" \
  -d '{{
    ""secret"": ""whsec_live_example_secret_key_8f9e"",
    ""signatureHeader"": ""t=1757270400,v1=6a8b9c..."",
    ""rawPayload"": ""{{\""orderId\"": \""ord_9981\""}}"",
    ""toleranceSeconds"": 300
  }}'"),
                    ["typescript"] = string.Create(CultureInfo.InvariantCulture, $@"import axios from 'axios';

async function testSignatureVerification() {{
  const res = await axios.post('{baseUrl}/api/v1/webhook-signatures/verify', {{
    secret: 'whsec_live_example_secret_key_8f9e',
    signatureHeader: 't=1757270400,v1=6a8b9c...',
    rawPayload: JSON.stringify({{ orderId: 'ord_9981' }}),
    toleranceSeconds: 300
  }}, {{
    headers: {{
      'Authorization': `Bearer {apiKey}`,
      'Content-Type': 'application/json'
    }}
  }});
  console.log('Verification result:', res.data.isValid);
}}"),
                    ["csharp"] = string.Create(CultureInfo.InvariantCulture, $@"using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(""Bearer"", ""{apiKey}"");

var body = new
{{
    secret = ""whsec_live_example_secret_key_8f9e"",
    signatureHeader = ""t=1757270400,v1=6a8b9c..."",
    rawPayload = ""{{\""""orderId\"""": \""""ord_9981\""""}}"",
    toleranceSeconds = 300
}};

var response = await client.PostAsJsonAsync(""{baseUrl}/api/v1/webhook-signatures/verify"", body);
var json = await response.Content.ReadAsStringAsync();
Console.WriteLine(json);")
                }
            )
        ];
    }

    private List<SdkRecipeDto> BuildSdkRecipes(string baseUrl, string apiKey, string secret)
    {
        return
        [
            new(
                Language: "typescript",
                DisplayName: "TypeScript / Node.js",
                Description: "Production-ready Webhook signature verification and Event publishing for Node.js, Express, Fastify, and Next.js.",
                InstallationCommand: "npm install axios express @types/express",
                VerificationSnippet: _snippetGenerator.GenerateSignatureVerificationSnippet("typescript", secret),
                PublishingSnippet: _snippetGenerator.GenerateEventPublishingSnippet("typescript", baseUrl, apiKey, "order.created", "{\"orderId\": \"ord_1001\", \"amount\": 199.99}"),
                Dependencies: ["crypto (built-in)", "express", "axios"]
            ),
            new(
                Language: "csharp",
                DisplayName: "C# / .NET 10",
                Description: "High-performance Webhook verifier and client for ASP.NET Core Minimal APIs and Controllers using System.Security.Cryptography.",
                InstallationCommand: "dotnet add package Microsoft.AspNetCore.App",
                VerificationSnippet: _snippetGenerator.GenerateSignatureVerificationSnippet("csharp", secret),
                PublishingSnippet: _snippetGenerator.GenerateEventPublishingSnippet("csharp", baseUrl, apiKey, "order.created", "{\"orderId\": \"ord_1001\", \"amount\": 199.99}"),
                Dependencies: ["System.Security.Cryptography (built-in)", "System.Net.Http.Json"]
            ),
            new(
                Language: "python",
                DisplayName: "Python 3",
                Description: "Clean signature verification and event publishing for FastAPI, Flask, and Django receivers.",
                InstallationCommand: "pip install requests fastapi uvicorn",
                VerificationSnippet: _snippetGenerator.GenerateSignatureVerificationSnippet("python", secret),
                PublishingSnippet: _snippetGenerator.GenerateEventPublishingSnippet("python", baseUrl, apiKey, "order.created", "{\"orderId\": \"ord_1001\", \"amount\": 199.99}"),
                Dependencies: ["hmac (built-in)", "hashlib (built-in)", "requests", "fastapi"]
            ),
            new(
                Language: "go",
                DisplayName: "Go (Golang)",
                Description: "Zero-dependency constant-time HMAC-SHA256 signature verification and HTTP webhook dispatcher.",
                InstallationCommand: "go get -u",
                VerificationSnippet: _snippetGenerator.GenerateSignatureVerificationSnippet("go", secret),
                PublishingSnippet: _snippetGenerator.GenerateEventPublishingSnippet("go", baseUrl, apiKey, "order.created", "{\"orderId\": \"ord_1001\", \"amount\": 199.99}"),
                Dependencies: ["crypto/hmac", "crypto/sha256", "crypto/subtle", "net/http"]
            ),
            new(
                Language: "php",
                DisplayName: "PHP 8.x",
                Description: "Simple hash_hmac and hash_equals webhook verification function for Laravel, Symfony, and vanilla PHP.",
                InstallationCommand: "composer require guzzlehttp/guzzle",
                VerificationSnippet: _snippetGenerator.GenerateSignatureVerificationSnippet("php", secret),
                PublishingSnippet: string.Create(CultureInfo.InvariantCulture, $@"<?php
require 'vendor/autoload.php';
use GuzzleHttp\Client;

$client = new Client();
$response = $client->post('{baseUrl}/api/v1/events/publish', [
    'headers' => [
        'Authorization' => 'Bearer {apiKey}',
        'Content-Type'  => 'application/json',
    ],
    'json' => [
        'eventType' => 'order.created',
        'payload'   => ['orderId' => 'ord_1001', 'amount' => 199.99],
    ]
]);

echo $response->getBody();"),
                Dependencies: ["ext-hash", "guzzlehttp/guzzle"]
            )
        ];
    }
}
