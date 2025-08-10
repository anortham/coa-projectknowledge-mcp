using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;
using COA.Mcp.Framework.TokenOptimization.Caching;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Resources;
using COA.ProjectKnowledge.McpServer.ResponseBuilders;
using COA.ProjectKnowledge.McpServer.Constants;
using COA.ProjectKnowledge.McpServer.Helpers;
using ValidationAttributes = COA.ProjectKnowledge.McpServer.Validation;
using Microsoft.Extensions.Logging;
// Use framework attributes with aliases to avoid conflicts
using FrameworkAttributes = COA.Mcp.Framework.Attributes;
using ComponentModel = System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class SearchKnowledgeTool : McpToolBase<SearchKnowledgeParams, SearchKnowledgeResult>
{
    private readonly KnowledgeService _knowledgeService;
    private readonly KnowledgeResourceProvider _resourceProvider;
    private readonly ITokenEstimator _tokenEstimator;
    private readonly KnowledgeSearchResponseBuilder _responseBuilder;
    private readonly IResponseCacheService _cacheService;
    private readonly ExecutionContextService _contextService;
    private readonly ILogger<SearchKnowledgeTool> _logger;
    
    public SearchKnowledgeTool(
        KnowledgeService knowledgeService,
        KnowledgeResourceProvider resourceProvider,
        ITokenEstimator tokenEstimator,
        IResponseCacheService cacheService,
        ExecutionContextService contextService,
        ILogger<SearchKnowledgeTool> logger,
        ILogger<KnowledgeSearchResponseBuilder> builderLogger)
    {
        _knowledgeService = knowledgeService;
        _resourceProvider = resourceProvider;
        _tokenEstimator = tokenEstimator;
        _cacheService = cacheService;
        _contextService = contextService;
        _logger = logger;
        _responseBuilder = new KnowledgeSearchResponseBuilder(builderLogger);
    }
    
    public override string Name => ToolNames.FindKnowledge;
    public override string Description => ToolDescriptions.FindKnowledge;
    public override ToolCategory Category => ToolCategory.Query;

    protected override async Task<SearchKnowledgeResult> ExecuteInternalAsync(SearchKnowledgeParams parameters, CancellationToken cancellationToken)
    {
        // Create execution context for tracking
        var customData = new Dictionary<string, object?>
        {
            ["Query"] = parameters.Query,
            ["Workspace"] = parameters.Workspace ?? "default",
            ["MaxResults"] = parameters.MaxResults ?? 50
        };
        
        return await _contextService.RunWithContextAsync(
            Name,
            async (context) => await ExecuteWithContextAsync(parameters, context, cancellationToken),
            customData: customData);
    }
    
    private async Task<SearchKnowledgeResult> ExecuteWithContextAsync(
        SearchKnowledgeParams parameters, 
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Generate cache key for this search request
        var cacheKey = GenerateCacheKey(parameters);
        
        // Record cache key in context
        context.CustomData["CacheKey"] = cacheKey;
        
        // Try to get cached response first (simplified)
        try
        {
            var cachedResult = await _cacheService.GetAsync<SearchKnowledgeResult>(cacheKey);
            if (cachedResult != null)
            {
                _logger.LogDebug("Returning cached search result for key: {CacheKey}", cacheKey);
                context.CustomData["CacheHit"] = true;
                _contextService.RecordMetric("CacheHit", 1);
                return cachedResult;
            }
            else
            {
                context.CustomData["CacheHit"] = false;
                _contextService.RecordMetric("CacheMiss", 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache retrieval failed for key: {CacheKey}", cacheKey);
            // Continue with normal execution if cache fails
        }
        
        try
        {
            var request = new SearchKnowledgeRequest
            {
                Query = parameters.Query,
                Workspace = parameters.Workspace,
                MaxResults = parameters.MaxResults ?? 50
            };

            var response = await _knowledgeService.SearchKnowledgeAsync(request);
            
            // Track search metrics
            _contextService.RecordMetric("ResultCount", response.Items?.Count ?? 0);
            _contextService.RecordMetric("TotalCount", response.TotalCount);

            if (!response.Success)
            {
                throw new ToolExecutionException(
                    Name,
                    response.Error ?? "Search failed");
            }

            // Convert KnowledgeSearchItem to Knowledge for the response builder
            var knowledgeItems = response.Items.Select(item => 
            {
                var knowledge = new Knowledge
                {
                    Id = item.Id,
                    Type = item.Type,
                    Content = item.Content,
                    CreatedAt = item.CreatedAt,
                    ModifiedAt = item.ModifiedAt,
                    AccessCount = item.AccessCount,
                    Workspace = "" // KnowledgeSearchItem doesn't have Workspace
                };
                
                // Set metadata properties
                if (item.Tags != null) knowledge.SetMetadata("tags", item.Tags);
                if (item.Status != null) knowledge.SetMetadata("status", item.Status);
                if (item.Priority != null) knowledge.SetMetadata("priority", item.Priority);
                
                return knowledge;
            }).ToList();
            
            // Build token-aware response using ResponseBuilder
            var responseContext = new ResponseContext
            {
                ResponseMode = parameters.ResponseMode ?? "adaptive",
                TokenLimit = parameters.MaxTokens ?? 8000,
                ToolName = Name
            };
            
            // Use the response builder to create an optimized response
            var optimizedResponse = await _responseBuilder.BuildResponseAsync(knowledgeItems, responseContext);
            
            // Check if we got an AIOptimizedResponse
            if (optimizedResponse is AIOptimizedResponse aiResponse)
            {
                var responseData = aiResponse.Data as AIResponseData;
                var resultItems = new List<KnowledgeItem>();
                
                // Convert the results back to KnowledgeItem format
                if (responseData?.Results != null && responseData.Results is IEnumerable<object> results)
                {
                    foreach (dynamic item in results)
                    {
                        resultItems.Add(new KnowledgeItem
                        {
                            Id = item.Id,
                            Type = item.Type,
                            Content = item.Content,
                            CreatedAt = item.CreatedAt,
                            Tags = item.Tags,
                            Status = item.Status,
                            Priority = item.Priority
                        });
                    }
                }
                
                // Calculate actual token usage
                var actualTokens = _tokenEstimator.EstimateObject(aiResponse);
                _logger.LogDebug("Response tokens: {Actual} (limit: {Limit})", actualTokens, responseContext.TokenLimit);
                
                // If data was truncated, store full results as resource
                if (aiResponse.Meta?.Truncated == true && knowledgeItems.Count > resultItems.Count)
                {
                    var searchId = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}";
                    var resourceUri = _resourceProvider.StoreAsResource(
                        "search",
                        searchId,
                        knowledgeItems,
                        $"Full search results for '{parameters.Query}' ({knowledgeItems.Count} items)");
                    
                    var truncatedResult = new SearchKnowledgeResult
                    {
                        Success = true,
                        Items = resultItems,
                        TotalCount = responseData?.ExtensionData?.ContainsKey("TotalCount") == true 
                        ? Convert.ToInt32(responseData.ExtensionData["TotalCount"]) 
                        : knowledgeItems.Count,
                        ResourceUri = resourceUri,
                        Message = responseData?.Summary ?? $"Found {knowledgeItems.Count} items",
                        Insights = aiResponse.Insights,
                        Actions = aiResponse.Actions?.Select(a => new SuggestedAction
                        {
                            Tool = a.Action,
                            Description = a.Description,
                            Parameters = a.Parameters
                        }).ToList(),
                        Meta = new ToolExecutionMetadata
                        {
                            Mode = "ai-optimized",
                            Truncated = true,
                            Tokens = actualTokens
                        }
                    };
                    
                    // Cache the result for future requests
                    try
                    {
                        await _cacheService.SetAsync(cacheKey, truncatedResult, GetCacheOptions(parameters));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cache truncated result for key: {CacheKey}", cacheKey);
                    }
                    
                    return truncatedResult;
                }
                
                var optimizedResult = new SearchKnowledgeResult
                {
                    Success = true,
                    Items = resultItems,
                    TotalCount = responseData?.ExtensionData?.ContainsKey("TotalCount") == true 
                        ? Convert.ToInt32(responseData.ExtensionData["TotalCount"]) 
                        : knowledgeItems.Count,
                    Message = responseData?.Summary ?? $"Found {knowledgeItems.Count} items",
                    Insights = aiResponse.Insights,
                    Actions = aiResponse.Actions?.Select(a => new SuggestedAction
                    {
                        Tool = a.Action,
                        Description = a.Description,
                        Parameters = a.Parameters
                    }).ToList(),
                    Meta = new ToolExecutionMetadata
                    {
                        Mode = "ai-optimized",
                        Truncated = false,
                        Tokens = actualTokens
                    }
                };
                
                // Cache the result for future requests
                try
                {
                    await _cacheService.SetAsync(cacheKey, optimizedResult, GetCacheOptions(parameters));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache optimized result for key: {CacheKey}", cacheKey);
                }
                
                return optimizedResult;
            }
            
            // Fallback to simple response if builder returns non-AI response
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
            
            var result = new SearchKnowledgeResult
            {
                Success = true,
                Items = items,
                TotalCount = response.TotalCount,
                Message = response.Message ?? $"Found {items.Count} matching knowledge items"
            };
            
            // Cache the result for future requests
            try
            {
                await _cacheService.SetAsync(cacheKey, result, GetCacheOptions(parameters));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache result for key: {CacheKey}", cacheKey);
            }
            
            return result;
        }
        catch (McpException)
        {
            // Re-throw MCP exceptions as they already have proper error info
            throw;
        }
        catch (ArgumentException ex)
        {
            throw new ParameterValidationException(
                ValidationResult.Failure("query", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during search");
            throw new ToolExecutionException(
                Name,
                $"An unexpected error occurred during search: {ex.Message}",
                ex);
        }
    }
    
    /// <summary>
    /// Generates a cache key for the search request
    /// </summary>
    private string GenerateCacheKey(SearchKnowledgeParams parameters)
    {
        var keyParts = new List<string>
        {
            "knowledge-search",
            parameters.Query.ToLowerInvariant(),
            parameters.Workspace ?? "default",
            (parameters.MaxResults ?? 50).ToString(),
            parameters.ResponseMode ?? "adaptive",
            (parameters.MaxTokens ?? 8000).ToString()
        };
        
        return string.Join(":", keyParts);
    }
    
    /// <summary>
    /// Gets cache options based on the search parameters
    /// </summary>
    private CacheEntryOptions GetCacheOptions(SearchKnowledgeParams parameters)
    {
        // Cache search results for 5 minutes by default
        // Shorter cache for dynamic queries, longer for static ones
        var cacheMinutes = parameters.Query.Contains("today") || parameters.Query.Contains("recent") ? 1 : 5;
        
        return new CacheEntryOptions(); // Basic options for now - need to check API docs
    }
}

[Serializable]
public class SearchKnowledgeParams
{
    [FrameworkAttributes.Required(ErrorMessage = "Search query is required")]
    [FrameworkAttributes.StringLength(500, ErrorMessage = "Query must be between 1 and 500 characters")]
    [ComponentModel.Description("Search query string - supports 'type:', 'tag:', 'status:' prefixes")]
    public string Query { get; set; } = string.Empty;
    
    [ValidationAttributes.WorkspaceName]
    [FrameworkAttributes.StringLength(100, ErrorMessage = "Workspace name cannot exceed 100 characters")]
    [ComponentModel.Description("Workspace to search in (optional, defaults to current)")]
    public string? Workspace { get; set; }
    
    [FrameworkAttributes.Range(1, 1000, ErrorMessage = "MaxResults must be between 1 and 1000")]
    [ComponentModel.Description("Maximum number of results to return (default: 50)")]
    public int? MaxResults { get; set; }
    
    [ComponentModel.Description("Response mode: 'summary', 'full', or 'adaptive' (default: adaptive)")]
    public string? ResponseMode { get; set; }
    
    [FrameworkAttributes.Range(100, 100000, ErrorMessage = "MaxTokens must be between 100 and 100000")]
    [ComponentModel.Description("Maximum tokens for response (default: 8000)")]
    public int? MaxTokens { get; set; }
}

[Serializable]
public class SearchKnowledgeResult : ToolResultBase
{
    public override string Operation => ToolNames.FindKnowledge;
    public List<KnowledgeItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public new List<string>? Insights { get; set; }
    public new List<SuggestedAction>? Actions { get; set; }
}

// Using SuggestedAction from COA.Mcp.Framework.Models

[Serializable]
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