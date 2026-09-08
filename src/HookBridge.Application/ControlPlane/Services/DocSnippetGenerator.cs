using System.Globalization;
using System.Text;
using System.Text.Json;

namespace HookBridge.Application.ControlPlane.Services;

public sealed class DocSnippetGenerator : IDocSnippetGenerator
{
    private static readonly string[] DefaultEventPatterns = ["order.*", "invoice.paid"];

    public string GenerateSignatureVerificationSnippet(string language, string? secretPlaceholder = null)
    {
        var secret = string.IsNullOrWhiteSpace(secretPlaceholder) ? "whsec_live_example_secret_key" : secretPlaceholder;

        return language.ToLowerInvariant() switch
        {
            "typescript" or "ts" or "javascript" or "js" or "node" => GenerateTypeScriptSignatureSnippet(secret),
            "csharp" or "c#" or "dotnet" or "net" => GenerateCSharpSignatureSnippet(secret),
            "python" or "py" => GeneratePythonSignatureSnippet(secret),
            "go" or "golang" => GenerateGoSignatureSnippet(secret),
            "php" => GeneratePhpSignatureSnippet(secret),
            _ => GenerateTypeScriptSignatureSnippet(secret)
        };
    }

    public string GenerateEventPublishingSnippet(string language, string baseUrl, string apiKey, string eventType, string payloadJson)
    {
        var formattedBaseUrl = baseUrl.TrimEnd('/');
        var key = string.IsNullOrWhiteSpace(apiKey) ? "hb_live_sample_apikey_12345" : apiKey;
        var type = string.IsNullOrWhiteSpace(eventType) ? "order.created" : eventType;
        var body = string.IsNullOrWhiteSpace(payloadJson) ? "{\"id\": \"ord_1001\", \"amount\": 149.99, \"currency\": \"USD\"}" : payloadJson;

        return language.ToLowerInvariant() switch
        {
            "curl" or "bash" or "shell" => GenerateCurlPublishSnippet(formattedBaseUrl, key, type, body),
            "typescript" or "ts" or "javascript" or "js" or "node" => GenerateTypeScriptPublishSnippet(formattedBaseUrl, key, type, body),
            "csharp" or "c#" or "dotnet" or "net" => GenerateCSharpPublishSnippet(formattedBaseUrl, key, type, body),
            "python" or "py" => GeneratePythonPublishSnippet(formattedBaseUrl, key, type, body),
            "go" or "golang" => GenerateGoPublishSnippet(formattedBaseUrl, key, type, body),
            _ => GenerateCurlPublishSnippet(formattedBaseUrl, key, type, body)
        };
    }

