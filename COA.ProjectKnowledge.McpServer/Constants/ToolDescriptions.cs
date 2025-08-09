namespace COA.ProjectKnowledge.McpServer.Constants;

/// <summary>
/// Optimized tool descriptions that guide AI agents to use tools effectively
/// </summary>
public static class ToolDescriptions
{
    // Knowledge Management - Core Actions
    public const string StoreKnowledge = "Capture and save important information, insights, decisions, or findings to the knowledge base. Use when you want to remember something for later or share knowledge across projects.";
    
    public const string FindKnowledge = "Search and discover relevant information from the knowledge base. Use when you need to find previous insights, decisions, or information related to your current work.";
    
    public const string DiscoverProjects = "Find all available projects and workspaces that contain knowledge. Use when you want to see what projects have shared knowledge or to understand the scope of available information.";
    
    public const string SearchAcrossProjects = "Search for information across multiple projects and workspaces simultaneously. Use when looking for insights that might exist in other projects or for cross-project learning.";
    
    // Session & State Management  
    public const string SaveCheckpoint = "Save the current state of your work session for later resumption. Use when completing a major milestone, before switching contexts, or to create restoration points.";
    
    public const string LoadCheckpoint = "Retrieve and restore a previous work session state. Use when resuming work, recovering from interruptions, or accessing previous session context.";
    
    public const string ListCheckpoints = "View all available checkpoints for a session to understand work history and choose restoration points. Use when you need to see session timeline or find specific checkpoints.";
    
    // Activity & History
    public const string ShowActivity = "View chronological history and timeline of recent knowledge activities. Use when you want to understand recent work patterns, see what's been happening, or track progress over time.";
    
    // Task Management
    public const string CreateChecklist = "Create a new task list with items to track and manage. Use when you need to organize work, create TODO lists, or track completion of multiple related tasks.";
    
    public const string ViewChecklist = "View an existing checklist with current completion status. Use when you want to check progress on tasks, see what's completed, or review outstanding work items.";
    
    public const string UpdateTask = "Mark checklist items as completed or update their status. Use when you finish tasks, want to track progress, or need to update task completion state.";
    
    // Relationships
    public const string LinkKnowledge = "Create connections and relationships between different pieces of knowledge. Use when you want to show how concepts relate, create knowledge graphs, or establish information dependencies.";
    
    public const string FindConnections = "Discover existing relationships and connections for a piece of knowledge. Use when exploring related information, understanding knowledge networks, or finding connected insights.";
    
    // Export & Sharing
    public const string ExportKnowledge = "Export knowledge to external formats like Obsidian markdown files. Use when you want to share knowledge outside the system, create backups, or integrate with other knowledge management tools.";
}