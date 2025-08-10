using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.TokenOptimization;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Resources;
using COA.ProjectKnowledge.McpServer.Constants;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

// Use framework attributes with aliases to avoid conflicts
using FrameworkAttributes = COA.Mcp.Framework.Attributes;
using ComponentModel = System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

/// <summary>
/// Search knowledge across multiple projects/workspaces
/// </summary>
public class SearchCrossProjectTool : McpToolBase<CrossProjectSearchParams, CrossProjectSearchResult>
{
    private readonly KnowledgeService _knowledgeService;
    private readonly KnowledgeResourceProvider _resourceProvider;
    private readonly ITokenEstimator _tokenEstimator;
    private readonly ILogger<SearchCrossProjectTool> _logger;

    public SearchCrossProjectTool(
        KnowledgeService knowledgeService,
        KnowledgeResourceProvider resourceProvider,
        ITokenEstimator tokenEstimator,
        ILogger<SearchCrossProjectTool> logger)
    {
        _knowledgeService = knowledgeService;
        _resourceProvider = resourceProvider;
        _tokenEstimator = tokenEstimator;
        _logger = logger;
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

            // Calculate actual token usage
            var responseTokens = _tokenEstimator.EstimateCollection(crossProjectItems);
            var tokenLimit = parameters.MaxTokens ?? 8000;
            
            _logger.LogDebug("Cross-project search tokens: {Tokens} (limit: {Limit})", responseTokens, tokenLimit);
            
            // If results exceed token limit, store as resource
            if (responseTokens > tokenLimit)
            {
                var searchId = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}";
                var resourceUri = _resourceProvider.StoreAsResource(
                    "search",
                    searchId,
                    crossProjectItems,
                    $"Cross-project search results for '{parameters.Query}' ({crossProjectItems.Count} items)");

                // Calculate how many items fit in token budget
                var previewItems = new List<CrossProjectKnowledgeItem>();
                var currentTokens = 0;
                var tokenBudget = (int)(tokenLimit * 0.8); // Use 80% for data, leave room for metadata
                
                foreach (var item in crossProjectItems)
                {
                    var itemTokens = _tokenEstimator.EstimateObject(item);
                    if (currentTokens + itemTokens <= tokenBudget)
                    {
                        previewItems.Add(item);
                        currentTokens += itemTokens;
                    }
                    else
                    {
                        break;
                    }
                }

                // Return limited items with resource URI
                return new CrossProjectSearchResult
                {
                    Success = true,
                    Items = previewItems,
                    TotalCount = response.TotalCount,
                    WorkspaceCount = crossProjectItems.Select(i => i.Workspace).Distinct().Count(),
                    ResourceUri = resourceUri,
                    Message = $"Found {crossProjectItems.Count} items across {crossProjectItems.Select(i => i.Workspace).Distinct().Count()} projects (showing {previewItems.Count}). Full results: {resourceUri}",
                    Meta = new ToolExecutionMetadata
                    {
                        Mode = "token-optimized",
                        Truncated = true,
                        Tokens = currentTokens
                    }
                };
            }

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
                    Message = $"Cross-project search failed: {ex.Message}",
                    Recovery = new RecoveryInfo
                    {
                        Steps = new[]
                        {
                            "Check if the search query is valid",
                            "Verify workspace names are correct",
                            "Try a simpler search query first",
                            "Reduce the number of workspaces being searched"
                        },
                        SuggestedActions = new List<SuggestedAction>
                        {
                            new SuggestedAction
                            {
                                Tool = ToolNames.DiscoverProjects,
                                Description = "List available workspaces",
                                Parameters = new Dictionary<string, object>()
                            },
                            new SuggestedAction
                            {
                                Tool = ToolNames.FindKnowledge,
                                Description = "Search within current workspace",
                                Parameters = new Dictionary<string, object> { { "query", parameters.Query } }
                            }
                        }
                    }
                }
            };
        }
    }
}

public class CrossProjectSearchParams
{
    [FrameworkAttributes.Required(ErrorMessage = "Query is required")]
    [FrameworkAttributes.StringLength(500, ErrorMessage = "Query must be between 1 and 500 characters")]

    [ComponentModel.Description("Search query string - supports 'type:', 'workspace:', 'tag:' prefixes")]
    public string Query { get; set; } = string.Empty;

    [ComponentModel.Description("Specific workspaces to search (optional, if not specified searches ALL projects)")]
    public string[]? Workspaces { get; set; }

    [ComponentModel.Description("Maximum number of results to return (default: 20)")]
    public int? MaxResults { get; set; }
    
    [ComponentModel.Description("Maximum tokens for response (default: 8000)")]
    public int? MaxTokens { get; set; }
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