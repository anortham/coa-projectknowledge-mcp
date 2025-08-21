namespace COA.ProjectKnowledge.McpServer.Constants;

/// <summary>
/// Centralized tool names for consistency and easy renaming
/// </summary>
public static class ToolNames
{
    // Knowledge Management - Core Actions
    public const string StoreKnowledge = "store_knowledge";
    public const string FindKnowledge = "find_knowledge"; // Better than "search_knowledge"
    public const string DiscoverProjects = "discover_projects"; // Better than "get_workspaces" 
    public const string SearchAcrossProjects = "search_across_projects"; // Better than "search_cross_project"
    
    // Activity & History
    public const string ShowActivity = "show_activity"; // Better than "get_timeline"
    public const string GetTimelineAdvanced = "get_timeline_advanced"; // Enhanced timeline with grouping and analysis
    
    // Relationships
    public const string LinkKnowledge = "link_knowledge"; // Better than "create_relationship"
    public const string FindConnections = "find_connections"; // Better than "get_relationships"
    
    // Export & Sharing
    public const string ExportKnowledge = "export_knowledge";
    
    // Federation
    public const string SearchFederation = "search_federation";
}