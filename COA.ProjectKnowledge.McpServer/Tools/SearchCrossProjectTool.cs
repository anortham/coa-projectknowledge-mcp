using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.ResponseBuilders;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Resources;
using COA.ProjectKnowledge.McpServer.Constants;
using COA.ProjectKnowledge.McpServer.Helpers;
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
    private readonly CrossProjectSearchResponseBuilder _responseBuilder;
    private readonly ILogger<SearchCrossProjectTool> _logger;

    public SearchCrossProjectTool(
        KnowledgeService knowledgeService,
        KnowledgeResourceProvider resourceProvider,
        ITokenEstimator tokenEstimator,
        ILogger<SearchCrossProjectTool> logger,
        ILogger<CrossProjectSearchResponseBuilder> builderLogger)
    {
        _knowledgeService = knowledgeService;
        _resourceProvider = resourceProvider;
        _tokenEstimator = tokenEstimator;
        _responseBuilder = new CrossProjectSearchResponseBuilder(builderLogger);
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
                    Error = ErrorHelpers.CreateSearchError(response.Error ?? "Cross-project search failed")
                };
            }

            // Convert service items to a list for the response builder
            var crossWorkspaceItems = response.Items.Cast<CrossWorkspaceSearchItem>().ToList();
            
            // Use the response builder to get optimized CrossProjectKnowledgeItems
            var responseContext = new ResponseContext
            {
                ResponseMode = "adaptive",
                TokenLimit = parameters.MaxTokens ?? 8000,
                ToolName = Name
            };
            
            var resultItems = await _responseBuilder.BuildResponseAsync(crossWorkspaceItems, responseContext);

            // Calculate actual token usage
            var actualTokens = _tokenEstimator.EstimateCollection(resultItems);
            _logger.LogDebug("Cross-project response tokens: {Actual} (limit: {Limit})", actualTokens, responseContext.TokenLimit);
            
            // Check if data was truncated (fewer items returned than original)
            string? resourceUri = null;
            if (crossWorkspaceItems.Count > resultItems.Count)
            {
                var searchId = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}";
                resourceUri = _resourceProvider.StoreAsResource(
                    "search",
                    searchId,
                    crossWorkspaceItems,
                    $"Full cross-project search results for '{parameters.Query}' ({crossWorkspaceItems.Count} items)");

            }
            
            // Create the result with the optimized items
            var workspaceCount = resultItems.Select(i => i.Workspace).Distinct().Count();
            var result = new CrossProjectSearchResult
            {
                Success = true,
                Items = resultItems,
                TotalCount = response.TotalCount,
                WorkspaceCount = workspaceCount,
                ResourceUri = resourceUri,
                Message = $"Found {response.TotalCount} items across {workspaceCount} projects" + 
                         (resourceUri != null ? " (truncated for token limit)" : ""),
                Meta = new ToolExecutionMetadata
                {
                    Mode = "token-optimized",
                    Truncated = resourceUri != null,
                    Tokens = actualTokens
                }
            };
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cross-project search failed");
            return new CrossProjectSearchResult
            {
                Success = false,
                Items = new List<CrossProjectKnowledgeItem>(),
                Error = ErrorHelpers.CreateSearchError($"Cross-project search failed: {ex.Message}")
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