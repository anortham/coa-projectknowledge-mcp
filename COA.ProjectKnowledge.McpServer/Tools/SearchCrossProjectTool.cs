using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Constants;
using System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

/// <summary>
/// Search knowledge across multiple projects/workspaces
/// </summary>
public class SearchCrossProjectTool : McpToolBase<CrossProjectSearchParams, CrossProjectSearchResult>
{
    private readonly KnowledgeService _knowledgeService;

    public SearchCrossProjectTool(KnowledgeService knowledgeService)
    {
        _knowledgeService = knowledgeService;
    }

    public override string Name => ToolNames.SearchAcrossProjects;
    public override string Description => ToolDescriptions.SearchAcrossProjects;
    public override ToolCategory Category => ToolCategory.Query;

    protected override async Task<CrossProjectSearchResult> ExecuteInternalAsync(CrossProjectSearchParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            var request = new CrossWorkspaceSearchRequest
            {
                Query = parameters.Query,
                Workspaces = parameters.Workspaces,
                MaxResults = parameters.MaxResults ?? 20
            };

            var response = await _knowledgeService.SearchAcrossWorkspacesAsync(request);

            if (!response.Success)
            {
                return new CrossProjectSearchResult
                {
                    Success = false,
                    Items = new List<CrossProjectKnowledgeItem>(),
                    Error = new ErrorInfo { Code = "CROSS_PROJECT_SEARCH_FAILED", Message = response.Error ?? "Cross-project search failed" }
                };
            }

            var crossProjectItems = response.Items.Cast<CrossWorkspaceSearchItem>().Select(item => new CrossProjectKnowledgeItem
            {
                Id = item.Id,
                Type = item.Type,
                Content = item.Content.Length > 500 ? item.Content.Substring(0, 497) + "..." : item.Content,
                Workspace = item.Workspace,
                Tags = item.Tags,
                Status = item.Status,
                Priority = item.Priority,
                CreatedAt = item.CreatedAt,
                ModifiedAt = item.ModifiedAt,
                AccessCount = item.AccessCount
            }).ToList();

            return new CrossProjectSearchResult
            {
                Success = true,
                Items = crossProjectItems,
                TotalCount = response.TotalCount,
                WorkspaceCount = crossProjectItems.Select(i => i.Workspace).Distinct().Count(),
                Message = response.Message ?? $"Found {crossProjectItems.Count} matching knowledge items across {crossProjectItems.Select(i => i.Workspace).Distinct().Count()} projects"
            };
        }
        catch (Exception ex)
        {
            return new CrossProjectSearchResult
            {
                Success = false,
                Items = new List<CrossProjectKnowledgeItem>(),
                Error = new ErrorInfo
                {
                    Code = "CROSS_PROJECT_SEARCH_FAILED",
                    Message = $"Cross-project search failed: {ex.Message}"
                }
            };
        }
    }
}

public class CrossProjectSearchParams
{
    [Description("Search query string - supports 'type:', 'workspace:', 'tag:' prefixes")]
    public string Query { get; set; } = string.Empty;

    [Description("Specific workspaces to search (optional, if not specified searches ALL projects)")]
    public string[]? Workspaces { get; set; }

    [Description("Maximum number of results to return (default: 20)")]
    public int? MaxResults { get; set; }
}

public class CrossProjectSearchResult : ToolResultBase
{
    public override string Operation => ToolNames.SearchAcrossProjects;
    public List<CrossProjectKnowledgeItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int WorkspaceCount { get; set; }
}

public class CrossProjectKnowledgeItem
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Workspace { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public int AccessCount { get; set; }
    public string[]? Tags { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
}