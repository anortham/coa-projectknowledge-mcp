using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.ProjectKnowledge.McpServer.Services;
using System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class ExportKnowledgeTool : McpToolBase<ExportKnowledgeParams, ExportKnowledgeResult>
{
    private readonly MarkdownExportService _exportService;
    
    public ExportKnowledgeTool(MarkdownExportService exportService)
    {
        _exportService = exportService;
    }
    
    public override string Name => "export_knowledge";
    public override string Description => "Export knowledge to Obsidian-compatible markdown files";
    public override ToolCategory Category => ToolCategory.Resources;

    protected override async Task<ExportKnowledgeResult> ExecuteInternalAsync(ExportKnowledgeParams parameters, CancellationToken cancellationToken)
    {
        try
        {
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
                    Message = $"Export failed: {ex.Message}"
                }
            };
        }
    }
}

public class ExportKnowledgeParams
{
    [Description("Output directory path (optional, defaults to exports folder)")]
    public string? OutputPath { get; set; }
    
    [Description("Workspace to export (optional, defaults to current)")]
    public string? Workspace { get; set; }
    
    [Description("Include relationship links between items (default: true)")]
    public bool? IncludeRelationships { get; set; }
    
    [Description("Create an index/README file (default: true)")]
    public bool? CreateIndex { get; set; }
    
    [Description("Filter by knowledge type (optional)")]
    public string? FilterByType { get; set; }
    
    [Description("Include archived items (default: false)")]
    public bool? IncludeArchived { get; set; }
}

public class ExportKnowledgeResult : ToolResultBase
{
    public override string Operation => "export_knowledge";
    public string? OutputPath { get; set; }
    public int ExportedCount { get; set; }
    public int FailedCount { get; set; }
}