    public string GenerateEndpointRegistrationSnippet(string language, string baseUrl, string apiKey, string targetUrl, string description, string[] eventPatterns)
    {
        var formattedBaseUrl = baseUrl.TrimEnd('/');
        var key = string.IsNullOrWhiteSpace(apiKey) ? "hb_live_sample_apikey_12345" : apiKey;
        var target = string.IsNullOrWhiteSpace(targetUrl) ? "https://api.yourdomain.com/webhooks/hookbridge" : targetUrl;
        var desc = string.IsNullOrWhiteSpace(description) ? "Production Payment Receiver" : description;
        var patterns = (eventPatterns == null || eventPatterns.Length == 0) ? DefaultEventPatterns : eventPatterns;
        var patternsJson = JsonSerializer.Serialize(patterns);

        return language.ToLowerInvariant() switch
        {
            "curl" or "bash" or "shell" => string.Create(CultureInfo.InvariantCulture, $@"curl -X POST ""{formattedBaseUrl}/api/v1/endpoints"" \
  -H ""Authorization: Bearer {key}"" \
  -H ""Content-Type: application/json"" \
  -d '{{
    ""url"": ""{target}"",
    ""description"": ""{desc}"",
    ""rateLimitPerMinute"": 120,
    ""timeoutSeconds"": 15,
    ""eventTypes"": {patternsJson}
  }}'"),
            "typescript" or "ts" or "javascript" or "js" or "node" => string.Create(CultureInfo.InvariantCulture, $@"import axios from 'axios';

async function registerWebhookEndpoint() {{
  const response = await axios.post('{formattedBaseUrl}/api/v1/endpoints', {{
    url: '{target}',
    description: '{desc}',
    rateLimitPerMinute: 120,
    timeoutSeconds: 15,
    eventTypes: {patternsJson}
  }}, {{
    headers: {{
      'Authorization': `Bearer {key}`,
      'Content-Type': 'application/json'
    }}
  }});

  console.log('Endpoint registered:', response.data);
  console.log('Signing Secret:', response.data.signingSecret); // Store this securely!
  return response.data;
}}"),
            "csharp" or "c#" or "dotnet" => string.Create(CultureInfo.InvariantCulture, $@"using System.Net.Http.Json;
using System.Net.Http.Headers;

using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(""Bearer"", ""{key}"");

var requestBody = new
{{
    url = ""{target}"",
    description = ""{desc}"",
    rateLimitPerMinute = 120,
    timeoutSeconds = 15,
    eventTypes = new[] {{ {string.Join(", ", patterns.Select(p => $"\"{p}\""))} }}
}};

var response = await client.PostAsJsonAsync(""{formattedBaseUrl}/api/v1/endpoints"", requestBody);
response.EnsureSuccessStatusCode();

var result = await response.Content.ReadAsStringAsync();
Console.WriteLine($""Endpoint Created: {{result}}"");"),
            "python" or "py" => string.Create(CultureInfo.InvariantCulture, $@"import requests

url = '{formattedBaseUrl}/api/v1/endpoints'
headers = {{
    'Authorization': 'Bearer {key}',
    'Content-Type': 'application/json'
}}
payload = {{
    'url': '{target}',
    'description': '{desc}',
    'rateLimitPerMinute': 120,
    'timeoutSeconds': 15,
    'eventTypes': {patternsJson}
}}

response = requests.post(url, json=payload, headers=headers)
response.raise_for_status()

data = response.json()
print('Endpoint registered successfully:', data['id'])
print('Signing Secret:', data.get('signingSecret'))"),
            "go" or "golang" => string.Create(CultureInfo.InvariantCulture, $@"package main

import (
	""bytes""
	""encoding/json""
	""fmt""
	""io""
	""net/http""
)

func main() {{
	url := ""{formattedBaseUrl}/api/v1/endpoints""
	payload := map[string]interface_{{
		""url"": ""{target}"",
		""description"": ""{desc}"",
		""rateLimitPerMinute"": 120,
		""timeoutSeconds"": 15,
		""eventTypes"": []string{{{string.Join(", ", patterns.Select(p => $"\"{p}\""))}}},
	}}

	jsonBytes, _ := json.Marshal(payload)
	req, _ := http.NewRequest(""POST"", url, bytes.NewBuffer(jsonBytes))
	req.Header.Set(""Authorization"", ""Bearer {key}"")
	req.Header.Set(""Content-Type"", ""application/json"")

	client := &http.Client{{}}
	resp, err := client.Do(req)
	if err != nil {{
		panic(err)
	}}
	defer resp.Body.Close()

	body, _ := io.ReadAll(resp.Body)
	fmt.Printf(""Response: %s\n"", string(body))
}}"),
            _ => string.Create(CultureInfo.InvariantCulture, $@"curl -X POST ""{formattedBaseUrl}/api/v1/endpoints"" \
  -H ""Authorization: Bearer {key}"" \
  -H ""Content-Type: application/json"" \
  -d '{{
    ""url"": ""{target}"",
    ""description"": ""{desc}"",
    ""rateLimitPerMinute"": 120,
    ""timeoutSeconds"": 15,
    ""eventTypes"": {patternsJson}
  }}'")
        };
    }

    public string GenerateDeliveryReplaySnippet(string language, string baseUrl, string apiKey, Guid deliveryId)
    {
        var formattedBaseUrl = baseUrl.TrimEnd('/');
        var key = string.IsNullOrWhiteSpace(apiKey) ? "hb_live_sample_apikey_12345" : apiKey;
        var id = deliveryId == Guid.Empty ? Guid.NewGuid() : deliveryId;

        return language.ToLowerInvariant() switch
        {
            "curl" or "bash" or "shell" => string.Create(CultureInfo.InvariantCulture, $@"curl -X POST ""{formattedBaseUrl}/api/v1/deliveries/{id}/replay"" \
  -H ""Authorization: Bearer {key}"" \
  -H ""Content-Type: application/json"""),
            "typescript" or "ts" or "javascript" or "js" or "node" => string.Create(CultureInfo.InvariantCulture, $@"import axios from 'axios';

async function replayWebhookDelivery(deliveryId: string) {{
  const response = await axios.post(`{formattedBaseUrl}/api/v1/deliveries/${{deliveryId}}/replay`, {{}}, {{
    headers: {{
      'Authorization': `Bearer {key}`
    }}
  }});

  console.log('Replay initiated:', response.data);
  return response.data;
}}

replayWebhookDelivery('{id}');"),
            "csharp" or "c#" or "dotnet" => string.Create(CultureInfo.InvariantCulture, $@"using System.Net.Http.Headers;

using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(""Bearer"", ""{key}"");

var response = await client.PostAsync(""{formattedBaseUrl}/api/v1/deliveries/{id}/replay"", null);
response.EnsureSuccessStatusCode();

Console.WriteLine(""Delivery replay requested successfully."");"),
            "python" or "py" => string.Create(CultureInfo.InvariantCulture, $@"import requests

url = f'{formattedBaseUrl}/api/v1/deliveries/{id}/replay'
headers = {{'Authorization': 'Bearer {key}'}}

response = requests.post(url, headers=headers)
response.raise_for_status()

print('Replay response:', response.json())"),
            "go" or "golang" => string.Create(CultureInfo.InvariantCulture, $@"package main

import (
	""fmt""
	""net/http""
)

func main() {{
	url := ""{formattedBaseUrl}/api/v1/deliveries/{id}/replay""
	req, _ := http.NewRequest(""POST"", url, nil)
	req.Header.Set(""Authorization"", ""Bearer {key}"")

	resp, err := http.DefaultClient.Do(req)
	if err != nil {{
		panic(err)
	}}
	defer resp.Body.Close()

	fmt.Printf(""Replay Status: %d\n"", resp.StatusCode)
}}"),
            _ => string.Create(CultureInfo.InvariantCulture, $@"curl -X POST ""{formattedBaseUrl}/api/v1/deliveries/{id}/replay"" \
  -H ""Authorization: Bearer {key}""")
        };
    }

    public string GenerateGenericSnippet(string language, string method, string url, Dictionary<string, string>? headers, string? body)
    {
        var sb = new StringBuilder();
        var upperMethod = method.ToUpperInvariant();
        headers ??= new Dictionary<string, string>();

        switch (language.ToLowerInvariant())
        {
            case "curl" or "bash" or "shell":
                sb.Append(CultureInfo.InvariantCulture, $"curl -X {upperMethod} \"{url}\"");
                foreach (var (k, v) in headers)
                {
                    sb.Append(CultureInfo.InvariantCulture, $" \\\n  -H \"{k}: {v}\"");
                }
                if (!string.IsNullOrWhiteSpace(body) && upperMethod != "GET" && upperMethod != "DELETE")
                {
                    sb.Append(CultureInfo.InvariantCulture, $" \\\n  -d '{body}'");
                }
                return sb.ToString();

            case "typescript" or "ts" or "javascript" or "node":
                sb.AppendLine("import axios from 'axios';");
                sb.AppendLine();
                sb.AppendLine(CultureInfo.InvariantCulture, $"async function callApi() {{");
                sb.AppendLine(CultureInfo.InvariantCulture, $"  const response = await axios({{");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    method: '{upperMethod.ToLowerInvariant()}',");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    url: '{url}',");
                if (headers.Count > 0)
                {
                    sb.AppendLine("    headers: {");
                    foreach (var (k, v) in headers)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"      '{k}': '{v}',");
                    }
                    sb.AppendLine("    },");
                }
                if (!string.IsNullOrWhiteSpace(body) && upperMethod != "GET" && upperMethod != "DELETE")
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    data: {body}");
                }
                sb.AppendLine("  });");
                sb.AppendLine("  return response.data;");
                sb.AppendLine("}");
                return sb.ToString();

            case "csharp" or "c#" or "dotnet":
                sb.AppendLine("using System.Net.Http;");
                sb.AppendLine("using System.Text;");
                sb.AppendLine();
                sb.AppendLine("using var client = new HttpClient();");
                sb.AppendLine(CultureInfo.InvariantCulture, $"using var request = new HttpRequestMessage(HttpMethod.{ToHttpMethodName(upperMethod)}, \"{url}\");");
                foreach (var (k, v) in headers)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"request.Headers.TryAddWithoutValidation(\"{k}\", \"{v}\");");
                }
                if (!string.IsNullOrWhiteSpace(body) && upperMethod != "GET" && upperMethod != "DELETE")
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"request.Content = new StringContent(\"{body.Replace("\"", "\\\"")}\", Encoding.UTF8, \"application/json\");");
                }
                sb.AppendLine("var response = await client.SendAsync(request);");
                sb.AppendLine("response.EnsureSuccessStatusCode();");
                sb.AppendLine("var content = await response.Content.ReadAsStringAsync();");
                return sb.ToString();

            case "python" or "py":
                sb.AppendLine("import requests");
                sb.AppendLine();
                sb.AppendLine(CultureInfo.InvariantCulture, $"url = '{url}'");
                if (headers.Count > 0)
                {
                    sb.AppendLine("headers = {");
                    foreach (var (k, v) in headers)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"    '{k}': '{v}',");
                    }
                    sb.AppendLine("}");
                }
                else
                {
                    sb.AppendLine("headers = {}");
                }
                if (!string.IsNullOrWhiteSpace(body) && upperMethod != "GET" && upperMethod != "DELETE")
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"payload = {body}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"response = requests.{upperMethod.ToLowerInvariant()}(url, json=payload, headers=headers)");
                }
                else
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"response = requests.{upperMethod.ToLowerInvariant()}(url, headers=headers)");
                }
                sb.AppendLine("response.raise_for_status()");
                sb.AppendLine("print(response.json())");
                return sb.ToString();

            case "go" or "golang":
                sb.AppendLine("package main");
                sb.AppendLine();
                sb.AppendLine("import (");
                sb.AppendLine("    \"bytes\"");
                sb.AppendLine("    \"fmt\"");
                sb.AppendLine("    \"io\"");
                sb.AppendLine("    \"net/http\"");
                sb.AppendLine(")");
                sb.AppendLine();
                sb.AppendLine("func main() {");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    url := \"{url}\"");
                if (!string.IsNullOrWhiteSpace(body) && upperMethod != "GET" && upperMethod != "DELETE")
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    payload := []byte(`{body}`)");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    req, _ := http.NewRequest(\"{upperMethod}\", url, bytes.NewBuffer(payload))");
                }
                else
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    req, _ := http.NewRequest(\"{upperMethod}\", url, nil)");
                }
                foreach (var (k, v) in headers)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    req.Header.Set(\"{k}\", \"{v}\")");
                }
                sb.AppendLine("    resp, err := http.DefaultClient.Do(req)");
                sb.AppendLine("    if err != nil { panic(err) }");
                sb.AppendLine("    defer resp.Body.Close()");
                sb.AppendLine("    body, _ := io.ReadAll(resp.Body)");
                sb.AppendLine("    fmt.Println(string(body))");
                sb.AppendLine("}");
                return sb.ToString();

            default:
                return GenerateGenericSnippet("curl", method, url, headers, body);
        }
    }

    private static string ToHttpMethodName(string method) => method.ToUpperInvariant() switch
    {
        "GET" => "Get",
        "POST" => "Post",
        "PUT" => "Put",
        "DELETE" => "Delete",
        "PATCH" => "Patch",
        "HEAD" => "Head",
        "OPTIONS" => "Options",
        _ => "Post"
    };

    private static string GenerateTypeScriptSignatureSnippet(string secret) => string.Create(CultureInfo.InvariantCulture, $@"import crypto from 'crypto';
import type {{ Request, Response, NextFunction }} from 'express';

/**
 * Verifies HookBridge HMAC-SHA256 signature on incoming webhooks.
 * Header format: X-HookBridge-Signature: t=<timestamp>,v1=<signature>[,v1=<rotating_signature>]
 *
 * @param rawBody - Raw unparsed HTTP request body string or Buffer (never parsed JSON!)
 * @param signatureHeader - The 'X-HookBridge-Signature' HTTP request header
 * @param secret - Your endpoint webhook secret (e.g. whsec_...)
 * @param toleranceSeconds - Maximum allowed clock skew (default 300 seconds / 5 mins)
 */
export function verifyHookBridgeSignature(
  rawBody: string | Buffer,
  signatureHeader: string | undefined,
  secret: string = '{secret}',
  toleranceSeconds: number = 300
): boolean {{
  if (!signatureHeader || !rawBody) {{
    return false;
  }}

  // 1. Parse header components: t=timestamp and v1=signature
  const elements = signatureHeader.split(',');
  let timestampStr: string | null = null;
  const signatures: string[] = [];

  for (const element of elements) {{
    const [key, value] = element.trim().split('=');
    if (key === 't') {{
      timestampStr = value;
    }} else if (key === 'v1') {{
      signatures.push(value);
    }}
  }}

  if (!timestampStr || signatures.length === 0) {{
    return false;
  }}

  // 2. Anti-Replay Attack Protection: verify timestamp within tolerance window
  const timestamp = parseInt(timestampStr, 10);
  if (isNaN(timestamp)) return false;

  const currentTimestamp = Math.floor(Date.now() / 1000);
  if (Math.abs(currentTimestamp - timestamp) > toleranceSeconds) {{
    console.warn(`[HookBridge] Webhook rejected: timestamp ${{timestamp}} outside ${{toleranceSeconds}}s window.`);
    return false;
  }}

  // 3. Compute HMAC-SHA256 on canonical payload: `${{timestamp}}.${{rawBody}}`
  const canonicalPayload = `${{timestampStr}}.${{typeof rawBody === 'string' ? rawBody : rawBody.toString('utf8')}}`;
  const computedSignature = crypto
    .createHmac('sha256', secret)
    .update(canonicalPayload, 'utf8')
    .digest('hex');

  const computedBuffer = Buffer.from(computedSignature, 'hex');

  // 4. Constant-time comparison against all received v1 signatures (supports secret rotation)
  for (const sig of signatures) {{
    try {{
      const sigBuffer = Buffer.from(sig, 'hex');
      if (computedBuffer.length === sigBuffer.length && crypto.timingSafeEqual(computedBuffer, sigBuffer)) {{
        return true;
      }}
    }} catch {{
      // Ignore length mismatches
    }}
  }}

  return false;
}}

// Express.js Middleware Example:
export function hookBridgeMiddleware(req: Request, res: Response, next: NextFunction) {{
  const sigHeader = req.headers['x-hookbridge-signature'] as string;
  const rawBody = (req as any).rawBody || JSON.stringify(req.body);

  if (!verifyHookBridgeSignature(rawBody, sigHeader, process.env.HOOKBRIDGE_SECRET || '{secret}')) {{
    return res.status(401).json({{ error: 'Invalid HookBridge HMAC signature' }});
  }}

  next();
}}");

    private static string GenerateCSharpSignatureSnippet(string secret) => string.Create(CultureInfo.InvariantCulture, $@"using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace HookBridge.Sdk;

public static class WebhookVerifier
{{
    private const string SignatureHeaderName = ""X-HookBridge-Signature"";

    /// <summary>
    /// Verifies the HookBridge HMAC-SHA256 signature for incoming webhooks.
    /// Format: X-HookBridge-Signature: t=timestamp,v1=signature
    /// </summary>
    public static bool VerifySignature(
        string rawBody,
        string? signatureHeader,
        string secret = ""{secret}"",
        int toleranceSeconds = 300)
    {{
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrEmpty(rawBody))
            return false;

        // 1. Parse header components (t=timestamp, v1=signatures)
        string? timestampStr = null;
        var signatures = new List<string>();

        var parts = signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {{
            var kvp = part.Split('=', 2);
            if (kvp.Length != 2) continue;

            if (kvp[0] == ""t"")
                timestampStr = kvp[1];
            else if (kvp[0] == ""v1"")
                signatures.Add(kvp[1]);
        }}

        if (timestampStr == null || signatures.Count == 0 || !long.TryParse(timestampStr, out var timestamp))
            return false;

        // 2. Anti-Replay Protection: Validate timestamp skew
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp) > toleranceSeconds)
            return false;

        // 3. Compute HMAC-SHA256 of canonical payload: $""{{timestamp}}.{{rawBody}}""
        var canonicalPayload = $""{{timestampStr}}.{{rawBody}}"";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalPayload));
        var computedHex = Convert.ToHexStringLower(computedHash);
        var computedBytes = Encoding.UTF8.GetBytes(computedHex);

        // 4. Constant-time comparison
        foreach (var sig in signatures)
        {{
            var sigBytes = Encoding.UTF8.GetBytes(sig.ToLowerInvariant());
            if (CryptographicOperations.FixedTimeEquals(computedBytes, sigBytes))
                return true;
        }}

        return false;
    }}
}}

