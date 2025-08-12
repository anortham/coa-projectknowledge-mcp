# COA ProjectKnowledge MCP

**Production-ready knowledge management system for MCP (Model Context Protocol) with federation capabilities and AI-optimized tooling.**

## 🚀 Features

✅ **Complete Knowledge Management**
- 5 simplified knowledge types (reduced from 44+ in legacy system)  
- **Temporal search with intelligent ranking** - prioritizes recent and frequently accessed knowledge
- **Advanced filtering** by type, tags, status, priority, and date ranges
- Cross-platform user-level storage (`~/.coa/knowledge/`)
- Full-text search with SQLite FTS5 and temporal scoring
- Chronological IDs for optimal performance
- Relationship management between knowledge items

✅ **Federation System**
- Hub-and-spoke architecture for team knowledge sharing
- HTTP API for cross-project integration
- Real-time knowledge synchronization
- Multi-workspace support

✅ **AI-Optimized MCP Tools**
- 14+ MCP tools with descriptive names and guidance
- Checkpoint system for session state management
- Task management with checklists
- Export to Obsidian-compatible markdown

✅ **Production Quality**
- Global .NET tool packaging
- 100% ErrorHelpers integration across all 14 tools
- Comprehensive error handling with actionable recovery steps
- Framework v1.5.4 with enhanced features
- Centralized response builders for consistent formatting
- Serilog file-only logging
- Service lifetime optimizations

## 📦 Installation

### As Global .NET Tool (Recommended)
```bash
# Build release version
dotnet build -c Release

# Install globally 
dotnet tool install --global --add-source ./COA.ProjectKnowledge.McpServer/bin/Release projectknowledge

# Start federation hub
projectknowledge --mode http --port 5100
```

### For Development
```bash
# Clone and build
git clone <repository>
cd COA.ProjectKnowledge.McpServer
dotnet restore --configfile ../NuGet.config.local
dotnet build

# Run in STDIO mode (for Claude Code)
dotnet run -- stdio

# Run in HTTP mode (for federation)
dotnet run -- --mode http --port 5100
```

## 🔧 Configuration

### Claude Code Integration
The MCP server auto-configures with Claude Code. No manual setup required.

### Federation Setup
1. Start one instance as hub: `projectknowledge --mode http --port 5100`
2. Configure other MCP clients to connect to the hub
3. Knowledge automatically syncs across projects

## 🛠 Available MCP Tools

### Knowledge Management
- `store_knowledge` - Capture insights, decisions, findings
- `find_knowledge` - **Enhanced temporal search** with intelligent ranking, advanced filtering, and temporal scoring
- `search_across_projects` - Cross-project knowledge search
- `discover_projects` - Find available projects with knowledge

### Session Management
- `save_checkpoint` - Save current work state  
- `load_checkpoint` - Restore previous session
- `list_checkpoints` - View session history

### Task Management
- `create_checklist` - Create trackable task lists
- `view_checklist` - Check task completion status
- `update_task` - Mark tasks complete

### Activity & History
- `show_activity` - View chronological work timeline

### Relationships
- `link_knowledge` - Create knowledge connections
- `find_connections` - Discover related information

### Export & Sharing
- `export_knowledge` - Export to Obsidian markdown

## 📊 Knowledge Types

| Type | Purpose | Examples |
|------|---------|----------|
| **Checkpoint** | Session state | Work progress, milestones |
| **Checklist** | Task tracking | TODO lists, feature checklists |
| **TechnicalDebt** | Issues & blockers | Bugs, performance issues |
| **ProjectInsight** | Decisions & patterns | Architecture choices, lessons learned |  
| **WorkNote** | General notes | Meeting notes, research findings |

## 🏗 Architecture

### Hub-and-Spoke Federation
- **Central Hub**: One ProjectKnowledge instance (HTTP mode)
- **Federation Clients**: Other MCP servers connect to hub
- **Cross-Platform Database**: `~/.coa/knowledge/knowledge.db`
- **Real-Time Sync**: Automatic knowledge sharing

