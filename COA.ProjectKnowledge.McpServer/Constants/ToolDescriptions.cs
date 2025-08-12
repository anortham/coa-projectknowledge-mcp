namespace COA.ProjectKnowledge.McpServer.Constants;

/// <summary>
/// Optimized tool descriptions that guide AI agents to use tools effectively
/// </summary>
public static class ToolDescriptions
{
    // Knowledge Management - Core Actions
    public const string StoreKnowledge = "Remember and preserve important information, insights, decisions, or findings for future reference. Perfect for architectural decisions, technical debt notes, lessons learned, bug analysis, or any valuable context that should be retained. Use proactively whenever you encounter something worth remembering - don't wait to be asked.";
    
    public const string FindKnowledge = "Search and discover relevant knowledge with intelligent ranking that prioritizes recent and frequently accessed information. Supports advanced filtering by type, tags, status, priority, and date ranges. Uses temporal scoring to ensure the most relevant knowledge surfaces first. Perfect for questions like 'what did we decide about...?' or 'show me recent technical debt items'.";
    
    public const string DiscoverProjects = "Explore what projects and workspaces contain knowledge. Use proactively when starting work to understand what information is already available, or when you suspect relevant insights might exist in other projects.";
    
    public const string SearchAcrossProjects = "Search for information across multiple projects and workspaces simultaneously. Use when looking for insights that might exist in other projects, learning from similar work, or when a single project search isn't comprehensive enough.";
    
    // Session & State Management  
    public const string SaveCheckpoint = "Save the current state of your work session for later resumption. Use when completing a major milestone, before switching contexts, or to create restoration points.";
    
    public const string LoadCheckpoint = "Retrieve and restore a previous work session state. Use when resuming work, recovering from interruptions, or accessing previous session context.";
    
    public const string ListCheckpoints = "View all available checkpoints for a session to understand work history and choose restoration points. Use when you need to see session timeline or find specific checkpoints.";
    
    // Activity & History
    public const string ShowActivity = "View chronological history and timeline of recent work activities. Use when you want to understand what's been happening recently, track progress over time, or get context about recent decisions and changes.";
    
    // Task Management
    public const string CreateChecklist = "Create a new task list with items to track and manage. Use when you need to organize work, create TODO lists, or track completion of multiple related tasks.";
    
    public const string ViewChecklist = "View an existing checklist with current completion status. Use when you want to check progress on tasks, see what's completed, or review outstanding work items.";
    
    public const string UpdateTask = "Mark checklist items as completed or update their status. Use when you finish tasks, want to track progress, or need to update task completion state.";
    
    // Relationships
    public const string LinkKnowledge = "Connect related information, decisions, or concepts together. Use proactively when you notice relationships between different pieces of work, when decisions depend on each other, or when you want to build understanding of how things relate.";
    
    public const string FindConnections = "Explore what's related to or connected with a piece of information. Use when you want to understand the broader context, find related decisions, or discover what else might be impacted by changes.";
    
    // Export & Sharing
    public const string ExportKnowledge = "Export knowledge to external formats like Obsidian markdown files. Use when you want to share knowledge outside the system, create backups, or integrate with other knowledge management tools.";
    
    // Federation
    public const string SearchFederation = "Search for information across federated ProjectKnowledge hubs using MCP protocol. Use when you need to find knowledge from remote teams, partner organizations, or distributed knowledge bases.";
}