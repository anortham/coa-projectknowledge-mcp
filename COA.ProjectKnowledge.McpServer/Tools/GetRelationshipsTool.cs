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

public class GetRelationshipsTool : McpToolBase<GetRelationshipsParams, GetRelationshipsResult>
{
    private readonly RelationshipService _relationshipService;
    
    public GetRelationshipsTool(RelationshipService relationshipService)
    {
        _relationshipService = relationshipService;
    }
    
    public override string Name => ToolNames.FindConnections;
    public override string Description => "Get all relationships for a knowledge item";
    public override ToolCategory Category => ToolCategory.Query;

    protected override async Task<GetRelationshipsResult> ExecuteInternalAsync(GetRelationshipsParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            // Validate required parameters
            if (string.IsNullOrWhiteSpace(parameters.KnowledgeId))
            {
                return new GetRelationshipsResult
                {
                    Success = false,
                    Error = ErrorHelpers.CreateRelationshipError("Knowledge ID is required and cannot be empty", "get")
                };
            }
            
            var direction = parameters.Direction?.ToLower() switch
            {
                "from" => RelationshipDirection.From,
                "to" => RelationshipDirection.To,
                _ => RelationshipDirection.Both
            };
            
            var relationships = await _relationshipService.GetRelationshipsAsync(
                parameters.KnowledgeId,
                direction);
            
            var items = relationships.Select(r => new RelationshipInfo
            {
                FromId = r.FromId,
                ToId = r.ToId,
                RelationshipType = r.RelationshipType,
                CreatedAt = r.CreatedAt,
                Metadata = r.Metadata
            }).ToList();
            
            return new GetRelationshipsResult
            {
                Success = true,
                Relationships = items,
                TotalCount = items.Count,
                Message = $"Found {items.Count} relationships"
            };
        }
        catch (Exception ex)
        {
            return new GetRelationshipsResult
            {
                Success = false,
                Error = ErrorHelpers.CreateRelationshipError($"Failed to get relationships: {ex.Message}", "get")
            };
        }
    }
}

public class GetRelationshipsParams
{
    [ComponentModel.Description("ID of the knowledge item to get relationships for")]
    public string KnowledgeId { get; set; } = string.Empty;
    
    [ComponentModel.Description("Direction of relationships to retrieve (from, to, or both)")]
    public string? Direction { get; set; }
}

public class GetRelationshipsResult : ToolResultBase
{
    public override string Operation => "get_relationships";
    public List<RelationshipInfo> Relationships { get; set; } = new();
    public int TotalCount { get; set; }
}

public class RelationshipInfo
{
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}