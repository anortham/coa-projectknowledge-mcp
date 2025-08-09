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
    
    // Session & State Management
    public const string SaveCheckpoint = "save_checkpoint"; // Better than "create_checkpoint"
    public const string LoadCheckpoint = "load_checkpoint"; // Better than "get_checkpoint"
    public const string ListCheckpoints = "list_checkpoints";
    
    // Activity & History
    public const string ShowActivity = "show_activity"; // Better than "get_timeline"
    
    // Task Management
    public const string CreateChecklist = "create_checklist";
    public const string ViewChecklist = "view_checklist"; // Better than "get_checklist"
    public const string UpdateTask = "update_task"; // Better than "update_checklist_item"
    
    // Relationships
    public const string LinkKnowledge = "link_knowledge"; // Better than "create_relationship"
    public const string FindConnections = "find_connections"; // Better than "get_relationships"
    
    // Export & Sharing
    public const string ExportKnowledge = "export_knowledge";
}