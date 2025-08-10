using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Data.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Services;

public class RelationshipService
{
    private readonly KnowledgeDbContext _context;
    private readonly ILogger<RelationshipService> _logger;
    
    public RelationshipService(
        KnowledgeDbContext context,
        ILogger<RelationshipService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public async Task<Relationship> CreateRelationshipAsync(
        string fromId, 
        string toId, 
        string relationshipType,
        Dictionary<string, object>? metadata = null)
    {
        var entity = new RelationshipEntity
        {
            Id = Guid.NewGuid().ToString(),
            FromId = fromId,
            ToId = toId,
            RelationshipType = relationshipType,
            Description = metadata?.ContainsKey("description") == true ? metadata["description"]?.ToString() : null,
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Relationships.Add(entity);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Created relationship: {FromId} -> {ToId} ({Type})", 
            fromId, toId, relationshipType);
        
        var relationship = new Relationship
        {
            FromId = entity.FromId,
            ToId = entity.ToId,
            RelationshipType = entity.RelationshipType,
            Metadata = metadata ?? new Dictionary<string, object>(),
            CreatedAt = entity.CreatedAt
        };
        
        return relationship;
    }
    
    public async Task<List<Relationship>> GetRelationshipsAsync(string knowledgeId, RelationshipDirection direction = RelationshipDirection.Both)
    {
        var query = _context.Relationships.AsQueryable();
        
        switch (direction)
        {
            case RelationshipDirection.From:
                query = query.Where(r => r.FromId == knowledgeId);
                break;
            case RelationshipDirection.To:
                query = query.Where(r => r.ToId == knowledgeId);
                break;
            case RelationshipDirection.Both:
                query = query.Where(r => r.FromId == knowledgeId || r.ToId == knowledgeId);
                break;
        }
        
        var entities = await query.ToListAsync();
        
        return entities.Select(e => new Relationship
        {
            FromId = e.FromId,
            ToId = e.ToId,
            RelationshipType = e.RelationshipType,
            Metadata = !string.IsNullOrEmpty(e.Description) 
                ? new Dictionary<string, object> { ["description"] = e.Description }
                : new Dictionary<string, object>(),
            CreatedAt = e.CreatedAt
        }).ToList();
    }
    
    public async Task<bool> DeleteRelationshipAsync(string fromId, string toId, string relationshipType)
    {
        var entity = await _context.Relationships
            .FirstOrDefaultAsync(r => r.FromId == fromId && r.ToId == toId && r.RelationshipType == relationshipType);
        
        if (entity == null)
        {
            return false;
        }
        
        _context.Relationships.Remove(entity);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Deleted relationship: {FromId} -> {ToId} ({Type})", 
            fromId, toId, relationshipType);
        
        return true;
    }
    
    public async Task<bool> KnowledgeExistsAsync(string knowledgeId)
    {
        return await _context.Knowledge.AnyAsync(k => k.Id == knowledgeId);
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