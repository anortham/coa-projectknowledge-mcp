using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
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
    
    public override string Name => "store_knowledge";
    public override string Description => "Store knowledge in the centralized knowledge base";
    public override ToolCategory Category => ToolCategory.Resources;

    protected override async Task<StoreKnowledgeResult> ExecuteInternalAsync(StoreKnowledgeParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            var knowledge = new Knowledge
            {
                Type = parameters.Type ?? KnowledgeTypes.WorkNote,
                Content = parameters.Content
            };
            
            // Add code snippets if provided
            if (parameters.CodeSnippets != null)
            {
                knowledge.CodeSnippets = parameters.CodeSnippets;
            }
            
            // Add metadata fields
            if (parameters.Metadata != null)
            {
                foreach (var kvp in parameters.Metadata)
                {
                    knowledge.SetMetadata(kvp.Key, kvp.Value);
                }
            }
            
            // Add common metadata
            if (!string.IsNullOrEmpty(parameters.Status))
                knowledge.SetMetadata("status", parameters.Status);
            if (!string.IsNullOrEmpty(parameters.Priority))
                knowledge.SetMetadata("priority", parameters.Priority);
            if (parameters.Tags != null && parameters.Tags.Length > 0)
                knowledge.SetMetadata("tags", parameters.Tags);
            if (parameters.RelatedTo != null && parameters.RelatedTo.Length > 0)
                knowledge.SetMetadata("relatedTo", parameters.RelatedTo);
            
            var stored = await _knowledgeService.StoreAsync(knowledge);
            
            return new StoreKnowledgeResult
            {
                Success = true,
                Id = stored.Id,
                StoredType = stored.Type
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
    public Dictionary<string, object>? Metadata { get; set; }
    
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
    public override string Operation => "store_knowledge";
    public string? Id { get; set; }
    public string? StoredType { get; set; }
}