namespace COA.ProjectKnowledge.McpServer.Constants;

/// <summary>
/// Defines the 3 core knowledge types
/// </summary>
public static class KnowledgeTypes
{
    public const string TechnicalDebt = "TechnicalDebt";
    public const string ProjectInsight = "ProjectInsight";
    public const string WorkNote = "WorkNote";
    
    /// <summary>
    /// Get all valid knowledge types
    /// </summary>
    public static readonly string[] All = new[]
    {
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