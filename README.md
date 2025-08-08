# COA ProjectKnowledge MCP

A simplified knowledge management system for MCP (Model Context Protocol) that centralizes workspace-level knowledge storage with federation capabilities.

## Quick Start

### Prerequisites
- .NET 9.0 SDK
- Visual Studio 2022 or VS Code
- Access to COA NuGet feed (for COA.Mcp.Framework)

### Build and Run

```bash
# Build the project
cd COA.ProjectKnowledge.McpServer
dotnet restore --configfile ../NuGet.config.local
dotnet build

# Run in STDIO mode (for Claude Code)
dotnet run -- stdio

# Run in HTTP mode (for federation)
dotnet run -- --mode http
```

### Configure in Claude Code

Add to your MCP settings:

```json
{
  "mcpServers": {
    "projectknowledge": {
      "command": "dotnet",
      "args": ["C:/source/COA ProjectKnowledge MCP/COA.ProjectKnowledge.McpServer/bin/Debug/net9.0/COA.ProjectKnowledge.McpServer.dll"],
      "env": {}
    }
  }
}
```

## Features Implemented

✅ **Core Models**
- Knowledge base with 5 simplified types (reduced from 44+)
- Checkpoint and Checklist support
- Chronological ID generation for natural sorting
- JSON metadata for flexibility

✅ **Database Layer**
- SQLite storage with full-text search
- Automatic schema creation
- Access tracking and archival support

✅ **MCP Tools**
- `store_knowledge` - Store knowledge with metadata
- `search_knowledge` - Search with FTS support

✅ **Services**
- KnowledgeService for CRUD operations
- WorkspaceResolver for automatic workspace detection

## Knowledge Types

1. **Checkpoint** - Session state persistence
2. **Checklist** - Trackable task lists
3. **TechnicalDebt** - Issues and blockers
4. **ProjectInsight** - Architectural decisions and patterns
5. **WorkNote** - General development notes

## Configuration

Edit `appsettings.json` to customize:
- Database location
- Federation settings
- Workspace detection strategy

## Architecture

The system follows the blueprint in `docs/IMPLEMENTATION_GUIDE.md`:
- Simplified from 44+ memory types to 5 core types
- SQLite database with JSON fields for flexibility
- MCP framework 1.4.2 with auto-service support
- Dual-mode operation (STDIO for Claude, HTTP for federation)

## Next Steps

- Add remaining MCP tools (Checkpoint, Checklist, Relationship)
- Implement federation API endpoints
- Add migration tool from COA CodeSearch
- Create comprehensive test suite
- Implement remaining services (RelationshipService, FederationService)

## Documentation

- [Implementation Guide](docs/IMPLEMENTATION_GUIDE.md) - Complete technical blueprint
- [API Reference](docs/API_REFERENCE.md) - Tool and API documentation
- [Migration Guide](docs/MIGRATION_GUIDE.md) - Migrating from COA CodeSearch
- [Federation Guide](docs/FEDERATION_GUIDE.md) - Setting up federation