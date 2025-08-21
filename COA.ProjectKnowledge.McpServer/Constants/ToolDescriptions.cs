namespace COA.ProjectKnowledge.McpServer.Constants;

/// <summary>
/// Optimized tool descriptions that guide AI agents to use tools effectively
/// </summary>
public static class ToolDescriptions
{
    // Knowledge Management - Core Actions
    public const string StoreKnowledge = "ALWAYS store git commits as knowledge IMMEDIATELY after creating them. Remember and preserve: 1) Git commits (MANDATORY - include message, files changed, and impact), 2) Architectural decisions, 3) Technical debt notes, 4) Bug discoveries and fixes, 5) Lessons learned. Use IMMEDIATELY when: creating ANY git commit, making design decisions, discovering issues, or learning something significant. For git commits use: Type=WorkNote, Tags=['git-commit', feature-area], include commit hash and branch name. Never wait to be asked - proactively capture ALL significant moments.";
    
    public const string FindKnowledge = "Search and discover relevant knowledge with intelligent ranking that prioritizes recent and frequently accessed information. Supports advanced filtering by type, tags, status, priority, and date ranges. Uses temporal scoring to ensure the most relevant knowledge surfaces first. Perfect for questions like 'what did we decide about...?' or 'show me recent technical debt items'.";
    
    public const string DiscoverProjects = "Explore what projects and workspaces contain knowledge. Use proactively when starting work to understand what information is already available, or when you suspect relevant insights might exist in other projects.";
    
    public const string SearchAcrossProjects = "Search for information across multiple projects and workspaces simultaneously. Use when looking for insights that might exist in other projects, learning from similar work, or when a single project search isn't comprehensive enough.";
    
    // Activity & History
    public const string ShowActivity = "View chronological history and timeline of recent work activities. Use when you want to understand what's been happening recently, track progress over time, or get context about recent decisions and changes.";
    
    public const string GetTimelineAdvanced = "Generate detailed activity timeline with smart grouping and analysis. Perfect for standups ('What did I work on yesterday?'), progress reviews, and understanding work patterns. Groups activities by hour/day/week/type with statistics and summaries. Use when you need comprehensive activity analysis rather than just a simple list.";
    
    // Relationships
    public const string LinkKnowledge = "Connect related information, decisions, or concepts together. Use proactively when you notice relationships between different pieces of work, when decisions depend on each other, or when you want to build understanding of how things relate.";
    
    public const string FindConnections = "Explore what's related to or connected with a piece of information. Use when you want to understand the broader context, find related decisions, or discover what else might be impacted by changes.";
    
    // Export & Sharing
    public const string ExportKnowledge = "Export knowledge to external formats like Obsidian markdown files. Use when you want to share knowledge outside the system, create backups, or integrate with other knowledge management tools.";
    
    // Federation
    public const string SearchFederation = "Search for information across federated ProjectKnowledge hubs using MCP protocol. Use when you need to find knowledge from remote teams, partner organizations, or distributed knowledge bases.";
}