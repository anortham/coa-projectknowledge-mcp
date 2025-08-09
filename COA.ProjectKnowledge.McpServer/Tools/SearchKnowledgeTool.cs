using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Constants;
using System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class SearchKnowledgeTool : McpToolBase<SearchKnowledgeParams, SearchKnowledgeResult>
{
    private readonly KnowledgeService _knowledgeService;
    
    public SearchKnowledgeTool(KnowledgeService knowledgeService)
    {
        _knowledgeService = knowledgeService;
    }
    
    public override string Name => ToolNames.FindKnowledge;
    public override string Description => ToolDescriptions.FindKnowledge;
    public override ToolCategory Category => ToolCategory.Query;

    protected override async Task<SearchKnowledgeResult> ExecuteInternalAsync(SearchKnowledgeParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            var request = new SearchKnowledgeRequest
            {
                Query = parameters.Query,
                Workspace = parameters.Workspace,
                MaxResults = parameters.MaxResults ?? 50
            };

            var response = await _knowledgeService.SearchKnowledgeAsync(request);

            if (!response.Success)
            {
                return new SearchKnowledgeResult
                {
                    Success = false,
                    Items = new List<KnowledgeItem>(),
                    Error = new ErrorInfo { Code = "SEARCH_FAILED", Message = response.Error ?? "Search failed" }
                };
            }

            var items = response.Items.Select(k => new KnowledgeItem
            {
                Id = k.Id,
                Type = k.Type,
                Content = k.Content.Length > 500 
                    ? k.Content.Substring(0, 497) + "..." 
                    : k.Content,
                CreatedAt = k.CreatedAt,
                ModifiedAt = k.ModifiedAt,
                AccessCount = k.AccessCount,
                Tags = k.Tags,
                Status = k.Status,
                Priority = k.Priority
            }).ToList();
            
            return new SearchKnowledgeResult
            {
                Success = true,
                Items = items,
                TotalCount = response.TotalCount,
                Message = response.Message ?? $"Found {items.Count} matching knowledge items"
            };
        }
        catch (Exception ex)
        {
            return new SearchKnowledgeResult
            {
                Success = false,
                Items = new List<KnowledgeItem>(),
                Error = new ErrorInfo
                {
                    Code = "SEARCH_FAILED",
                    Message = $"Search failed: {ex.Message}"
                }
            };
        }
    }
}

public class SearchKnowledgeParams
{
    [Description("Search query string")]
    public string Query { get; set; } = string.Empty;
    
    [Description("Workspace to search in (optional, defaults to current)")]
    public string? Workspace { get; set; }
    
    [Description("Maximum number of results to return (default: 50)")]
    public int? MaxResults { get; set; }
}

public class SearchKnowledgeResult : ToolResultBase
{
    public override string Operation => ToolNames.FindKnowledge;
    public List<KnowledgeItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public class KnowledgeItem
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public int AccessCount { get; set; }
    public string[]? Tags { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
}