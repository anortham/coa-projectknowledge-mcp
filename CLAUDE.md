# COA ProjectKnowledge MCP - Project Context

## Overview
This is a fully-functional knowledge management MCP server that reduces complexity from 44+ memory types to just 5 core types. It provides cross-platform user-level knowledge storage with SQLite, federation capabilities, and optimized AI-friendly tool names.

## Key Design Decisions

### Simplification Strategy
- **5 Core Types Only**: Checkpoint, Checklist, TechnicalDebt, ProjectInsight, WorkNote
- **User-Level Storage**: Cross-platform storage at `~/.coa/knowledge/knowledge.db`
- **JSON Metadata**: Flexible schema using JSON fields for extensibility
- **Chronological IDs**: Time-based IDs for natural sorting and performance

### Technology Stack
- **.NET 9.0** with COA.Mcp.Framework 1.4.2
- **SQLite** with full-text search (FTS5)
- **Dual-Mode Operation**: STDIO for Claude Code, HTTP for federation
- **Global .NET Tool**: Installable via `dotnet tool install`

## Available MCP Tools

### Knowledge Management
- `store_knowledge` - Capture and save important information, insights, decisions, or findings
- `find_knowledge` - Search and discover relevant information from the knowledge base

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

## Architecture Notes

### Service Layer
- **KnowledgeService**: Core CRUD operations with chronological ID optimization
- **CheckpointService**: Session-based checkpoint management
- **ChecklistService**: Task list management with completion tracking
- **RelationshipService**: Knowledge linking and relationship management
- **FederationService**: Cross-project knowledge sharing
- **WorkspaceResolver**: Auto-detects workspace from Git root

### Storage Layer
- **KnowledgeDbContext**: EF Core with SQLite operations
- **ChronologicalId**: Time-based ID generation for natural sorting
- **Access Tracking**: Automatic tracking of access patterns and usage stats

### Framework Integration
- **ToolNames Constants**: Centralized tool naming for consistency
- **ToolDescriptions Constants**: AI-optimized descriptions for better agent guidance
- **Auto-Discovery**: Tools automatically registered via inheritance
- **Dual Mode**: STDIO + HTTP server capabilities

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

## Common Development Tasks

### Adding a New Knowledge Type
1. Add constant to `KnowledgeTypes` class (if needed)
2. Update `ToolDescriptions` for any new tools
3. Add service methods if specialized behavior needed
4. Create MCP tool inheriting from `McpToolBase`

### Testing Tools After Changes
**IMPORTANT**: After making code changes:
1. **YOU (the user) must exit Claude Code** - the MCP server runs in memory
2. Build in release mode: `dotnet build -c Release`  
3. **YOU (the user) must restart Claude Code** to reload the MCP server
4. Only then will changes be active for testing

**CRITICAL**: While Claude Code is running:
- **NEVER** build in Release mode - it's locked by the running MCP server!
- **ONLY** build in Debug mode: `dotnet build -c Debug`
- Release builds will fail with file lock errors since the MCP server is using those files

### Debugging
- **STDIO Mode**: Logs suppressed to prevent JSON-RPC corruption
- **HTTP Mode**: Full logging available via `--mode http --port 5100`
- **Database Access**: Query SQLite directly at `~/.coa/knowledge/knowledge.db`
- **Serilog**: File-only logging to `~/.coa/knowledge/logs/`

## Implementation Status

✅ **Production Ready**
- All 5 core knowledge types implemented
- Complete MCP tool suite with AI-optimized names/descriptions
- Chronological ID optimization for performance
- Cross-project federation working
- Cross-platform user-level storage
- Relationship management system
- Export capabilities
- Global .NET tool packaging

✅ **Federation System**
- HTTP server for federation hub
- API endpoints for external clients
- Cross-project search capabilities
- Health monitoring and statistics

✅ **Quality & Performance**
- Service lifetime issues resolved
- Chronological ID database optimization
- Datetime handling fixed (local time support)
- Quote handling in workspace queries
- Comprehensive error handling

## Testing Patterns

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

### Cross-Project Search
```
mcp__projectknowledge__search_across_projects  
Query: "authentication JWT"
Workspaces: ["My Web App", "API Gateway Project"]
MaxResults: 10
```

## Migration from COA CodeSearch
Automated migration strategy available:
- Checkpoint → Checkpoint (direct mapping)
- Checklist/ChecklistItem → Checklist (combined structure)
- TechnicalDebt/Blocker/BugReport → TechnicalDebt
- ArchitecturalDecision/CodePattern → ProjectInsight
- All others → WorkNote (catch-all)

## Slash Commands Available
- `/checkpoint` - Create structured checkpoint with current work state
- `/resume` - Load latest checkpoint and restore work session

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

The system is **production-ready** for immediate deployment and cross-project knowledge sharing!

## Framework Best Practices Implemented

ProjectKnowledge serves as a **reference implementation** for the COA MCP Framework, showcasing:

### 1. ✅ Enhanced Error Handling
- Centralized `ErrorHelpers` class for consistent error responses
- Recovery steps and suggested actions in all tools
- Context-specific error guidance

### 2. ✅ Resource Providers for Large Data
- `KnowledgeResourceProvider` automatically handles datasets >30-50 items
- Returns resource URIs instead of inline data
- 15-minute cache for frequently accessed resources

### 3. ✅ Token Optimization
- `COA.Mcp.Framework.TokenOptimization` package integrated
- Smart truncation with resource fallback
- `ToolExecutionMetadata` for token tracking

### 4. ✅ Interactive Prompts
- `KnowledgeCapturePrompt` - Guided knowledge entry
- `CheckpointReviewPrompt` - Session restoration workflow
- Context-aware with variable substitution

### 5. ✅ Dual-Mode Architecture
- STDIO for Claude Code integration
- HTTP for federation hub
- Auto-service management for background HTTP server

### 6. ✅ Proper Service Registration
- Scoped services for per-request lifetime
- Singleton services for application lifetime
- Auto-discovery of tools and prompts

For detailed implementation patterns, see `Documentation/FrameworkBestPractices.md`