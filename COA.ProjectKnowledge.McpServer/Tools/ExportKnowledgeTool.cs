using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.TokenOptimization;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Resources;
using COA.ProjectKnowledge.McpServer.Constants;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

// Use framework attributes with aliases to avoid conflicts
using FrameworkAttributes = COA.Mcp.Framework.Attributes;
using ComponentModel = System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class ExportKnowledgeTool : McpToolBase<ExportKnowledgeParams, ExportKnowledgeResult>
{
    private readonly MarkdownExportService _exportService;
    private readonly KnowledgeResourceProvider _resourceProvider;
    private readonly KnowledgeService _knowledgeService;
    private readonly ITokenEstimator _tokenEstimator;
    private readonly ILogger<ExportKnowledgeTool> _logger;
    
    public ExportKnowledgeTool(
        MarkdownExportService exportService,
        KnowledgeResourceProvider resourceProvider,
        KnowledgeService knowledgeService,
        ITokenEstimator tokenEstimator,
        ILogger<ExportKnowledgeTool> logger)
    {
        _exportService = exportService;
        _resourceProvider = resourceProvider;
        _knowledgeService = knowledgeService;
        _tokenEstimator = tokenEstimator;
        _logger = logger;
    }
    
    public override string Name => ToolNames.ExportKnowledge;
    public override string Description => "Export knowledge to Obsidian-compatible markdown files";
    public override ToolCategory Category => ToolCategory.Resources;

    protected override async Task<ExportKnowledgeResult> ExecuteInternalAsync(ExportKnowledgeParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            // Check if we should use resource provider for large exports
            var items = await _knowledgeService.GetAllAsync(parameters.Workspace, cancellationToken);
            
            if (!string.IsNullOrEmpty(parameters.FilterByType))
            {
                items = items.Where(k => k.Type.Equals(parameters.FilterByType, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            
            // Calculate token estimate for the export
            var exportTokens = _tokenEstimator.EstimateCollection(items);
            var tokenLimit = parameters.MaxTokens ?? 10000;
            
            _logger.LogDebug("Export token estimate: {Tokens} (limit: {Limit})", exportTokens, tokenLimit);
            
            // If export would exceed token limit and no output path specified, use resource provider
            if (exportTokens > tokenLimit && string.IsNullOrEmpty(parameters.OutputPath))
            {
                // Generate export ID
                var exportId = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}";
                
                // Store as resource
                var resourceUri = _resourceProvider.StoreAsResource(
                    "export",
                    exportId,
                    items,
                    $"Export of {items.Count} knowledge items from {parameters.Workspace ?? "all workspaces"}");
                
                return new ExportKnowledgeResult
                {
                    Success = true,
                    ExportedCount = items.Count,
                    ResourceUri = resourceUri,
                    Message = $"Export ({items.Count} items, ~{exportTokens:N0} tokens) exceeds token limit. Stored as resource: {resourceUri}",
                    Meta = new ToolExecutionMetadata
                    {
                        Mode = "resource",
                        Truncated = false,
                        Tokens = exportTokens
                    }
                };
            }
            
            // Regular export for smaller datasets or when output path is specified
            var options = new ExportOptions
            {
                IncludeRelationships = parameters.IncludeRelationships ?? true,
                CreateIndex = parameters.CreateIndex ?? true,
                FilterByType = parameters.FilterByType,
                IncludeArchived = parameters.IncludeArchived ?? false
            };
            
            var result = await _exportService.ExportToMarkdownAsync(
                parameters.OutputPath,
                parameters.Workspace,
                options);
            
            return new ExportKnowledgeResult
            {
                Success = result.Success,
                OutputPath = result.OutputPath,
                ExportedCount = result.ExportedCount,
                FailedCount = result.FailedCount,
                Message = result.Message
            };
        }
        catch (Exception ex)
        {
            return new ExportKnowledgeResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "EXPORT_FAILED",
                    Message = $"Export failed: {ex.Message}",
                    Recovery = new RecoveryInfo
                    {
                        Steps = new[]
                        {
                            "Check if the output path is valid and writable",
                            "Ensure you have sufficient disk space",
                            "Verify the workspace name if specified",
                            "Try exporting without filters first"
                        },
                        SuggestedActions = new List<SuggestedAction>
                        {
                            new SuggestedAction
                            {
                                Tool = ToolNames.FindKnowledge,
                                Description = "Search for specific items to export",
                                Parameters = new Dictionary<string, object> 
                                { 
                                    { "query", parameters.FilterByType ?? "" }, 
                                    { "workspace", parameters.Workspace ?? "" } 
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}

public class ExportKnowledgeParams
{
    [ComponentModel.Description("Output directory path (optional, defaults to exports folder)")]
    public string? OutputPath { get; set; }
    
    [ComponentModel.Description("Workspace to export (optional, defaults to current)")]
    public string? Workspace { get; set; }
    
    [ComponentModel.Description("Include relationship links between items (default: true)")]
    public bool? IncludeRelationships { get; set; }
    
    [ComponentModel.Description("Create an index/README file (default: true)")]
    public bool? CreateIndex { get; set; }
    
    [ComponentModel.Description("Filter by knowledge type (optional)")]
    public string? FilterByType { get; set; }
    
    [ComponentModel.Description("Include archived items (default: false)")]
    public bool? IncludeArchived { get; set; }
    
    [ComponentModel.Description("Maximum tokens for response when using resource storage (default: 10000)")]
    public int? MaxTokens { get; set; }
}

public class ExportKnowledgeResult : ToolResultBase
{
    public override string Operation => "export_knowledge";
    public string? OutputPath { get; set; }
    public int ExportedCount { get; set; }
    public int FailedCount { get; set; }
}