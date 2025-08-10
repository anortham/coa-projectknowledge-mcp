namespace COA.ProjectKnowledge.McpServer.Constants;

/// <summary>
/// Defines the 5 core knowledge types
/// </summary>
public static class KnowledgeTypes
{
    public const string Checkpoint = "Checkpoint";
    public const string Checklist = "Checklist";
    public const string TechnicalDebt = "TechnicalDebt";
    public const string ProjectInsight = "ProjectInsight";
    public const string WorkNote = "WorkNote";
    
    /// <summary>
    /// Get all valid knowledge types
    /// </summary>
    public static readonly string[] All = new[]
    {
        Checkpoint,
        Checklist,
        TechnicalDebt,
        ProjectInsight,
        WorkNote
    };
    
    /// <summary>
    /// Check if a type is valid
    /// </summary>
    public static bool IsValid(string? type)
    {
        return type != null && All.Contains(type, StringComparer.OrdinalIgnoreCase);
    }
}