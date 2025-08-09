using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Data.Entities;
using COA.ProjectKnowledge.McpServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Services;

public class ChecklistService
{
    private readonly KnowledgeDbContext _context;
    private readonly IWorkspaceResolver _workspaceResolver;

    public ChecklistService(KnowledgeDbContext context, IWorkspaceResolver workspaceResolver)
    {
        _context = context;
        _workspaceResolver = workspaceResolver;
    }

    public async Task<Checklist> CreateChecklistAsync(string content, List<string> items, string? parentChecklistId = null)
    {
        var workspace = _workspaceResolver.GetCurrentWorkspace();
        
        var checklist = new Checklist
        {
            Content = content,
            Items = items.Select((item, index) => new ChecklistItem
            {
                Id = $"{ChronologicalId.Generate()}-item{index}",
                Content = item,
                IsCompleted = false,
                CompletedAt = null,
                Order = index
            }).ToList(),
            ParentChecklistId = parentChecklistId,
            Workspace = workspace
        };

        var entity = new KnowledgeEntity
        {
            Id = checklist.Id,
            Type = KnowledgeTypes.Checklist,
            Content = checklist.Content,
            Metadata = JsonSerializer.Serialize(new
            {
                checklist.Items,
                checklist.ParentChecklistId
            }),
            Tags = JsonSerializer.Serialize(new List<string> { "checklist" }),
            Priority = "normal",
            Status = checklist.Items.All(i => i.IsCompleted) ? "completed" : "active",
            Workspace = checklist.Workspace,
            CreatedAt = checklist.CreatedAt,
            ModifiedAt = checklist.CreatedAt,
            AccessedAt = checklist.CreatedAt,
            AccessCount = 0
        };

        _context.Knowledge.Add(entity);
        
        // Create relationship to parent if specified
        if (!string.IsNullOrEmpty(parentChecklistId))
        {
            var relationship = new RelationshipEntity
            {
                Id = Guid.NewGuid().ToString(),
                FromId = parentChecklistId,
                ToId = checklist.Id,
                RelationshipType = "parent_of",
                Description = "Parent checklist relationship",
                CreatedAt = DateTime.UtcNow
            };
            _context.Relationships.Add(relationship);
        }

        await _context.SaveChangesAsync();

        return checklist;
    }

    public async Task<Checklist?> GetChecklistAsync(string checklistId)
    {
        var workspace = _workspaceResolver.GetCurrentWorkspace();
        
        var entity = await _context.Knowledge
            .FirstOrDefaultAsync(k => k.Id == checklistId 
                && k.Type == KnowledgeTypes.Checklist 
                && k.Workspace == workspace);

        if (entity == null) return null;

        // Update access tracking efficiently
        var now = DateTime.UtcNow;
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Knowledge SET AccessedAt = @now, AccessCount = AccessCount + 1 WHERE Id = @id",
            new Microsoft.Data.Sqlite.SqliteParameter("@now", now),
            new Microsoft.Data.Sqlite.SqliteParameter("@id", entity.Id));

        return ConvertToChecklist(entity);
    }

    public async Task<bool> UpdateChecklistItemAsync(string checklistId, string itemId, bool isCompleted)
    {
        var workspace = _workspaceResolver.GetCurrentWorkspace();
        
        var entity = await _context.Knowledge
            .FirstOrDefaultAsync(k => k.Id == checklistId 
                && k.Type == KnowledgeTypes.Checklist 
                && k.Workspace == workspace);

        if (entity == null) return false;

        var checklist = ConvertToChecklist(entity);
        var item = checklist.Items.FirstOrDefault(i => i.Id == itemId);
        
        if (item == null) return false;

        item.IsCompleted = isCompleted;
        item.CompletedAt = isCompleted ? DateTime.UtcNow : null;

        // Update entity metadata
        entity.Metadata = JsonSerializer.Serialize(new
        {
            checklist.Items,
            checklist.ParentChecklistId
        });
        entity.Status = checklist.Items.All(i => i.IsCompleted) ? "completed" : "active";
        entity.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<List<Checklist>> ListChecklistsAsync(bool includeCompleted = true, int maxResults = 20)
    {
        var workspace = _workspaceResolver.GetCurrentWorkspace();
        
        IQueryable<KnowledgeEntity> query = _context.Knowledge
            .Where(k => k.Type == KnowledgeTypes.Checklist && k.Workspace == workspace);

        if (!includeCompleted)
        {
            query = query.Where(k => k.Status != "completed");
        }

        var entities = await query
            .OrderByDescending(k => k.Id) // Use chronological ID for natural sorting
            .Take(maxResults)
            .ToListAsync();

        return entities.Select(ConvertToChecklist).ToList();
    }

    private Checklist ConvertToChecklist(KnowledgeEntity entity)
    {
        var metadata = !string.IsNullOrEmpty(entity.Metadata) 
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entity.Metadata) ?? new Dictionary<string, JsonElement>()
            : new Dictionary<string, JsonElement>();

        return new Checklist
        {
            Id = entity.Id,
            Content = entity.Content,
            Items = metadata.TryGetValue("Items", out var items) 
                ? JsonSerializer.Deserialize<List<ChecklistItem>>(items.GetRawText()) ?? new List<ChecklistItem>()
                : new List<ChecklistItem>(),
            ParentChecklistId = metadata.TryGetValue("ParentChecklistId", out var parentId) 
                ? parentId.GetString() 
                : null,
            CreatedAt = entity.CreatedAt,
            Workspace = entity.Workspace ?? string.Empty
        };
    }

    // Removed duplicate method - use ChronologicalId.Generate() instead
}