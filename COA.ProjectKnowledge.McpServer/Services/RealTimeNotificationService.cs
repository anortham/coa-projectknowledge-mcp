using COA.ProjectKnowledge.McpServer.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Services;

/// <summary>
/// Handles real-time notifications for knowledge changes over WebSocket connections.
/// Broadcasts knowledge updates, new items, and other events to connected clients.
/// </summary>
public class RealTimeNotificationService : IRealTimeNotificationService
{
    private readonly ILogger<RealTimeNotificationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly WebSocketBroadcastService? _webSocketService;
    
    public RealTimeNotificationService(
        ILogger<RealTimeNotificationService> logger,
        IConfiguration configuration,
        WebSocketBroadcastService? webSocketService = null)
    {
        _logger = logger;
        _configuration = configuration;
        _webSocketService = webSocketService;
    }

    public bool IsEnabled => _configuration.GetValue<bool>("Mcp:Features:RealTimeUpdates", true);

    // Interface implementation methods
    public async Task NotifyKnowledgeCreatedAsync(Knowledge knowledge)
    {
        var item = new KnowledgeSearchItem
        {
            Id = knowledge.Id,
            Type = knowledge.Type,
            Content = knowledge.Content,
            Tags = knowledge.Tags
        };
        await BroadcastKnowledgeCreatedAsync(item, knowledge.Workspace);
    }

    public async Task NotifyKnowledgeUpdatedAsync(Knowledge knowledge)
    {
        var item = new KnowledgeSearchItem
        {
            Id = knowledge.Id,
            Type = knowledge.Type,
            Content = knowledge.Content,
            Tags = knowledge.Tags
        };
        await BroadcastKnowledgeUpdatedAsync(item, knowledge.Workspace);
    }

    public async Task NotifyKnowledgeDeletedAsync(string knowledgeId)
    {
        await BroadcastKnowledgeDeletedAsync(knowledgeId, "default");
    }

    public async Task NotifyCheckpointCreatedAsync(string sessionId, int sequenceNumber)
    {
        await BroadcastCheckpointCreatedAsync($"checkpoint-{sessionId}-{sequenceNumber}", sessionId, "default");
    }

    public async Task NotifyChecklistItemCompletedAsync(string checklistId, string itemId)
    {
        // Placeholder - implement if needed
        await Task.CompletedTask;
    }

    public async Task NotifyRelationshipCreatedAsync(string fromId, string toId, string relationshipType)
    {
        // Placeholder - implement if needed
        await Task.CompletedTask;
    }

    /// <summary>
    /// Broadcasts knowledge item creation to all connected clients
    /// </summary>
    public async Task BroadcastKnowledgeCreatedAsync(KnowledgeSearchItem item, string workspace)
    {
        if (!IsEnabled) return;

        var notification = new KnowledgeNotification
        {
            Type = "knowledge_created",
            KnowledgeId = item.Id,
            KnowledgeType = item.Type,
            Workspace = workspace,
            Content = TruncateContent(item.Content),
            Tags = item.Tags,
            Timestamp = DateTime.UtcNow
        };

        await BroadcastNotificationAsync(notification);
    }

    /// <summary>
    /// Broadcasts knowledge item updates to all connected clients
    /// </summary>
    public async Task BroadcastKnowledgeUpdatedAsync(KnowledgeSearchItem item, string workspace)
    {
        if (!IsEnabled) return;

        var notification = new KnowledgeNotification
        {
            Type = "knowledge_updated",
            KnowledgeId = item.Id,
            KnowledgeType = item.Type,
            Workspace = workspace,
            Content = TruncateContent(item.Content),
            Tags = item.Tags,
            Timestamp = DateTime.UtcNow
        };

        await BroadcastNotificationAsync(notification);
    }

    /// <summary>
    /// Broadcasts knowledge item deletion to all connected clients
    /// </summary>
    public async Task BroadcastKnowledgeDeletedAsync(string knowledgeId, string workspace)
    {
        if (!IsEnabled) return;

        var notification = new KnowledgeNotification
        {
            Type = "knowledge_deleted",
            KnowledgeId = knowledgeId,
            Workspace = workspace,
            Timestamp = DateTime.UtcNow
        };

        await BroadcastNotificationAsync(notification);
    }

    /// <summary>
    /// Broadcasts checkpoint creation to all connected clients
    /// </summary>
    public async Task BroadcastCheckpointCreatedAsync(string checkpointId, string sessionId, string workspace)
    {
        if (!IsEnabled) return;

        var notification = new KnowledgeNotification
        {
            Type = "checkpoint_created",
            KnowledgeId = checkpointId,
            KnowledgeType = "Checkpoint",
            Workspace = workspace,
            SessionId = sessionId,
            Timestamp = DateTime.UtcNow
        };

        await BroadcastNotificationAsync(notification);
    }

    /// <summary>
    /// Broadcasts workspace activity updates
    /// </summary>
    public async Task BroadcastWorkspaceActivityAsync(string workspace, string activityType, object data)
    {
        if (!IsEnabled) return;

        var notification = new WorkspaceActivityNotification
        {
            Type = "workspace_activity",
            Workspace = workspace,
            ActivityType = activityType,
            Data = data,
            Timestamp = DateTime.UtcNow
        };

        await BroadcastNotificationAsync(notification);
    }

    private async Task BroadcastNotificationAsync(object notification)
    {
        if (!IsEnabled) return;

        try
        {
            // Determine the message type based on the notification
            var messageType = notification switch
            {
                KnowledgeNotification kn => kn.Type,
                WorkspaceActivityNotification wan => wan.Type,
                _ => "notification"
            };

            // If WebSocket service is available and enabled, broadcast through it
            if (_webSocketService?.IsWebSocketEnabled == true)
            {
                await _webSocketService.BroadcastAsync(messageType, notification);
                _logger.LogDebug("Broadcast {NotificationType} via WebSocket", notification.GetType().Name);
            }
            else
            {
                // Fallback to logging if WebSocket is not available
                _logger.LogDebug("WebSocket not available, would broadcast: {NotificationType}", 
                    notification.GetType().Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error broadcasting notification");
        }
    }

    private static string TruncateContent(string? content, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        if (content.Length <= maxLength) return content;
        return content[..maxLength] + "...";
    }

}

/// <summary>
/// Notification model for knowledge changes
/// </summary>
public class KnowledgeNotification
{
    public string Type { get; set; } = "";
    public string KnowledgeId { get; set; } = "";
    public string KnowledgeType { get; set; } = "";
    public string Workspace { get; set; } = "";
    public string? Content { get; set; }
    public string[]? Tags { get; set; }
    public string? SessionId { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Notification model for workspace activity
/// </summary>
public class WorkspaceActivityNotification
{
    public string Type { get; set; } = "";
    public string Workspace { get; set; } = "";
    public string ActivityType { get; set; } = "";
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; }
}