// ASP.NET Core Minimal API Example:
// app.MapPost(""/api/webhooks"", async (HttpRequest request) =>
// {{
//     using var reader = new StreamReader(request.Body, Encoding.UTF8);
//     var rawBody = await reader.ReadToEndAsync();
//     var sig = request.Headers[""X-HookBridge-Signature""].ToString();
//     if (!WebhookVerifier.VerifySignature(rawBody, sig, ""{secret}""))
//         return Results.Unauthorized();
//     return Results.Ok(new {{ status = ""processed"" }});
// }});");

    private static string GeneratePythonSignatureSnippet(string secret) => string.Create(CultureInfo.InvariantCulture, $@"import hmac
import hashlib
import time
from typing import Optional

def verify_hookbridge_signature(
    raw_body: str,
    signature_header: Optional[str],
    secret: str = '{secret}',
    tolerance_seconds: int = 300
) -> bool:
    """"""
    Verifies HookBridge HMAC-SHA256 signature.
    Header format: X-HookBridge-Signature: t=<timestamp>,v1=<signature>
    """"""
    if not signature_header or not raw_body:
        return False

    # 1. Parse timestamp and v1 signatures
    timestamp_str = None
    signatures = []

    for element in signature_header.split(','):
        parts = element.strip().split('=', 1)
        if len(parts) == 2:
            k, v = parts[0], parts[1]
            if k == 't':
                timestamp_str = v
            elif k == 'v1':
                signatures.append(v)

    if not timestamp_str or not signatures:
        return False

    # 2. Anti-Replay Protection: Validate timestamp
    try:
        timestamp = int(timestamp_str)
    except ValueError:
        return False

    if abs(int(time.time()) - timestamp) > tolerance_seconds:
        return False

    # 3. Canonical payload: f""{{timestamp}}.{{raw_body}}""
    canonical = f""{{timestamp_str}}.{{raw_body}}""
    computed_signature = hmac.new(
        secret.encode('utf-8'),
        canonical.encode('utf-8'),
        hashlib.sha256
    ).hexdigest()

    # 4. Constant-time comparison
    for sig in signatures:
        if hmac.compare_digest(computed_signature, sig):
            return True

    return False

# FastAPI Example:
# from fastapi import FastAPI, Request, HTTPException, status
# app = FastAPI()
# @app.post('/webhooks/hookbridge')
# async def receive_webhook(request: Request):
#     body_bytes = await request.body()
#     raw_body = body_bytes.decode('utf-8')
#     sig_header = request.headers.get('X-HookBridge-Signature')
#     if not verify_hookbridge_signature(raw_body, sig_header, '{secret}'):
#         raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail='Invalid signature')
#     return {{'status': 'received'}}");

    private static string GenerateGoSignatureSnippet(string secret) => string.Create(CultureInfo.InvariantCulture, $@"package hookbridge

import (
	""crypto/hmac""
	""crypto/sha256""
	""crypto/subtle""
	""encoding/hex""
	""fmt""
	""math""
	""strconv""
	""strings""
	""time""
)

// VerifySignature validates the incoming HookBridge HMAC-SHA256 signature.
// Header format: X-HookBridge-Signature: t=<timestamp>,v1=<signature>
func VerifySignature(rawBody string, signatureHeader string, secret string, toleranceSeconds int64) bool {{
	if signatureHeader == """" || rawBody == """" {{
		return false
	}}
	if secret == """" {{
		secret = ""{secret}""
	}}
	if toleranceSeconds <= 0 {{
		toleranceSeconds = 300
	}}

	// 1. Parse header components
	var timestampStr string
	var signatures []string

	for _, item := range strings.Split(signatureHeader, "","") {{
		parts := strings.SplitN(strings.TrimSpace(item), ""="", 2)
		if len(parts) == 2 {{
			if parts[0] == ""t"" {{
				timestampStr = parts[1]
			}} else if parts[0] == ""v1"" {{
				signatures = append(signatures, parts[1])
			}}
		}}
	}}

	if timestampStr == """" || len(signatures) == 0 {{
		return false
	}}

	timestamp, err := strconv.ParseInt(timestampStr, 10, 64)
	if err != nil {{
		return false
	}}

	// 2. Anti-Replay: Check clock skew
	now := time.Now().Unix()
	if math.Abs(float64(now-timestamp)) > float64(toleranceSeconds) {{
		return false
	}}

	// 3. Compute HMAC-SHA256 of canonical payload
	canonical := fmt.Sprintf(""%s.%s"", timestampStr, rawBody)
	mac := hmac.New(sha256.New, []byte(secret))
	mac.Write([]byte(canonical))
	computedHex := hex.EncodeToString(mac.Sum(nil))

	// 4. Constant-time comparison
	for _, sig := range signatures {{
		if subtle.ConstantTimeCompare([]byte(computedHex), []byte(sig)) == 1 {{
			return true
		}}
	}}

	return false
}}");

    private static string GeneratePhpSignatureSnippet(string secret) => string.Create(CultureInfo.InvariantCulture, $@"<?php

/**
 * Verifies HookBridge HMAC-SHA256 signature.
 * Header format: X-HookBridge-Signature: t=<timestamp>,v1=<signature>
 */
function verifyHookBridgeSignature(string $rawBody, ?string $signatureHeader, string $secret = '{secret}', int $toleranceSeconds = 300): bool {{
    if (empty($signatureHeader) || empty($rawBody)) {{
        return false;
    }}

    // 1. Parse t and v1
    $elements = explode(',', $signatureHeader);
    $timestamp = null;
    $signatures = [];

    foreach ($elements as $element) {{
        $parts = explode('=', trim($element), 2);
        if (count($parts) === 2) {{
            if ($parts[0] === 't') {{
                $timestamp = $parts[1];
            }} elseif ($parts[0] === 'v1') {{
                $signatures[] = $parts[1];
            }}
        }}
    }}

    if ($timestamp === null || empty($signatures) || !is_numeric($timestamp)) {{
        return false;
    }}

    // 2. Anti-Replay
    if (abs(time() - (int)$timestamp) > $toleranceSeconds) {{
        return false;
    }}

    // 3. Compute HMAC-SHA256
    $canonical = $timestamp . '.' . $rawBody;
    $computed = hash_hmac('sha256', $canonical, $secret);

    // 4. Constant-time comparison
    foreach ($signatures as $sig) {{
        if (hash_equals($computed, $sig)) {{
            return true;
        }}
    }}

    return false;
}}
");

    private static string GenerateCurlPublishSnippet(string baseUrl, string key, string type, string body) =>
        string.Create(CultureInfo.InvariantCulture, $@"curl -X POST ""{baseUrl}/api/v1/events/publish"" \
  -H ""Authorization: Bearer {key}"" \
  -H ""Content-Type: application/json"" \
  -d '{{
    ""eventType"": ""{type}"",
    ""payload"": {body}
  }}'");

    private static string GenerateTypeScriptPublishSnippet(string baseUrl, string key, string type, string body) =>
        string.Create(CultureInfo.InvariantCulture, $@"import axios from 'axios';

async function publishWebhookEvent() {{
  const response = await axios.post('{baseUrl}/api/v1/events/publish', {{
    eventType: '{type}',
    payload: {body}
  }}, {{
    headers: {{
      'Authorization': `Bearer {key}`,
      'Content-Type': 'application/json'
    }}
  }});

  console.log('Event Published:', response.data);
  return response.data;
}}

publishWebhookEvent();");

    private static string GenerateCSharpPublishSnippet(string baseUrl, string key, string type, string body) =>
        string.Create(CultureInfo.InvariantCulture, $@"using System.Net.Http.Json;
using System.Net.Http.Headers;

using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(""Bearer"", ""{key}"");

var request = new
{{
    eventType = ""{type}"",
    payload = {body}
}};

var response = await client.PostAsJsonAsync(""{baseUrl}/api/v1/events/publish"", request);
response.EnsureSuccessStatusCode();

var result = await response.Content.ReadAsStringAsync();
Console.WriteLine($""Event Published: {{result}}"");");

    private static string GeneratePythonPublishSnippet(string baseUrl, string key, string type, string body) =>
        string.Create(CultureInfo.InvariantCulture, $@"import requests

url = '{baseUrl}/api/v1/events/publish'
headers = {{
    'Authorization': 'Bearer {key}',
    'Content-Type': 'application/json'
}}
payload = {{
    'eventType': '{type}',
    'payload': {body}
}}

response = requests.post(url, json=payload, headers=headers)
response.raise_for_status()

print('Event published successfully:', response.json())");

    private static string GenerateGoPublishSnippet(string baseUrl, string key, string type, string body) =>
        string.Create(CultureInfo.InvariantCulture, $@"package main

import (
	""bytes""
	""encoding/json""
	""fmt""
	""io""
	""net/http""
)

func main() {{
	url := ""{baseUrl}/api/v1/events/publish""
	data := map[string]interface_{{
		""eventType"": ""{type}"",
		""payload"": {body},
	}}

	jsonBytes, _ := json.Marshal(data)
	req, _ := http.NewRequest(""POST"", url, bytes.NewBuffer(jsonBytes))
	req.Header.Set(""Authorization"", ""Bearer {key}"")
	req.Header.Set(""Content-Type"", ""application/json"")

	resp, err := http.DefaultClient.Do(req)
	if err != nil {{
		panic(err)
	}}
	defer resp.Body.Close()

	body, _ := io.ReadAll(resp.Body)
	fmt.Printf(""Published Result: %s\n"", string(body))
}}");
}
