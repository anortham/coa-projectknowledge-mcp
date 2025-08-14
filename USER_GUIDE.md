# COA ProjectKnowledge MCP - User Guide

## Overview
COA ProjectKnowledge MCP is a fully-functional knowledge management MCP server that reduces complexity from 44+ memory types to just 5 core types. It provides cross-platform user-level knowledge storage with SQLite, federation capabilities, and optimized AI-friendly tool names.

## Key Features
- **5 Core Types Only**: Checkpoint, Checklist, TechnicalDebt, ProjectInsight, WorkNote
- **User-Level Storage**: Cross-platform storage at `~/.coa/knowledge/knowledge.db`
- **JSON Metadata**: Flexible schema using JSON fields for extensibility
- **Chronological IDs**: Time-based IDs for natural sorting and performance
- **Dual-Mode Operation**: STDIO for Claude Code, HTTP for federation
- **Global .NET Tool**: Installable via `dotnet tool install`

## Available MCP Tools

### Knowledge Management
- `store_knowledge` - Capture and save important information, insights, decisions, or findings
- `find_knowledge` - Enhanced temporal search with intelligent ranking, advanced filtering, and temporal scoring

### Session & State Management  
- `save_checkpoint` - Save current work state for later resumption with session tracking
- `load_checkpoint` - Retrieve and restore previous work session state
- `list_checkpoints` - View checkpoint timeline for a session

### Activity & History
- `show_activity` - View chronological history and timeline of recent knowledge activities

### Task Management
- `create_checklist` - Create a new task list with items to track
- `view_checklist` - View checklist with current completion status  
- `update_task` - Update checklist item completion status

### Cross-Project Discovery
- `discover_projects` - Find all available projects and workspaces that contain knowledge
- `search_across_projects` - Search for information across multiple projects simultaneously

### Relationships
- `link_knowledge` - Create connections and relationships between knowledge items
- `find_connections` - Discover existing relationships for knowledge items

### Export & Sharing
- `export_knowledge` - Export knowledge to Obsidian-compatible markdown files

## Database Location
- **Cross-Platform**: `~/.coa/knowledge/knowledge.db` (Linux/macOS) or `%USERPROFILE%\.coa\knowledge\knowledge.db` (Windows)
- **Federation Ready**: Single database shared across all projects for knowledge federation
- **Configurable**: Override via appsettings.json if needed

## Enhanced Search Features

### Temporal Knowledge Search
The `find_knowledge` tool includes advanced temporal scoring to surface the most relevant knowledge:

#### Temporal Scoring Modes
- **None**: No temporal scoring - pure relevance matching
- **Default**: Moderate decay over 30 days (recommended for most use cases)
- **Aggressive**: Strong preference for recent knowledge (7-day half-life)
- **Gentle**: Slow decay over long periods (90-day half-life)

#### Advanced Filtering Options
- **Type Filtering**: Filter by knowledge types (ProjectInsight, TechnicalDebt, etc.)
- **Tag Filtering**: Search by specific tags (any match)
- **Status Filtering**: Filter by status values (active, completed, archived, etc.)
- **Priority Filtering**: Filter by priority levels (low, normal, high, critical)
- **Date Range Filtering**: Limit results to specific creation date ranges
- **Workspace Filtering**: Search within specific workspaces or across all

#### Boost Options
- **BoostRecent**: Prioritize recently modified knowledge (default: true)
- **BoostFrequent**: Prioritize frequently accessed knowledge (default: false)

#### Sorting & Ordering
- **OrderBy**: Sort by created, modified, accessed, accesscount, or relevance
- **OrderDescending**: Control ascending/descending order (default: true)

## Usage Examples

### Store Knowledge
```
mcp__projectknowledge__store_knowledge
Type: ProjectInsight
Content: "Authentication uses JWT tokens with 1-hour expiry"
Tags: ["auth", "security", "jwt"]
Priority: high
Status: documented
```

### Save Checkpoint  
```
mcp__projectknowledge__save_checkpoint
Content: "## Accomplished\n- Implemented JWT authentication\n- Added user registration\n## Next Steps\n1. Add role-based permissions\n2. Test security flows"
SessionId: "auth-implementation-2025-08-08"
ActiveFiles: ["AuthService.cs", "JwtHelper.cs"]
```

### Enhanced Knowledge Search
```
mcp__projectknowledge__find_knowledge
Query: "authentication JWT"
TemporalScoring: Default
Types: ["ProjectInsight", "TechnicalDebt"]
Tags: ["security", "jwt"]
BoostRecent: true
MaxResults: 10
```

### Cross-Project Search
```
mcp__projectknowledge__search_across_projects  
Query: "authentication JWT"
Workspaces: ["My Web App", "API Gateway Project"]
MaxResults: 10
```

## Installation & Usage

### As Global Tool
```bash
dotnet build -c Release
dotnet tool install --global --add-source ./COA.ProjectKnowledge.McpServer/bin/Release projectknowledge

# Start federation hub
projectknowledge --mode http --port 5100
```

### Configuration
- **Claude Code**: Automatically configured via MCP discovery
- **Federation**: Configure other MCP clients to connect to hub
- **Database**: Auto-creates at user profile location
- **Logging**: Auto-configures to user profile logs folder

## Slash Commands Available
- `/checkpoint` - Create structured checkpoint with current work state
- `/resume` - Load latest checkpoint and restore work session

## Federation Architecture (Hub-and-Spoke Model)

### Single Machine, Multiple Clients
- **ONE** ProjectKnowledge instance per developer machine (STDIO + HTTP)
- **OTHER** MCP servers (SQL Analyzer, Web Tools, etc.) send knowledge TO the hub
- **NO** cross-machine federation (single machine architecture)
- **Cross-workspace** search within local database only

### API Endpoints (For MCP Client Integration)
- `POST /api/knowledge/store` - Receive knowledge from MCP clients
- `POST /api/knowledge/batch` - Batch operations from clients
- `POST /api/knowledge/contribute` - External tool contributions
- `GET /api/knowledge/health` - Health check with statistics

## Migration from COA CodeSearch
Automated migration strategy available:
- Checkpoint → Checkpoint (direct mapping)
- Checklist/ChecklistItem → Checklist (combined structure)
- TechnicalDebt/Blocker/BugReport → TechnicalDebt
- ArchitecturalDecision/CodePattern → ProjectInsight
- All others → WorkNote (catch-all)

The system is **production-ready** for immediate deployment and cross-project knowledge sharing!