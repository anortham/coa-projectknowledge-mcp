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

public class ListCheckpointsTool : McpToolBase<ListCheckpointsParams, ListCheckpointsResult>
{
    private readonly CheckpointService _checkpointService;
    
    public ListCheckpointsTool(CheckpointService checkpointService)
    {
        _checkpointService = checkpointService;
    }
    
    public override string Name => ToolNames.ListCheckpoints;
    public override string Description => "List checkpoints for a session";
    public override ToolCategory Category => ToolCategory.Query;

    protected override async Task<ListCheckpointsResult> ExecuteInternalAsync(ListCheckpointsParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            var checkpoints = await _checkpointService.ListCheckpointsAsync(
                parameters.SessionId,
                parameters.MaxResults ?? 20);
            
            return new ListCheckpointsResult
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
        }
        catch (Exception ex)
        {
            return new ListCheckpointsResult
            {
                Success = false,
                Error = ErrorHelpers.CreateCheckpointError($"Failed to list checkpoints: {ex.Message}", "list")
            };
        }
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