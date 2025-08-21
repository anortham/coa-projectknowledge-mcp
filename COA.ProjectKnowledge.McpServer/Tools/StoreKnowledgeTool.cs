using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.TokenOptimization.Caching;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Constants;
using COA.ProjectKnowledge.McpServer.Helpers;
using ValidationAttributes = COA.ProjectKnowledge.McpServer.Validation;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;
// Use framework attributes with aliases to avoid conflicts
using FrameworkAttributes = COA.Mcp.Framework.Attributes;
using ComponentModel = System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class StoreKnowledgeTool : McpToolBase<StoreKnowledgeParams, StoreKnowledgeResult>
{
    private readonly KnowledgeService _knowledgeService;
    private readonly IResponseCacheService _cacheService;
    private readonly ExecutionContextService _contextService;
    private readonly ILogger<StoreKnowledgeTool> _logger;
    
    public StoreKnowledgeTool(
        KnowledgeService knowledgeService,
        IResponseCacheService cacheService,
        ExecutionContextService contextService,
        ILogger<StoreKnowledgeTool> logger)
    {
        _knowledgeService = knowledgeService;
        _cacheService = cacheService;
        _contextService = contextService;
        _logger = logger;
    }
    
    public override string Name => ToolNames.StoreKnowledge;
    public override string Description => ToolDescriptions.StoreKnowledge;
    public override ToolCategory Category => ToolCategory.Resources;

    protected override async Task<StoreKnowledgeResult> ExecuteInternalAsync(StoreKnowledgeParams parameters, CancellationToken cancellationToken)
    {
        // Create execution context
        var customData = new Dictionary<string, object?>
        {
            ["Type"] = parameters.Type ?? "WorkNote",
            ["ContentLength"] = parameters.Content?.Length ?? 0,
            ["Tags"] = parameters.Tags?.Length ?? 0,
            ["HasCodeSnippets"] = parameters.CodeSnippets?.Any() ?? false
        };
        
        return await _contextService.RunWithContextAsync(
            Name,
            async (context) =>
            {
                try
                {
                    // Normalize and validate inputs
                    var normalizedType = ValidationAttributes.KnowledgeTypeAttribute.Normalize(parameters.Type!);
                    var normalizedTags = ValidationAttributes.TagsAttribute.Normalize(parameters.Tags);
                    
                    // Record normalization metrics
                    context.CustomData["NormalizedType"] = normalizedType;
                    _contextService.RecordMetric("TagsNormalized", normalizedTags.Length);
            
            var request = new StoreKnowledgeRequest
            {
                Type = normalizedType ?? "WorkNote",
                Content = parameters.Content ?? string.Empty,
                CodeSnippets = parameters.CodeSnippets?.ToArray(),
                Status = parameters.Status,
                Priority = parameters.Priority,
                Tags = normalizedTags,
                RelatedTo = parameters.RelatedTo,
                Metadata = parameters.Metadata
            };
            
            var response = await _knowledgeService.StoreKnowledgeAsync(request);
            
            if (!response.Success)
            {
                return new StoreKnowledgeResult
                {
                    Success = false,
                    Error = ErrorHelpers.CreateStoreError(response.Error ?? "Failed to store knowledge")
                };
            }
            
            // Invalidate search caches for this workspace since new knowledge was added
            var workspace = "default"; // Simplified - we don't have workspace in response
            await InvalidateSearchCachesAsync(workspace, parameters.Tags);
            
                    // Record storage success metrics
                    _contextService.RecordMetric("KnowledgeStored", 1);
                    _contextService.RecordMetric("KnowledgeType", normalizedType!);
                    context.CustomData["KnowledgeId"] = response.KnowledgeId;
                    
                    return new StoreKnowledgeResult
                    {
                        Success = true,
                        Id = response.KnowledgeId,
                        StoredType = request.Type
                    };
                }
                catch (ArgumentException ex)
                {
                    return new StoreKnowledgeResult
                    {
                        Success = false,
                        Error = ErrorHelpers.CreateStoreError($"Invalid parameters: {ex.Message}")
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error storing knowledge");
                    return new StoreKnowledgeResult
                    {
                        Success = false,
                        Error = ErrorHelpers.CreateStoreError($"Failed to store knowledge: {ex.Message}")
                    };
                }
            },
            customData: customData);
    }
    
    /// <summary>
    /// Invalidates search caches when new knowledge is added (simplified version)
    /// </summary>
    private async Task InvalidateSearchCachesAsync(string workspace, string[]? tags)
    {
        try
        {
            // Simple cache invalidation - remove specific cache patterns
            // This is a placeholder until we have the correct IResponseCacheService API
            _logger.LogDebug("Would invalidate search caches for workspace: {Workspace}", workspace);
            
            // TODO: Implement proper cache invalidation when IResponseCacheService API is clarified
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate search caches for workspace: {Workspace}", workspace);
            // Don't throw - cache invalidation failure shouldn't break the store operation
        }
    }
}

public class StoreKnowledgeParams
{
    [FrameworkAttributes.Required(ErrorMessage = "Content is required")]
    [FrameworkAttributes.StringLength(100000, ErrorMessage = "Content cannot exceed 100,000 characters")]
    [ComponentModel.Description("The knowledge content")]
    public string Content { get; set; } = string.Empty;
    
    [ValidationAttributes.KnowledgeType]
    [ComponentModel.Description("Knowledge type (TechnicalDebt, ProjectInsight, WorkNote)")]
    public string? Type { get; set; }
    
    [ComponentModel.Description("Code snippets with syntax information")]
    public List<CodeSnippet>? CodeSnippets { get; set; }
    
    [ComponentModel.Description("Additional metadata fields")]
    public Dictionary<string, string>? Metadata { get; set; }
    
    [FrameworkAttributes.StringLength(50, ErrorMessage = "Status cannot exceed 50 characters")]
    [ComponentModel.Description("Status of the knowledge item")]
    public string? Status { get; set; }
    
    [FrameworkAttributes.StringLength(20, ErrorMessage = "Priority cannot exceed 20 characters")]
    [ComponentModel.Description("Priority level (low, normal, high, critical)")]
    public string? Priority { get; set; }
    
    [ValidationAttributes.Tags(MaxTags = 20, MaxTagLength = 50)]
    [ComponentModel.Description("Tags for categorization")]
    public string[]? Tags { get; set; }
    
    [ComponentModel.Description("IDs of related knowledge items")]
    public string[]? RelatedTo { get; set; }
}

public class StoreKnowledgeResult : ToolResultBase
{
    public override string Operation => ToolNames.StoreKnowledge;
    public string? Id { get; set; }
    public string? StoredType { get; set; }
}