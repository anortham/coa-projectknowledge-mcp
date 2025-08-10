using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Constants;
using COA.ProjectKnowledge.McpServer.Helpers;
using System.ComponentModel;
using Microsoft.Extensions.Logging;

// Use framework attributes with aliases to avoid conflicts
using FrameworkAttributes = COA.Mcp.Framework.Attributes;
using ComponentModel = System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class CreateCheckpointTool : McpToolBase<CreateCheckpointParams, CreateCheckpointResult>
{
    private readonly CheckpointService _checkpointService;
    private readonly ExecutionContextService _contextService;
    private readonly ILogger<CreateCheckpointTool> _logger;
    
    public CreateCheckpointTool(
        CheckpointService checkpointService,
        ExecutionContextService contextService,
        ILogger<CreateCheckpointTool> logger)
    {
        _checkpointService = checkpointService;
        _contextService = contextService;
        _logger = logger;
    }
    
    public override string Name => ToolNames.SaveCheckpoint;
    public override string Description => ToolDescriptions.SaveCheckpoint;
    public override ToolCategory Category => ToolCategory.Resources;

    protected override async Task<CreateCheckpointResult> ExecuteInternalAsync(CreateCheckpointParams parameters, CancellationToken cancellationToken)
    {
        // Create execution context for tracking
        var customData = new Dictionary<string, object?>
        {
            ["SessionId"] = parameters.SessionId,
            ["ContentLength"] = parameters.Content?.Length ?? 0,
            ["ActiveFileCount"] = parameters.ActiveFiles?.Length ?? 0
        };
        
        return await _contextService.RunWithContextAsync(
            Name,
            async (context) => await ExecuteWithContextAsync(parameters, context, cancellationToken),
            customData: customData);
    }
    
    private async Task<CreateCheckpointResult> ExecuteWithContextAsync(
        CreateCheckpointParams parameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var sessionId = parameters.SessionId ?? $"session-{DateTime.Now:yyyy-MM-dd-HHmmss}";
            context.CustomData["GeneratedSessionId"] = sessionId;
            
            var checkpoint = await _checkpointService.CreateCheckpointAsync(
                parameters.Content,
                sessionId,
                parameters.ActiveFiles?.ToList());
            
            context.CustomData["CheckpointId"] = checkpoint.Id;
            context.CustomData["SequenceNumber"] = checkpoint.SequenceNumber;
            
            return new CreateCheckpointResult
            {
                Success = true,
                CheckpointId = checkpoint.Id,
                SessionId = checkpoint.SessionId,
                SequenceNumber = checkpoint.SequenceNumber,
                Message = $"Checkpoint #{checkpoint.SequenceNumber} created for session {checkpoint.SessionId}"
            };
        }
        catch (ArgumentException ex)
        {
            context.CustomData["Error"] = "ValidationError";
            return new CreateCheckpointResult
            {
                Success = false,
                Error = ErrorHelpers.CreateCheckpointError($"Invalid parameters: {ex.Message}", "create")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create checkpoint");
            context.CustomData["Error"] = true;
            return new CreateCheckpointResult
            {
                Success = false,
                Error = ErrorHelpers.CreateCheckpointError($"Failed to create checkpoint: {ex.Message}", "create")
            };
        }
    }
}

public class CreateCheckpointParams
{
    [FrameworkAttributes.Required(ErrorMessage = "Content is required")]
    [FrameworkAttributes.StringLength(50000, ErrorMessage = "Content cannot exceed 50,000 characters")]
    [ComponentModel.Description("The checkpoint content describing the current state")]
    public string Content { get; set; } = string.Empty;
    
    [FrameworkAttributes.StringLength(100, ErrorMessage = "Session ID cannot exceed 100 characters")]
    [ComponentModel.Description("Session ID (optional, will be generated if not provided)")]
    public string? SessionId { get; set; }
    
    [ComponentModel.Description("List of files currently being worked on")]
    public string[]? ActiveFiles { get; set; }
}

public class CreateCheckpointResult : ToolResultBase
{
    public override string Operation => ToolNames.SaveCheckpoint;
    public string? CheckpointId { get; set; }
    public string? SessionId { get; set; }
    public int? SequenceNumber { get; set; }
}