using COA.Mcp.Framework.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Services;

/// <summary>
/// Service that integrates with the framework's WebSocket transport for broadcasting notifications
/// </summary>
public class WebSocketBroadcastService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebSocketBroadcastService> _logger;
    private IMcpTransport? _webSocketTransport;
    
    public WebSocketBroadcastService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<WebSocketBroadcastService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }
    
    public bool IsWebSocketEnabled => 
        _configuration.GetValue<bool>("Mcp:Transport:WebSocket:Enabled", false);
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!IsWebSocketEnabled)
        {
            _logger.LogInformation("WebSocket broadcasting disabled in configuration");
            return;
        }
        
        // Get WebSocket transport from DI if available
        _webSocketTransport = _serviceProvider.GetService<IMcpTransport>();
        
        if (_webSocketTransport?.Type == TransportType.WebSocket)
        {
            _logger.LogInformation("WebSocket broadcast service connected to transport");
        }
        else
        {
            _logger.LogWarning("WebSocket transport not available for broadcasting");
        }
        
        await Task.CompletedTask;
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _webSocketTransport = null;
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Broadcasts a message to all connected WebSocket clients
    /// </summary>
    public async Task BroadcastAsync<T>(string messageType, T data) where T : class
    {
        if (_webSocketTransport == null || !IsWebSocketEnabled)
        {
            _logger.LogDebug("WebSocket broadcasting not available");
            return;
        }
        
        try
        {
            var payload = new
            {
                type = messageType,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                data = data
            };
            
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var message = new TransportMessage
            {
                Content = json,
                Headers = new Dictionary<string, string>
                {
                    ["message-type"] = messageType,
                    ["broadcast"] = "true"
                }
                // No connection-id means broadcast to all
            };
            
            await _webSocketTransport.WriteMessageAsync(message);
            
            _logger.LogDebug("Broadcast {MessageType} to all WebSocket connections", messageType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast {MessageType}", messageType);
        }
    }
    
    /// <summary>
    /// Sends a message to a specific WebSocket connection
    /// </summary>
    public async Task SendToConnectionAsync<T>(string connectionId, string messageType, T data) where T : class
    {
        if (_webSocketTransport == null || !IsWebSocketEnabled)
        {
            _logger.LogDebug("WebSocket messaging not available");
            return;
        }
        
        try
        {
            var payload = new
            {
                type = messageType,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                data = data
            };
            
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var message = new TransportMessage
            {
                Content = json,
                Headers = new Dictionary<string, string>
                {
                    ["message-type"] = messageType,
                    ["connection-id"] = connectionId
                }
            };
            
            await _webSocketTransport.WriteMessageAsync(message);
            
            _logger.LogDebug("Sent {MessageType} to connection {ConnectionId}", messageType, connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send {MessageType} to {ConnectionId}", messageType, connectionId);
        }
    }
}