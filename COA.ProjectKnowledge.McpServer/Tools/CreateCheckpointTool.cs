using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class CreateCheckpointTool : McpToolBase<CreateCheckpointParams, CreateCheckpointResult>
{
    private readonly CheckpointService _checkpointService;
    
    public CreateCheckpointTool(CheckpointService checkpointService)
    {
        _checkpointService = checkpointService;
    }
    
    public override string Name => "create_checkpoint";
    public override string Description => "Create a checkpoint to save the current state of work";
    public override ToolCategory Category => ToolCategory.Resources;

    protected override async Task<CreateCheckpointResult> ExecuteInternalAsync(CreateCheckpointParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            var sessionId = parameters.SessionId ?? $"session-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            
            var checkpoint = await _checkpointService.StoreCheckpointAsync(
                parameters.Content,
                sessionId,
                parameters.ActiveFiles);
            
            return new CreateCheckpointResult
            {
                Success = true,
                CheckpointId = checkpoint.Id,
                SessionId = checkpoint.SessionId,
                SequenceNumber = checkpoint.SequenceNumber,
                Message = $"Checkpoint #{checkpoint.SequenceNumber} created for session {checkpoint.SessionId}"
            };
        }
        catch (Exception ex)
        {
            return new CreateCheckpointResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "CHECKPOINT_CREATE_FAILED",
                    Message = $"Failed to create checkpoint: {ex.Message}"
                }
            };
        }
    }
}

public class CreateCheckpointParams
{
    [Description("The checkpoint content describing the current state")]
    public string Content { get; set; } = string.Empty;
    
    [Description("Session ID (optional, will be generated if not provided)")]
    public string? SessionId { get; set; }
    
    [Description("List of files currently being worked on")]
    public string[]? ActiveFiles { get; set; }
}

public class CreateCheckpointResult : ToolResultBase
{
    public override string Operation => "create_checkpoint";
    public string? CheckpointId { get; set; }
    public string? SessionId { get; set; }
    public int? SequenceNumber { get; set; }
}