### Performance Optimizations
- **Chronological IDs**: Natural time-based sorting
- **Primary Key Queries**: `ORDER BY Id` instead of `ORDER BY CreatedAt`
- **FTS5 Search**: High-performance full-text search
- **Scoped Services**: Proper dependency injection lifetimes

### Technology Stack
- **.NET 9.0** with COA.Mcp.Framework 1.5.4
- **SQLite** with full-text search (FTS5) and temporal scoring
- **Entity Framework Core** for data access
- **Serilog** for structured logging
- **Cross-platform** user-level storage

## 🔍 Enhanced Search Features

### Temporal Knowledge Search
The `find_knowledge` tool provides intelligent search with temporal scoring:

**Temporal Scoring Modes:**
- `None` - Pure relevance matching
- `Default` - Moderate decay over 30 days *(recommended)*
- `Aggressive` - Strong preference for recent knowledge (7-day half-life)  
- `Gentle` - Slow decay over long periods (90-day half-life)

**Advanced Filtering:**
- Type, tag, status, priority filtering
- Date range queries
- Workspace-specific or cross-workspace search
- Boost recent or frequently accessed knowledge

**Usage Examples:**
```bash
# Find recent technical debt
find_knowledge(query="refactoring", types=["TechnicalDebt"], temporal_scoring="Aggressive")

# Search by tags and date range
find_knowledge(query="auth", tags=["security"], from_date="2025-01-01")

# Find frequently referenced knowledge
find_knowledge(boost_frequent=true, order_by="accesscount")
```

## 🤖 AI-Optimized Design

### Tool Names
- Action-oriented: `save_checkpoint`, `find_knowledge`, `discover_projects`
- Intuitive: `show_activity` vs `get_timeline`  
- Clear purpose: `search_across_projects` vs `search_cross_project`

### Tool Descriptions
- Explicit guidance on when to use each tool
- Context about purpose and benefits
- Clear parameter explanations

### Constants-Based Naming
- `ToolNames` constants class for consistency
- `ToolDescriptions` constants for maintainable guidance
- No magic strings - centralized renaming

## 📝 Slash Commands

- `/checkpoint` - Create structured work checkpoint
- `/resume` - Load and restore latest checkpoint

## 🔄 Migration

Automatic migration available from COA CodeSearch:
- Checkpoint → Checkpoint (1:1 mapping)
- Checklist/ChecklistItem → Checklist (combined)
- TechnicalDebt/Blocker/BugReport → TechnicalDebt
- ArchitecturalDecision/CodePattern → ProjectInsight
- All others → WorkNote

## 📚 Documentation

- [Federation Architecture](docs/FEDERATION_ARCHITECTURE_EXPLAINED.md) - Technical architecture details
- [API Reference](docs/API_REFERENCE.md) - Tool and HTTP API documentation  
- [Migration Guide](docs/MIGRATION_GUIDE.md) - Migrating from legacy systems
- [New User Setup](docs/NEW_USER_SETUP_GUIDE.md) - Getting started guide
- [.NET Tool Packaging](docs/DOTNET_TOOL_PACKAGING.md) - Distribution details

## 🧪 Testing

```bash
# Build and test
dotnet build -c Debug
dotnet test

# Test federation
projectknowledge --mode http --port 5100
curl http://localhost:5100/api/knowledge/health
```

**Note**: After code changes, exit Claude Code, rebuild in release mode, and restart Claude Code to reload the MCP server.

## 🤝 Contributing

1. Follow existing code patterns and naming conventions
2. Update `ToolNames` and `ToolDescriptions` constants for new tools
3. Add comprehensive error handling and logging
4. Update documentation and slash commands for any tool changes
5. Test both STDIO and HTTP modes

## 📄 License

Licensed for use within COA organization.

---

**Status**: ✅ Production Ready | **Federation**: ✅ Active | **Tools**: 14+ Available