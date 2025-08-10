using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Constants;
using COA.ProjectKnowledge.McpServer.Helpers;
using System.ComponentModel;
using Microsoft.Extensions.Logging;

// Use framework attributes with aliases to avoid conflicts
using FrameworkAttributes = COA.Mcp.Framework.Attributes;
using ComponentModel = System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

/// <summary>
/// Get list of available workspaces/projects that contain knowledge
/// </summary>
public class GetWorkspacesTool : McpToolBase<GetWorkspacesParams, GetWorkspacesResult>
{
    private readonly KnowledgeService _knowledgeService;

    public GetWorkspacesTool(KnowledgeService knowledgeService)
    {
        _knowledgeService = knowledgeService;
    }

    public override string Name => ToolNames.DiscoverProjects;
    public override string Description => ToolDescriptions.DiscoverProjects;
    public override ToolCategory Category => ToolCategory.Query;

    protected override async Task<GetWorkspacesResult> ExecuteInternalAsync(GetWorkspacesParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            var workspaces = await _knowledgeService.GetAvailableWorkspacesAsync();

            return new GetWorkspacesResult
            {
                Success = true,
                Workspaces = workspaces,
                Count = workspaces.Count,
                Message = $"Found {workspaces.Count} workspaces with knowledge"
            };
        }
        catch (Exception ex)
        {
            return new GetWorkspacesResult
            {
                Success = false,
                Workspaces = new List<string>(),
                Count = 0,
                Error = ErrorHelpers.CreateWorkspaceError($"Failed to get workspaces: {ex.Message}")
            };
        }
    }
}

public class GetWorkspacesParams
{
    // No parameters needed for this tool
}

public class GetWorkspacesResult : ToolResultBase
{
    public override string Operation => ToolNames.DiscoverProjects;
    public List<string> Workspaces { get; set; } = new();
    public int Count { get; set; }
}