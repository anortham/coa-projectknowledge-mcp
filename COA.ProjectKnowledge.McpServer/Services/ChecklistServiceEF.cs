using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Data.Entities;
using COA.ProjectKnowledge.McpServer.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Services;

public class ChecklistServiceEF
{
    private readonly KnowledgeDbContext _context;
    private readonly IWorkspaceResolver _workspaceResolver;

    public ChecklistServiceEF(KnowledgeDbContext context, IWorkspaceResolver workspaceResolver)
    {
        _context = context;
        _workspaceResolver = workspaceResolver;
    }

    public async Task<Checklist> CreateChecklistAsync(string content, List<string> items, string? parentChecklistId = null)
    {
        var workspace = _workspaceResolver.GetCurrentWorkspace();
        
        var checklist = new Checklist
        {
            Id = GenerateChronologicalId(),
            Content = content,
            Items = items.Select((item, index) => new ChecklistItem
            {
                Id = $"{GenerateChronologicalId()}-item{index}",
                Content = item,
                IsCompleted = false,
                CompletedAt = null
            }).ToList(),
            ParentChecklistId = parentChecklistId,
            CreatedAt = DateTime.UtcNow,
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
                checklist.ParentChecklistId,
                checklist.TotalItems,
                checklist.CompletedItems,
                checklist.IsCompleted
            }),
            Tags = JsonSerializer.Serialize(new List<string> { "checklist" }),
            Priority = "normal",
            Status = checklist.IsCompleted ? "completed" : "active",
            Workspace = checklist.Workspace,
            CreatedAt = checklist.CreatedAt,
            UpdatedAt = checklist.CreatedAt,
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

        // Update access tracking
        entity.AccessedAt = DateTime.UtcNow;
        entity.AccessCount++;
        await _context.SaveChangesAsync();

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
            checklist.ParentChecklistId,
            checklist.TotalItems,
            checklist.CompletedItems,
            checklist.IsCompleted
        });
        entity.Status = checklist.IsCompleted ? "completed" : "active";
        entity.UpdatedAt = DateTime.UtcNow;

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
            .OrderByDescending(k => k.CreatedAt)
            .Take(maxResults)
            .ToListAsync();

        return entities.Select(ConvertToChecklist).ToList();
    }

    private Checklist ConvertToChecklist(KnowledgeEntity entity)
    {
        var metadata = !string.IsNullOrEmpty(entity.Metadata) 
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entity.Metadata)
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
            Workspace = entity.Workspace
        };
    }

    private string GenerateChronologicalId()
    {
        var timestamp = DateTime.UtcNow;
        var random = Random.Shared.Next(1000, 9999);
        return $"{timestamp:yyyyMMddHHmmssfff}-{random}";
    }
}