using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Exceptions;
using COA.ProjectKnowledge.McpServer.Constants;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.ResponseBuilders;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Helpers;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace COA.ProjectKnowledge.McpServer.Tools;

/// <summary>
/// Enhanced knowledge search with temporal scoring and advanced filtering
/// </summary>
public class FindKnowledgeTool : McpToolBase<FindKnowledgeParams, FindKnowledgeResult>
{
    private readonly KnowledgeService _knowledgeService;
    private readonly KnowledgeSearchResponseBuilder _responseBuilder;
    private readonly ILogger<FindKnowledgeTool> _logger;
    
    public FindKnowledgeTool(
        KnowledgeService knowledgeService,
        KnowledgeSearchResponseBuilder responseBuilder,
        ILogger<FindKnowledgeTool> logger)
    {
        _knowledgeService = knowledgeService;
        _responseBuilder = responseBuilder;
        _logger = logger;
    }
    
    public override string Name => ToolNames.FindKnowledge;
    public override string Description => ToolDescriptions.FindKnowledge;
    
    protected override async Task<FindKnowledgeResult> ExecuteInternalAsync(FindKnowledgeParams parameters, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Finding knowledge with enhanced search: Query={Query}, TemporalScoring={TemporalScoring}, BoostRecent={BoostRecent}", 
            parameters.Query, parameters.TemporalScoring, parameters.BoostRecent);
        
        // Convert tool parameters to service parameters
        var searchParams = new KnowledgeSearchParameters
        {
            Query = parameters.Query,
            Types = parameters.Types,
            Workspace = parameters.Workspace,
            MaxResults = parameters.MaxResults ?? 50,
            OrderBy = parameters.OrderBy,
            OrderDescending = parameters.OrderDescending ?? true,
            BoostRecent = parameters.BoostRecent ?? true,
            BoostFrequent = parameters.BoostFrequent ?? false,
            TemporalScoring = parameters.TemporalScoring ?? TemporalScoringMode.Default,
            IncludeArchived = parameters.IncludeArchived ?? false,
            FromDate = parameters.FromDate,
            ToDate = parameters.ToDate,
            Tags = parameters.Tags,
            Priorities = parameters.Priorities,
            Statuses = parameters.Statuses
        };
        
        // Use enhanced search
        var response = await _knowledgeService.SearchEnhancedAsync(searchParams);
        
        if (!response.Success)
        {
            throw new McpException(
                "SEARCH_FAILED",
                response.Error ?? "Failed to search knowledge",
                new Dictionary<string, object>
                {
                    ["query"] = parameters.Query ?? "",
                    ["workspace"] = parameters.Workspace ?? "current"
                });
        }
        
        // Return result directly
        return new FindKnowledgeResult
        {
            Items = response.Items,
            TotalCount = response.TotalCount,
            Message = response.Message ?? $"Found {response.Items.Count} knowledge items with temporal scoring"
        };
    }
}

/// <summary>
/// Parameters for enhanced knowledge search
/// </summary>
public class FindKnowledgeParams
{
    /// <summary>
    /// Search query text
    /// </summary>
    public string? Query { get; set; }
    
    /// <summary>
    /// Filter by knowledge types
    /// </summary>
    public string[]? Types { get; set; }
    
    /// <summary>
    /// Filter by workspace (defaults to current)
    /// </summary>
    public string? Workspace { get; set; }
    
    /// <summary>
    /// Maximum results to return (default: 50)
    /// </summary>
    public int? MaxResults { get; set; }
    
    /// <summary>
    /// Order by field: created, modified, accessed, accesscount, relevance
    /// </summary>
    public string? OrderBy { get; set; }
    
    /// <summary>
    /// Order descending (default: true)
    /// </summary>
    public bool? OrderDescending { get; set; }
    
    /// <summary>
    /// Boost recent knowledge in scoring (default: true)
    /// </summary>
    public bool? BoostRecent { get; set; }
    
    /// <summary>
    /// Boost frequently accessed knowledge (default: false)
    /// </summary>
    public bool? BoostFrequent { get; set; }
    
    /// <summary>
    /// Temporal scoring mode: None, Default, Aggressive, Gentle (default: Default)
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TemporalScoringMode? TemporalScoring { get; set; }
    
    /// <summary>
    /// Include archived items (default: false)
    /// </summary>
    public bool? IncludeArchived { get; set; }
    
    /// <summary>
    /// Filter by creation date - from
    /// </summary>
    public DateTime? FromDate { get; set; }
    
    /// <summary>
    /// Filter by creation date - to
    /// </summary>
    public DateTime? ToDate { get; set; }
    
    /// <summary>
    /// Filter by tags (any match)
    /// </summary>
    public string[]? Tags { get; set; }
    
    /// <summary>
    /// Filter by priority levels
    /// </summary>
    public string[]? Priorities { get; set; }
    
    /// <summary>
    /// Filter by status values
    /// </summary>
    public string[]? Statuses { get; set; }
}

/// <summary>
/// Result from enhanced knowledge search
/// </summary>
public class FindKnowledgeResult
{
    /// <summary>
    /// Found knowledge items
    /// </summary>
    public List<KnowledgeSearchItem> Items { get; set; } = new();
    
    /// <summary>
    /// Total count of matching items (before limit)
    /// </summary>
    public int TotalCount { get; set; }
    
    /// <summary>
    /// Descriptive message about the search results
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Resource URI if results were stored as a resource
    /// </summary>
    public string? ResourceUri { get; set; }
}