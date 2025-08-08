using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Storage;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Services;

public class RelationshipService
{
    private readonly KnowledgeDatabase _database;
    private readonly ILogger<RelationshipService> _logger;
    
    public RelationshipService(
        KnowledgeDatabase database,
        ILogger<RelationshipService> logger)
    {
        _database = database;
        _logger = logger;
    }
    
    public async Task<Relationship> CreateRelationshipAsync(
        string fromId, 
        string toId, 
        string relationshipType,
        Dictionary<string, object>? metadata = null)
    {
        var relationship = new Relationship
        {
            FromId = fromId,
            ToId = toId,
            RelationshipType = relationshipType,
            Metadata = metadata ?? new Dictionary<string, object>(),
            CreatedAt = DateTime.UtcNow
        };
        
        await _database.InsertRelationshipAsync(relationship);
        
        _logger.LogInformation("Created relationship: {FromId} -> {ToId} ({Type})", 
            fromId, toId, relationshipType);
        
        return relationship;
    }
    
    public async Task<List<Relationship>> GetRelationshipsAsync(string knowledgeId, RelationshipDirection direction = RelationshipDirection.Both)
    {
        return await _database.GetRelationshipsAsync(knowledgeId, direction);
    }
    
    public async Task<bool> DeleteRelationshipAsync(string fromId, string toId, string relationshipType)
    {
        var deleted = await _database.DeleteRelationshipAsync(fromId, toId, relationshipType);
        
        if (deleted)
        {
            _logger.LogInformation("Deleted relationship: {FromId} -> {ToId} ({Type})", 
                fromId, toId, relationshipType);
        }
        
        return deleted;
    }
    
    public async Task<Dictionary<string, List<string>>> GetRelationshipGraphAsync(string knowledgeId, int maxDepth = 2)
    {
        var graph = new Dictionary<string, List<string>>();
        var visited = new HashSet<string>();
        var queue = new Queue<(string id, int depth)>();
        
        queue.Enqueue((knowledgeId, 0));
        
        while (queue.Count > 0)
        {
            var (currentId, depth) = queue.Dequeue();
            
            if (depth >= maxDepth || visited.Contains(currentId))
                continue;
                
            visited.Add(currentId);
            
            var relationships = await GetRelationshipsAsync(currentId);
            var connections = relationships
                .Select(r => r.FromId == currentId ? r.ToId : r.FromId)
                .Distinct()
                .ToList();
            
            graph[currentId] = connections;
            
            foreach (var connectedId in connections)
            {
                if (!visited.Contains(connectedId))
                {
                    queue.Enqueue((connectedId, depth + 1));
                }
            }
        }
        
        return graph;
    }
}

public class Relationship
{
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public enum RelationshipDirection
{
    From,
    To,
    Both
}

public static class RelationshipTypes
{
    public const string RelatesTo = "relates_to";
    public const string References = "references";
    public const string ParentOf = "parent_of";
    public const string ChildOf = "child_of";
    public const string Blocks = "blocks";
    public const string BlockedBy = "blocked_by";
    public const string Implements = "implements";
    public const string ImplementedBy = "implemented_by";
    public const string Follows = "follows";
    public const string Precedes = "precedes";
}