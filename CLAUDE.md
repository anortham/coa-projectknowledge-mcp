# COA ProjectKnowledge MCP - Project Context

## Overview
This is a simplified knowledge management MCP server that reduces complexity from 44+ memory types to just 5 core types. It provides workspace-level knowledge storage with SQLite and federation capabilities.

## Key Design Decisions

### Simplification Strategy
- **5 Core Types Only**: Checkpoint, Checklist, TechnicalDebt, ProjectInsight, WorkNote
- **Workspace-Level Storage**: Not per-project, but per-workspace (Git root)
- **JSON Metadata**: Flexible schema using JSON fields for extensibility
- **Chronological IDs**: Time-based IDs for natural sorting

### Technology Stack
- **.NET 9.0** with COA.Mcp.Framework 1.4.2
- **SQLite** with full-text search (FTS5)
- **Dual-Mode Operation**: STDIO for Claude Code, HTTP for federation

## Available MCP Tools

### Knowledge Management
- `store_knowledge` - Store knowledge with type, content, tags, priority, status
- `search_knowledge` - Search knowledge base with FTS support

### Checkpoint System (Session State)
- `create_checkpoint` - Save current work state with session tracking
- `get_checkpoint` - Retrieve latest or specific checkpoint
- `list_checkpoints` - View checkpoint timeline for a session

### Checklist System (In Progress)
- Tools being implemented for task tracking

## Database Location
- Default: `C:\source\.coa\knowledge\workspace.db`
- Configurable via appsettings.json

## Architecture Notes

### Service Layer
- **KnowledgeService**: Core CRUD operations for all knowledge types
- **CheckpointService**: Session-based checkpoint management
- **ChecklistService**: Task list management with completion tracking
- **WorkspaceResolver**: Auto-detects workspace from Git root

### Storage Layer
- **KnowledgeDatabase**: SQLite operations with FTS
- **Migrations**: SQL schema in Storage/Migrations/
- **Access Tracking**: Automatic tracking of access patterns

### Framework Integration
- Uses COA.Mcp.Framework's McpServerBuilder pattern
- Tools auto-discovered via [McpServerToolType] attribute
- No SetBasePath for config (uses default like CodeNav)

## Common Development Tasks

### Adding a New Knowledge Type
1. Add constant to `KnowledgeTypes` class
2. Create specialized model if needed (like Checkpoint/Checklist)
3. Add service methods if specialized behavior needed
4. Create MCP tool for operations

### Testing Tools
After building, restart Claude Code to reload the MCP server. Tools should appear in the MCP tools list.

### Debugging
- Logs are suppressed in STDIO mode to prevent breaking JSON-RPC
- Use HTTP mode (`--mode http`) for debugging with console output
- Check SQLite database directly at the configured path

## Implementation Status

✅ **Completed**
- Core models and database layer
- Knowledge storage and search
- Checkpoint system with tools
- Checklist service (tool pending)

🚧 **In Progress**
- Checklist MCP tool
- Relationship management

📋 **Planned**
- Federation service and HTTP API
- Migration from COA CodeSearch
- Web UI for browsing knowledge

## Known Issues
- BuildServiceProvider warning (harmless, needed for DB init)
- Auto-service for federation disabled (needs testing)

## Testing in Claude Code

**IMPORTANT**: When making code changes to the MCP server:
1. You cannot test changes immediately - the MCP server is already loaded in memory
2. You CANNOT build in release mode while Claude Code is running - it has the release DLLs locked
3. The user must:
   - Exit Claude Code first
   - Build in release mode: `dotnet build -c Release`
   - Restart Claude Code to reload the MCP server
4. Only then will the changes take effect for testing
5. You can build in Debug mode while Claude Code is running: `dotnet build` or `dotnet build -c Debug`

## Testing Patterns

### Store Knowledge
```
Type: ProjectInsight
Content: "Authentication uses JWT tokens with 1-hour expiry"
Tags: ["auth", "security", "jwt"]
Priority: high
Status: documented
```

### Create Checkpoint
```
Content: "Implemented user authentication with JWT"
SessionId: "auth-implementation-2024"
ActiveFiles: ["AuthService.cs", "JwtHelper.cs"]
```

## Migration from COA CodeSearch
Type mapping strategy:
- Checkpoint → Checkpoint (direct)
- Checklist/ChecklistItem → Checklist (combined)
- TechnicalDebt/Blocker/BugReport → TechnicalDebt
- ArchitecturalDecision/CodePattern → ProjectInsight
- All others → WorkNote

## Federation Design (Future)
- HTTP API on port 5100 (configurable)
- Auto-starts from STDIO mode when enabled
- Allows cross-project knowledge sharing
- Health endpoint: /api/knowledge/health