namespace HookBridge.Infrastructure.Integration;

public sealed class EventFlowOptions
{
    public const string SectionName = "EventFlow";

    /// <summary>
    /// Base URL of the EventFlow Data Plane HTTP Ingestion API.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// API Key for authenticating against EventFlow endpoints (sent in X-Api-Key header).
    /// </summary>
    public string ApiKey { get; set; } = "eventflow_development_secret_api_key_2026";

    /// <summary>
    /// HTTP timeout in seconds for EventFlow integration requests.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}
