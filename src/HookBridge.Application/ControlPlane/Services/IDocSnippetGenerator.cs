namespace HookBridge.Application.ControlPlane.Services;

public interface IDocSnippetGenerator
{
    string GenerateSignatureVerificationSnippet(string language, string? secretPlaceholder = null);
    string GenerateEventPublishingSnippet(string language, string baseUrl, string apiKey, string eventType, string payloadJson);
    string GenerateEndpointRegistrationSnippet(string language, string baseUrl, string apiKey, string targetUrl, string description, string[] eventPatterns);
    string GenerateDeliveryReplaySnippet(string language, string baseUrl, string apiKey, Guid deliveryId);
    string GenerateGenericSnippet(string language, string method, string url, Dictionary<string, string>? headers, string? body);
}
