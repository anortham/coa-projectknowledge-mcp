using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.ProjectKnowledge.McpServer.Services;
using System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class GetRelationshipsTool : McpToolBase<GetRelationshipsParams, GetRelationshipsResult>
{
    private readonly RelationshipService _relationshipService;
    
    public GetRelationshipsTool(RelationshipService relationshipService)
    {
        _relationshipService = relationshipService;
    }
    
    public override string Name => "get_relationships";
    public override string Description => "Get all relationships for a knowledge item";
    public override ToolCategory Category => ToolCategory.Query;

    protected override async Task<GetRelationshipsResult> ExecuteInternalAsync(GetRelationshipsParams parameters, CancellationToken cancellationToken)
    {
        try
        {
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
                Error = new ErrorInfo
                {
                    Code = "RELATIONSHIPS_GET_FAILED",
                    Message = $"Failed to get relationships: {ex.Message}"
                }
            };
        }
    }
}

public class GetRelationshipsParams
{
    [Description("ID of the knowledge item to get relationships for")]
    public string KnowledgeId { get; set; } = string.Empty;
    
    [Description("Direction of relationships to retrieve (from, to, or both)")]
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