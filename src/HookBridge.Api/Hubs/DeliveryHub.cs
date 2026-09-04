using System.Security.Claims;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time delivery lifecycle events and live inspection, strictly scoped to tenant security groups.
/// </summary>
[Authorize]
public partial class DeliveryHub : Hub<IDeliveryHubClient>
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ILogger<DeliveryHub> _logger;

    public const string TenantIdItemKey = "TenantId";

    public DeliveryHub(IHookBridgeDbContext dbContext, ILogger<DeliveryHub> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Rejecting SignalR connection {ConnectionId} due to missing or invalid tenant claim.")]
    private static partial void LogRejectingConnection(ILogger logger, string connectionId);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "SignalR client {ConnectionId} connected and joined tenant group {TenantGroup}.")]
    private static partial void LogClientConnected(ILogger logger, string connectionId, string tenantGroup);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "SignalR client {ConnectionId} disconnected from tenant group {TenantGroup}.")]
    private static partial void LogClientDisconnected(ILogger logger, string connectionId, string tenantGroup);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "Client {ConnectionId} attempted to subscribe to unauthorized or non-existent endpoint {EndpointId}.")]
    private static partial void LogUnauthorizedEndpointSub(ILogger logger, string connectionId, Guid endpointId);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Debug, Message = "Client {ConnectionId} subscribed to endpoint group {GroupName}.")]
    private static partial void LogEndpointSubscribed(ILogger logger, string connectionId, string groupName);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Debug, Message = "Client {ConnectionId} unsubscribed from endpoint group {GroupName}.")]
    private static partial void LogEndpointUnsubscribed(ILogger logger, string connectionId, string groupName);

    [LoggerMessage(EventId = 2007, Level = LogLevel.Warning, Message = "Client {ConnectionId} attempted to subscribe to unauthorized or non-existent application {ApplicationId}.")]
    private static partial void LogUnauthorizedAppSub(ILogger logger, string connectionId, Guid applicationId);

    [LoggerMessage(EventId = 2008, Level = LogLevel.Debug, Message = "Client {ConnectionId} subscribed to application group {GroupName}.")]
    private static partial void LogAppSubscribed(ILogger logger, string connectionId, string groupName);

    [LoggerMessage(EventId = 2009, Level = LogLevel.Debug, Message = "Client {ConnectionId} unsubscribed from application group {GroupName}.")]
    private static partial void LogAppUnsubscribed(ILogger logger, string connectionId, string groupName);

    public static string GetTenantGroup(Guid tenantId) => $"tenant:{tenantId:D}";
    public static string GetEndpointGroup(Guid tenantId, Guid endpointId) => $"tenant:{tenantId:D}:endpoint:{endpointId:D}";
    public static string GetAppGroup(Guid tenantId, Guid applicationId) => $"tenant:{tenantId:D}:app:{applicationId:D}";

    public override async Task OnConnectedAsync()
    {
        var tenantId = GetAuthenticatedTenantId();
        if (!tenantId.HasValue)
        {
            LogRejectingConnection(_logger, Context.ConnectionId);
            Context.Abort();
            return;
        }

        Context.Items[TenantIdItemKey] = tenantId.Value;

        var tenantGroup = GetTenantGroup(tenantId.Value);
        await Groups.AddToGroupAsync(Context.ConnectionId, tenantGroup);

        HookBridgeDiagnostics.ActiveSignalRConnections.Add(1, new KeyValuePair<string, object?>("tenant.id", tenantId.Value.ToString()));

        LogClientConnected(_logger, Context.ConnectionId, tenantGroup);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (tenantId.HasValue)
        {
            HookBridgeDiagnostics.ActiveSignalRConnections.Add(-1, new KeyValuePair<string, object?>("tenant.id", tenantId.Value.ToString()));
            LogClientDisconnected(_logger, Context.ConnectionId, GetTenantGroup(tenantId.Value));
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Allows the authenticated client to subscribe to real-time events for a specific endpoint belonging to their tenant.
    /// </summary>
    public async Task<bool> SubscribeToEndpoint(Guid endpointId)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (!tenantId.HasValue)
        {
            return false;
        }

        var endpointExists = await _dbContext.Endpoints
            .AnyAsync(e => e.Id == endpointId && e.TenantId == tenantId.Value);

        if (!endpointExists)
        {
            LogUnauthorizedEndpointSub(_logger, Context.ConnectionId, endpointId);
            return false;
        }

        var groupName = GetEndpointGroup(tenantId.Value, endpointId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        LogEndpointSubscribed(_logger, Context.ConnectionId, groupName);
        return true;
    }

    /// <summary>
    /// Allows the authenticated client to unsubscribe from a specific endpoint.
    /// </summary>
    public async Task<bool> UnsubscribeFromEndpoint(Guid endpointId)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (!tenantId.HasValue)
        {
            return false;
        }

        var groupName = GetEndpointGroup(tenantId.Value, endpointId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        LogEndpointUnsubscribed(_logger, Context.ConnectionId, groupName);
        return true;
    }

    /// <summary>
    /// Allows the authenticated client to subscribe to real-time events for an application belonging to their tenant.
    /// </summary>
    public async Task<bool> SubscribeToApplication(Guid applicationId)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (!tenantId.HasValue)
        {
            return false;
        }

        var appExists = await _dbContext.Applications
            .AnyAsync(a => a.Id == applicationId && a.TenantId == tenantId.Value);

        if (!appExists)
        {
            LogUnauthorizedAppSub(_logger, Context.ConnectionId, applicationId);
            return false;
        }

        var groupName = GetAppGroup(tenantId.Value, applicationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        LogAppSubscribed(_logger, Context.ConnectionId, groupName);
        return true;
    }

    /// <summary>
    /// Allows the authenticated client to unsubscribe from an application.
    /// </summary>
    public async Task<bool> UnsubscribeFromApplication(Guid applicationId)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (!tenantId.HasValue)
        {
            return false;
        }

        var groupName = GetAppGroup(tenantId.Value, applicationId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        LogAppUnsubscribed(_logger, Context.ConnectionId, groupName);
        return true;
    }

    private Guid? GetAuthenticatedTenantId()
    {
        if (Context.Items.TryGetValue(TenantIdItemKey, out var cached) && cached is Guid cachedId)
        {
            return cachedId;
        }

        var claim = Context.User?.FindFirst("tenant_id")
            ?? Context.User?.FindFirst("TenantId");

        if (claim != null && Guid.TryParse(claim.Value, out var tenantId))
        {
            return tenantId;
        }

        return null;
    }
}
