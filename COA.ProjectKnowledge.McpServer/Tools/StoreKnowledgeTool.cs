using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Constants;
using System.ComponentModel;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class StoreKnowledgeTool : McpToolBase<StoreKnowledgeParams, StoreKnowledgeResult>
{
    private readonly KnowledgeService _knowledgeService;
    
    public StoreKnowledgeTool(KnowledgeService knowledgeService)
    {
        _knowledgeService = knowledgeService;
    }
    
    public override string Name => ToolNames.StoreKnowledge;
    public override string Description => ToolDescriptions.StoreKnowledge;
    public override ToolCategory Category => ToolCategory.Resources;

    protected override async Task<StoreKnowledgeResult> ExecuteInternalAsync(StoreKnowledgeParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            var request = new StoreKnowledgeRequest
            {
                Type = parameters.Type ?? KnowledgeTypes.WorkNote,
                Content = parameters.Content,
                CodeSnippets = parameters.CodeSnippets?.ToArray(),
                Status = parameters.Status,
                Priority = parameters.Priority,
                Tags = parameters.Tags,
                RelatedTo = parameters.RelatedTo,
                Metadata = parameters.Metadata
            };
            
            var response = await _knowledgeService.StoreKnowledgeAsync(request);
            
            if (!response.Success)
            {
                return new StoreKnowledgeResult
                {
                    Success = false,
                    Error = new ErrorInfo { Code = "STORE_FAILED", Message = response.Error ?? "Failed to store knowledge" }
                };
            }
            
            return new StoreKnowledgeResult
            {
                Success = true,
                Id = response.KnowledgeId,
                StoredType = request.Type
            };
        }
        catch (Exception ex)
        {
            return new StoreKnowledgeResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "STORE_FAILED",
                    Message = $"Failed to store knowledge: {ex.Message}"
                }
            };
        }
    }
}

public class StoreKnowledgeParams
{
    [Description("The knowledge content")]
    public string Content { get; set; } = string.Empty;
    
    [Description("Knowledge type (Checkpoint, Checklist, TechnicalDebt, ProjectInsight, WorkNote)")]
    public string? Type { get; set; }
    
    [Description("Code snippets with syntax information")]
    public List<CodeSnippet>? CodeSnippets { get; set; }
    
    [Description("Additional metadata fields")]
    public Dictionary<string, string>? Metadata { get; set; }
    
    [Description("Status of the knowledge item")]
    public string? Status { get; set; }
    
    [Description("Priority level")]
    public string? Priority { get; set; }
    
    [Description("Tags for categorization")]
    public string[]? Tags { get; set; }
    
    [Description("IDs of related knowledge items")]
    public string[]? RelatedTo { get; set; }
}

public class StoreKnowledgeResult : ToolResultBase
{
    public override string Operation => ToolNames.StoreKnowledge;
    public string? Id { get; set; }
    public string? StoredType { get; set; }
}