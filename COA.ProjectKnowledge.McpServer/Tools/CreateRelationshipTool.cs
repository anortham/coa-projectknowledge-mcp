using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Constants;
using COA.ProjectKnowledge.McpServer.Helpers;
using System.ComponentModel;
using Microsoft.Extensions.Logging;

// Use framework attributes with aliases to avoid conflicts
using FrameworkAttributes = COA.Mcp.Framework.Attributes;
using ComponentModel = System.ComponentModel;

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
            // Validate that both knowledge items exist
            var fromExists = await _relationshipService.KnowledgeExistsAsync(parameters.FromId);
            if (!fromExists)
            {
                return new CreateRelationshipResult
                {
                    Success = false,
                    Error = new ErrorInfo
                    {
                        Code = "KNOWLEDGE_NOT_FOUND",
                        Message = $"Source knowledge item '{parameters.FromId}' not found",
                        Recovery = new RecoveryInfo
                        {
                            Steps = new[]
                            {
                                "Verify the FromId knowledge item exists",
                                "Create the source knowledge item first",
                                "Check for typos in the knowledge ID"
                            }
                        }
                    }
                };
            }

            var toExists = await _relationshipService.KnowledgeExistsAsync(parameters.ToId);
            if (!toExists)
            {
                return new CreateRelationshipResult
                {
                    Success = false,
                    Error = new ErrorInfo
                    {
                        Code = "KNOWLEDGE_NOT_FOUND",
                        Message = $"Target knowledge item '{parameters.ToId}' not found",
                        Recovery = new RecoveryInfo
                        {
                            Steps = new[]
                            {
                                "Verify the ToId knowledge item exists",
                                "Create the target knowledge item first",
                                "Check for typos in the knowledge ID"
                            }
                        }
                    }
                };
            }
            
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
                Error = ErrorHelpers.CreateRelationshipError($"Failed to create relationship: {ex.Message}", "create")
            };
        }
    }
}

public class CreateRelationshipParams
{
    [ComponentModel.Description("ID of the source knowledge item")]
    public string FromId { get; set; } = string.Empty;
    
    [ComponentModel.Description("ID of the target knowledge item")]
    public string ToId { get; set; } = string.Empty;
    
    [ComponentModel.Description("Type of relationship (relates_to, references, parent_of, blocks, etc.)")]
    public string? RelationshipType { get; set; }
    
    [ComponentModel.Description("Optional description of the relationship")]
    public string? Description { get; set; }
}

public class CreateRelationshipResult : ToolResultBase
{
    public override string Operation => "create_relationship";
    public string? FromId { get; set; }
    public string? ToId { get; set; }
    public string? RelationshipType { get; set; }
}