using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Constants;
using System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class GetCheckpointTool : McpToolBase<GetCheckpointParams, GetCheckpointResult>
{
    private readonly CheckpointService _checkpointService;
    
    public GetCheckpointTool(CheckpointService checkpointService)
    {
        _checkpointService = checkpointService;
    }
    
    public override string Name => ToolNames.LoadCheckpoint;
    public override string Description => ToolDescriptions.LoadCheckpoint;
    public override ToolCategory Category => ToolCategory.Query;

    protected override async Task<GetCheckpointResult> ExecuteInternalAsync(GetCheckpointParams parameters, CancellationToken cancellationToken)
    {
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
                    Error = new ErrorInfo
                    {
                        Code = "CHECKPOINT_NOT_FOUND",
                        Message = "No checkpoint found"
                    }
                };
            }
            
            return new GetCheckpointResult
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
        }
        catch (Exception ex)
        {
            return new GetCheckpointResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "CHECKPOINT_GET_FAILED",
                    Message = $"Failed to get checkpoint: {ex.Message}"
                }
            };
        }
    }
}

public class GetCheckpointParams
{
    [Description("Specific checkpoint ID to retrieve")]
    public string? CheckpointId { get; set; }
    
    [Description("Session ID to get the latest checkpoint from")]
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