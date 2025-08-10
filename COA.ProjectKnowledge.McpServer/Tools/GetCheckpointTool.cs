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
                    Error = ErrorHelpers.CreateCheckpointError($"Checkpoint {parameters.CheckpointId ?? "latest"} not found", "get")
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
                Error = ErrorHelpers.CreateCheckpointError($"Failed to get checkpoint: {ex.Message}", "get")
            };
        }
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