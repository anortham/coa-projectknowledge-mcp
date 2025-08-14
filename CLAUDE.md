# COA ProjectKnowledge MCP - Developer Instructions

## Core Architecture
- **.NET 9.0** with COA.Mcp.Framework 1.5.6  
- **SQLite** database with EF Core
- **5 Knowledge Types**: Checkpoint, Checklist, TechnicalDebt, ProjectInsight, WorkNote
- **Dual-Mode**: STDIO (Claude Code) + HTTP (Federation)

## Critical Development Rules

### Testing Framework
- **Use NUnit, NOT XUnit** - all existing tests use NUnit.Framework
- Use FluentAssertions for assertions (`result.Should().Be(expected)`)
- Inherit from `ProjectKnowledgeTestBase` for integration tests
- Test files: `[Test]` and `[TestCase(...)]` attributes

### Build Process Constraints
**CRITICAL**: After making code changes:
1. **YOU (the user) must exit Claude Code** - MCP server runs in memory
2. Build in release mode: `dotnet build -c Release`  
3. **YOU (the user) must restart Claude Code** to reload MCP server
4. Only then will changes be active for testing

**While Claude Code is running**:
- **NEVER** build in Release mode - file locks will cause build failures
- **ONLY** build in Debug mode: `dotnet build -c Debug`  
- Release builds fail because the running MCP server locks those files

### Code Patterns
- Tools inherit from `McpToolBase` 
- Services use scoped lifetime for per-request operations
- Use `ErrorHelpers.CreateXxxError()` instead of throwing exceptions
- Database queries use `ChronologicalId` for natural sorting performance
- Workspace names: Use `WorkspaceResolver.ResolveWorkspaceNameAsync()` for fuzzy matching

### Service Layer Guidelines
- **KnowledgeService**: Core CRUD with workspace resolution
- **CheckpointService**: Session-based state management  
- **ChecklistService**: Task management with completion tracking
- **WorkspaceResolver**: Git root detection + name normalization
- All services inject `IWorkspaceResolver` for consistent workspace handling

### Database Schema
- **KnowledgeEntity**: Core entity with JSON metadata fields
- **ChronologicalId**: Time-based IDs (format: `YYYYMMDDHHMMSS-RANDOM`)
- **Access Tracking**: Automatic usage stats and temporal scoring
- **Workspace**: String field for project/workspace isolation

### Error Handling Pattern
```csharp
// CORRECT - Use ErrorHelpers
return new ToolResult
{
    Success = false,
    Error = ErrorHelpers.CreateSearchError($"Failed: {ex.Message}", "search")
};

// WRONG - Never throw exceptions in tools
throw new ToolExecutionException(Name, message, ex);
```

### Framework Integration
- All 14 tools use `ErrorHelpers` (100% compliance)
- Resource providers for large datasets (>30-50 items)
- Token optimization with smart truncation
- Response builders for consistent formatting

### Workspace Name Resolution
- Handles format mismatches: `"COA ProjectKnowledge MCP"` vs `"coa-projectknowledge-mcp"`
- `NormalizeWorkspaceName()`: converts to lowercase-with-hyphens
- `ResolveWorkspaceNameAsync()`: tries exact match first, then normalized matching
- Used in `SearchKnowledgeAsync()` and `GetTimelineAsync()`

### Debugging
- **STDIO Mode**: Minimal logging (prevents JSON-RPC corruption)
- **HTTP Mode**: Full logging via `--mode http --port 5100`  
- **Database**: Direct SQLite access at `~/.coa/knowledge/knowledge.db`
- **Logs**: Serilog file output to `~/.coa/knowledge/logs/`

### Adding New Features
1. Create service method in appropriate service class
2. Add MCP tool inheriting from `McpToolBase`
3. Use `ErrorHelpers` for error handling
4. Add unit tests using NUnit
5. Update this file with any new patterns or constraints

### Test Coverage Requirements  
- Unit tests for all service methods
- Integration tests for MCP tools
- Workspace resolution scenarios
- Error handling edge cases
- Framework compliance validation

## Current Framework Compliance: 96%
- ✅ ErrorHelpers: 14/14 tools (100%)
- ✅ Resource Providers: 4/14 tools (29% - appropriate for large datasets)
- ✅ Token Optimization: 100% integrated
- ✅ Dual-Mode Architecture: 100% working
- ⚠️ Unit Testing: 1/14 tools tested (needs expansion)

**Reference Implementation**: ProjectKnowledge demonstrates proper COA MCP Framework usage patterns.