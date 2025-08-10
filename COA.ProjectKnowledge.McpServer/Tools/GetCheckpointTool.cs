using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.TokenOptimization;
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

public class GetCheckpointTool : McpToolBase<GetCheckpointParams, GetCheckpointResult>
{
    private readonly CheckpointService _checkpointService;
    private readonly IResponseCacheService _cacheService;
    private readonly ExecutionContextService _contextService;
    private readonly ILogger<GetCheckpointTool> _logger;
    
    public GetCheckpointTool(
        CheckpointService checkpointService,
        IResponseCacheService cacheService,
        ExecutionContextService contextService,
        ILogger<GetCheckpointTool> logger)
    {
        _checkpointService = checkpointService;
        _cacheService = cacheService;
        _contextService = contextService;
        _logger = logger;
    }
    
    public override string Name => ToolNames.LoadCheckpoint;
    public override string Description => ToolDescriptions.LoadCheckpoint;
    public override ToolCategory Category => ToolCategory.Query;

    protected override async Task<GetCheckpointResult> ExecuteInternalAsync(GetCheckpointParams parameters, CancellationToken cancellationToken)
    {
        // Create execution context for tracking
        var customData = new Dictionary<string, object?>
        {
            ["CheckpointId"] = parameters.CheckpointId,
            ["SessionId"] = parameters.SessionId
        };
        
        return await _contextService.RunWithContextAsync(
            Name,
            async (context) => await ExecuteWithContextAsync(parameters, context, cancellationToken),
            customData: customData);
    }
    
    private async Task<GetCheckpointResult> ExecuteWithContextAsync(
        GetCheckpointParams parameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Generate cache key
        var cacheKey = GenerateCacheKey(parameters);
        
        // Check cache first
        if (_cacheService != null)
        {
            var cachedResult = await _cacheService.GetAsync<GetCheckpointResult>(cacheKey);
            if (cachedResult != null)
            {
                _logger.LogDebug("Returning cached checkpoint for key: {CacheKey}", cacheKey);
                context.CustomData["CacheHit"] = true;
                return cachedResult;
            }
        }
        
        try
        {
            Checkpoint? checkpoint;
            
            if (!string.IsNullOrEmpty(parameters.CheckpointId))
            {
                checkpoint = await _checkpointService.GetCheckpointAsync(checkpointId: parameters.CheckpointId);
            }
            else
            {
                checkpoint = await _checkpointService.GetCheckpointAsync(sessionId: parameters.SessionId);
            }
            
            if (checkpoint == null)
            {
                return new GetCheckpointResult
                {
                    Success = false,
                    Error = ErrorHelpers.CreateCheckpointError($"Checkpoint {parameters.CheckpointId ?? "latest"} not found", "get")
                };
            }
            
            var result = new GetCheckpointResult
            {
                Success = true,
                Checkpoint = new CheckpointInfo
                {
                    Id = checkpoint.Id,
                    Content = checkpoint.Content,
                    SessionId = checkpoint.SessionId,
                    SequenceNumber = checkpoint.SequenceNumber,
                    ActiveFiles = checkpoint.ActiveFiles,
                    CreatedAt = checkpoint.CreatedAt
                }
            };
            
            // Cache the result for 10 minutes (checkpoints are immutable)
            if (_cacheService != null)
            {
                try
                {
                    await _cacheService.SetAsync(cacheKey, result, new CacheEntryOptions());
                    context.CustomData["CacheSet"] = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache checkpoint for key: {CacheKey}", cacheKey);
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get checkpoint");
            context.CustomData["Error"] = true;
            
            return new GetCheckpointResult
            {
                Success = false,
                Error = ErrorHelpers.CreateCheckpointError($"Failed to get checkpoint: {ex.Message}", "get")
            };
        }
    }
    
    private string GenerateCacheKey(GetCheckpointParams parameters)
    {
        var keyData = new
        {
            Tool = Name,
            CheckpointId = parameters.CheckpointId,
            SessionId = parameters.SessionId
        };
        
        var json = JsonSerializer.Serialize(keyData);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return $"checkpoint_{Convert.ToBase64String(hash).Replace("/", "_").Replace("+", "-").Substring(0, 16)}";
    }
}

public class GetCheckpointParams
{
    [FrameworkAttributes.StringLength(50, ErrorMessage = "Checkpoint ID cannot exceed 50 characters")]

    [ComponentModel.Description("Specific checkpoint ID to retrieve")]
    public string? CheckpointId { get; set; }
    
    [FrameworkAttributes.StringLength(100, ErrorMessage = "Session ID cannot exceed 100 characters")]

    
    [ComponentModel.Description("Session ID to get the latest checkpoint from")]
    public string? SessionId { get; set; }
}

public class GetCheckpointResult : ToolResultBase
{
    public override string Operation => ToolNames.LoadCheckpoint;
    public CheckpointInfo? Checkpoint { get; set; }
}

public class CheckpointInfo
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public string[] ActiveFiles { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
}