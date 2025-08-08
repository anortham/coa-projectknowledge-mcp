using COA.ProjectKnowledge.McpServer.Models;
using Microsoft.Extensions.Logging;

namespace COA.ProjectKnowledge.McpServer.Services;

public class ChecklistService
{
    private readonly KnowledgeService _knowledgeService;
    private readonly ILogger<ChecklistService> _logger;
    
    public ChecklistService(
        KnowledgeService knowledgeService,
        ILogger<ChecklistService> logger)
    {
        _knowledgeService = knowledgeService;
        _logger = logger;
    }
    
    public async Task<Checklist> CreateChecklistAsync(string content, List<string> items, string? parentChecklistId = null)
    {
        var checklist = new Checklist
        {
            Content = content,
            ParentChecklistId = parentChecklistId,
            Items = items.Select((item, index) => new ChecklistItem
            {
                Content = item,
                Order = index,
                IsCompleted = false
            }).ToList()
        };
        
        await _knowledgeService.StoreAsync(checklist);
        
        _logger.LogInformation("Created checklist {Id} with {Count} items", 
            checklist.Id, checklist.Items.Count);
        
        return checklist;
    }
    
    public async Task<Checklist?> GetChecklistAsync(string checklistId)
    {
        var knowledge = await _knowledgeService.GetByIdAsync(checklistId);
        return knowledge as Checklist;
    }
    
    public async Task<Checklist?> UpdateChecklistItemAsync(string checklistId, string itemId, bool isCompleted)
    {
        var updatedChecklist = await _knowledgeService.UpdateAsync(checklistId, k =>
        {
            if (k is Checklist cl)
            {
                // Get the items list once
                var items = cl.Items;
                var item = items.FirstOrDefault(i => i.Id == itemId);
                if (item != null)
                {
                    item.IsCompleted = isCompleted;
                    item.CompletedAt = isCompleted ? DateTime.UtcNow : null;
                    
                    // Set the modified list back to trigger SetMetadata
                    cl.Items = items;
                }
            }
        }) as Checklist;
        
        if (updatedChecklist == null)
        {
            _logger.LogWarning("Checklist not found: {Id}", checklistId);
            return null;
        }
        
        var item = updatedChecklist.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null)
        {
            _logger.LogWarning("Checklist item not found: {ItemId} in checklist {ChecklistId}", 
                itemId, checklistId);
            return null;
        }
        
        _logger.LogInformation("Updated checklist item {ItemId} in checklist {ChecklistId}: completed={IsCompleted}",
            itemId, checklistId, isCompleted);
        
        return updatedChecklist;
    }
    
    public async Task<List<Checklist>> GetActiveChecklistsAsync(string? workspace = null, int maxResults = 20)
    {
        var query = $"type:{KnowledgeTypes.Checklist}";
        var results = await _knowledgeService.SearchAsync(query, workspace, maxResults);
        
        return results.OfType<Checklist>()
            .Where(c => c.CompletionPercentage < 100)
            .OrderByDescending(c => c.ModifiedAt)
            .ToList();
    }
    
    public async Task<Checklist?> AddChecklistItemAsync(string checklistId, string itemContent)
    {
        var checklist = await GetChecklistAsync(checklistId);
        if (checklist == null)
        {
            _logger.LogWarning("Checklist not found: {Id}", checklistId);
            return null;
        }
        
        var newItem = new ChecklistItem
        {
            Content = itemContent,
            Order = checklist.Items.Count,
            IsCompleted = false
        };
        
        checklist.Items.Add(newItem);
        
        await _knowledgeService.UpdateAsync(checklistId, k =>
        {
            if (k is Checklist cl)
            {
                cl.Items = checklist.Items;
            }
        });
        
        _logger.LogInformation("Added item to checklist {ChecklistId}", checklistId);
        
        return checklist;
    }
}