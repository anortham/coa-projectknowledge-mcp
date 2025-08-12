using System.Text.Json;
using System.Text.Json.Serialization;

namespace COA.ProjectKnowledge.McpServer.Models;

/// <summary>
/// Simplified knowledge types - reduced from 44+ to 5 core types
/// </summary>
public static class KnowledgeTypes
{
    public const string Checkpoint = "Checkpoint";
    public const string Checklist = "Checklist";
    public const string TechnicalDebt = "TechnicalDebt";
    public const string ProjectInsight = "ProjectInsight";
    public const string WorkNote = "WorkNote";
    
    public static readonly HashSet<string> ValidTypes = new()
    {
        Checkpoint, Checklist, TechnicalDebt, ProjectInsight, WorkNote
    };
}

/// <summary>
/// Core knowledge entry model
/// </summary>
public class Knowledge
{
    public string Id { get; set; } = ChronologicalId.Generate();
    public string Type { get; set; } = KnowledgeTypes.WorkNote;
    public string Content { get; set; } = string.Empty;
    public List<CodeSnippet> CodeSnippets { get; set; } = new();
    public Dictionary<string, JsonElement> Metadata { get; set; } = new();
    public string Workspace { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AccessedAt { get; set; }
    public int AccessCount { get; set; } = 0;
    public DateTime? ArchivedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    
    // Computed properties
    public bool IsArchived => ArchivedAt.HasValue;
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
    
    // Helper methods for metadata
    public T? GetMetadata<T>(string key)
    {
        if (Metadata.TryGetValue(key, out var element))
        {
            try
            {
                return JsonSerializer.Deserialize<T>(element.GetRawText());
            }
            catch
            {
                return default;
            }
        }
        return default;
    }
    
    public void SetMetadata<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        Metadata[key] = JsonDocument.Parse(json).RootElement;
    }
    
    // Common metadata properties
    public string? Status => GetMetadata<string>("status");
    public string? Priority => GetMetadata<string>("priority");
    public string[]? Tags => GetMetadata<string[]>("tags");
    public string[]? RelatedTo => GetMetadata<string[]>("relatedTo");
}

/// <summary>
/// Code snippet with syntax information
/// </summary>
public class CodeSnippet
{
    public string Language { get; set; } = "plaintext";
    public string Code { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public int? StartLine { get; set; }
    public int? EndLine { get; set; }
    
    /// <summary>
    /// Get markdown representation for export
    /// </summary>
    public string ToMarkdown()
    {
        var header = FilePath != null ? $"// {FilePath}" : "";
        if (StartLine.HasValue && EndLine.HasValue)
        {
            header += $" (lines {StartLine}-{EndLine})";
        }
        
        return $"```{Language}\n{header}\n{Code}\n```";
    }
}