using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Constants;
using System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class CreateRelationshipTool : McpToolBase<CreateRelationshipParams, CreateRelationshipResult>
{
    private readonly RelationshipService _relationshipService;
    
    public CreateRelationshipTool(RelationshipService relationshipService)
    {
        _relationshipService = relationshipService;
    }
    
    public override string Name => ToolNames.LinkKnowledge;
    public override string Description => "Create a relationship between two knowledge items";
    public override ToolCategory Category => ToolCategory.Resources;

    protected override async Task<CreateRelationshipResult> ExecuteInternalAsync(CreateRelationshipParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(parameters.Description))
            {
                metadata["description"] = parameters.Description;
            }
            
            var relationship = await _relationshipService.CreateRelationshipAsync(
                parameters.FromId,
                parameters.ToId,
                parameters.RelationshipType ?? RelationshipTypes.RelatesTo,
                metadata);
            
            return new CreateRelationshipResult
            {
                Success = true,
                FromId = relationship.FromId,
                ToId = relationship.ToId,
                RelationshipType = relationship.RelationshipType,
                Message = $"Created relationship: {relationship.FromId} -> {relationship.ToId} ({relationship.RelationshipType})"
            };
        }
        catch (Exception ex)
        {
            return new CreateRelationshipResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "RELATIONSHIP_CREATE_FAILED",
                    Message = $"Failed to create relationship: {ex.Message}"
                }
            };
        }
    }
}

public class CreateRelationshipParams
{
    [Description("ID of the source knowledge item")]
    public string FromId { get; set; } = string.Empty;
    
    [Description("ID of the target knowledge item")]
    public string ToId { get; set; } = string.Empty;
    
    [Description("Type of relationship (relates_to, references, parent_of, blocks, etc.)")]
    public string? RelationshipType { get; set; }
    
    [Description("Optional description of the relationship")]
    public string? Description { get; set; }
}

public class CreateRelationshipResult : ToolResultBase
{
    public override string Operation => "create_relationship";
    public string? FromId { get; set; }
    public string? ToId { get; set; }
    public string? RelationshipType { get; set; }
}