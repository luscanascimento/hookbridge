using System.ComponentModel.DataAnnotations;

namespace HookBridge.Infrastructure.Integration;

public sealed class EventFlowOptions
{
    public const string SectionName = "EventFlow";

    /// <summary>
    /// Base URL of the EventFlow Data Plane HTTP Ingestion API.
    /// </summary>
    [Required(ErrorMessage = "EventFlow:BaseUrl is required.")]
    [Url(ErrorMessage = "EventFlow:BaseUrl must be a valid HTTP/HTTPS URL.")]
    public string BaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// API Key for authenticating against EventFlow endpoints (sent in X-Api-Key header).
    /// </summary>
    [Required(ErrorMessage = "EventFlow:ApiKey is required.")]
    [MinLength(8, ErrorMessage = "EventFlow:ApiKey must be at least 8 characters long.")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// HTTP timeout in seconds for EventFlow integration requests.
    /// </summary>
    [Range(1, 120, ErrorMessage = "EventFlow:TimeoutSeconds must be between 1 and 120 seconds.")]
    public int TimeoutSeconds { get; set; } = 10;
}
