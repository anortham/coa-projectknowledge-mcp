using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Models;

/// <summary>
/// Checklist with trackable items
/// </summary>
public class Checklist : Knowledge
{
    public Checklist()
    {
        Type = KnowledgeTypes.Checklist;
        Id = ChronologicalId.Generate();
    }
    
    /// <summary>
    /// Checklist items
    /// </summary>
    public List<ChecklistItem> Items
    {
        get => GetMetadata<List<ChecklistItem>>("items") ?? new();
        set => SetMetadata("items", value);
    }
    
    /// <summary>
    /// Parent checklist ID for nested checklists
    /// </summary>
    public string? ParentChecklistId
    {
        get => GetMetadata<string>("parentChecklistId");
        set => SetMetadata("parentChecklistId", value);
    }
    
    /// <summary>
    /// Calculate completion percentage
    /// </summary>
    public double CompletionPercentage
    {
        get
        {
            var items = Items;
            if (items.Count == 0) return 0;
            
            var completed = items.Count(i => i.IsCompleted);
            return (double)completed / items.Count * 100;
        }
    }
    
    /// <summary>
    /// Get summary status
    /// </summary>
    public string GetStatus()
    {
        var total = Items.Count;
        var completed = Items.Count(i => i.IsCompleted);
        
        if (completed == 0) return "Not Started";
        if (completed == total) return "Completed";
        return $"In Progress ({completed}/{total})";
    }
}

/// <summary>
/// Individual checklist item
/// </summary>
public class ChecklistItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }
    public int Order { get; set; } = 0;
    public Dictionary<string, JsonElement> Metadata { get; set; } = new();
}