using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.TokenOptimization.Caching;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Constants;
using COA.ProjectKnowledge.McpServer.Helpers;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Use framework attributes with aliases to avoid conflicts
using FrameworkAttributes = COA.Mcp.Framework.Attributes;
using ComponentModel = System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class ListCheckpointsTool : McpToolBase<ListCheckpointsParams, ListCheckpointsResult>
{
    private readonly CheckpointService _checkpointService;
    private readonly IResponseCacheService _cacheService;
    private readonly ExecutionContextService _contextService;
    private readonly ILogger<ListCheckpointsTool> _logger;
    
    public ListCheckpointsTool(
        CheckpointService checkpointService,
        IResponseCacheService cacheService,
        ExecutionContextService contextService,
        ILogger<ListCheckpointsTool> logger)
    {
        _checkpointService = checkpointService;
        _cacheService = cacheService;
        _contextService = contextService;
        _logger = logger;
    }
    
    public override string Name => ToolNames.ListCheckpoints;
    public override string Description => "List checkpoints for a session";
    public override ToolCategory Category => ToolCategory.Query;

    protected override async Task<ListCheckpointsResult> ExecuteInternalAsync(ListCheckpointsParams parameters, CancellationToken cancellationToken)
    {
        // Create execution context for tracking
        var customData = new Dictionary<string, object?>
        {
            ["SessionId"] = parameters.SessionId,
            ["MaxResults"] = parameters.MaxResults ?? 20
        };
        
        return await _contextService.RunWithContextAsync(
            Name,
            async (context) => await ExecuteWithContextAsync(parameters, context, cancellationToken),
            customData: customData);
    }
    
    private async Task<ListCheckpointsResult> ExecuteWithContextAsync(
        ListCheckpointsParams parameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Generate cache key
        var cacheKey = GenerateCacheKey(parameters);
        
        // Check cache first - short cache since checkpoints can be added frequently
        if (_cacheService != null)
        {
            var cachedResult = await _cacheService.GetAsync<ListCheckpointsResult>(cacheKey);
            if (cachedResult != null)
            {
                _logger.LogDebug("Returning cached checkpoint list for key: {CacheKey}", cacheKey);
                context.CustomData["CacheHit"] = true;
                return cachedResult;
            }
        }
        
        try
        {
            var checkpoints = await _checkpointService.ListCheckpointsAsync(
                parameters.SessionId,
                parameters.MaxResults ?? 20);
            
            var result = new ListCheckpointsResult
            {
                Success = true,
                Checkpoints = checkpoints.Select(c => new CheckpointSummary
                {
                    Id = c.Id,
                    Content = c.Content.Length > 200 ? c.Content.Substring(0, 197) + "..." : c.Content,
                    SessionId = c.SessionId,
                    SequenceNumber = c.SequenceNumber,
                    ActiveFiles = c.ActiveFiles,
                    CreatedAt = c.CreatedAt
                }).ToList(),
                TotalCount = checkpoints.Count
            };
            
            // Cache the result for 2 minutes (short cache since new checkpoints may be added)
            if (_cacheService != null)
            {
                try
                {
                    await _cacheService.SetAsync(cacheKey, result, new CacheEntryOptions());
                    context.CustomData["CacheSet"] = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache checkpoint list for key: {CacheKey}", cacheKey);
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list checkpoints");
            context.CustomData["Error"] = true;
            
            return new ListCheckpointsResult
            {
                Success = false,
                Error = ErrorHelpers.CreateCheckpointError($"Failed to list checkpoints: {ex.Message}", "list")
            };
        }
    }
    
    private string GenerateCacheKey(ListCheckpointsParams parameters)
    {
        var keyData = new
        {
            Tool = Name,
            SessionId = parameters.SessionId,
            MaxResults = parameters.MaxResults ?? 20
        };
        
        var json = JsonSerializer.Serialize(keyData);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return $"checkpointlist_{Convert.ToBase64String(hash).Replace("/", "_").Replace("+", "-").Substring(0, 16)}";
    }
}

public class ListCheckpointsParams
{
    [FrameworkAttributes.Required(ErrorMessage = "Session ID is required")]
    [FrameworkAttributes.StringLength(100, ErrorMessage = "Session ID cannot exceed 100 characters")]

    [ComponentModel.Description("Session ID to list checkpoints for")]
    public string SessionId { get; set; } = string.Empty;
    
    [ComponentModel.Description("Maximum number of checkpoints to return (default: 20)")]
    public int? MaxResults { get; set; }
}

public class ListCheckpointsResult : ToolResultBase
{
    public override string Operation => "list_checkpoints";
    public List<CheckpointSummary> Checkpoints { get; set; } = new();
    public int TotalCount { get; set; }
}

public class CheckpointSummary
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public string[] ActiveFiles { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
}