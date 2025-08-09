# ProjectKnowledge Federation Architecture - Hub and Spoke Model

## ❌ NOT Peer-to-Peer

ProjectKnowledge is **NOT** a peer-to-peer system where multiple instances talk to each other. It's a **centralized hub** with many clients.

## ✅ Hub and Spoke Architecture

```
                    THE HUB (Single Instance)
                 ┌─────────────────────────────┐
                 │   ProjectKnowledge MCP      │
                 │                              │
                 │  Running in TWO modes:       │
                 │  1. STDIO (for Claude Code)  │
                 │  2. HTTP API (port 5100)     │
                 │                              │
                 │  Single SQLite Database:     │
                 │  C:\source\.coa\knowledge\   │
                 │         workspace.db          │
                 └──────────┬──────────────────┘
                           │
                    HTTP API :5100
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
        ▼                  ▼                  ▼
   CLIENT #1          CLIENT #2          CLIENT #3
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ SQL Analyzer │  │  Web Tools   │  │ CodeSearch   │
│  MCP Server  │  │  MCP Server  │  │  MCP Server  │
│              │  │              │  │              │
│ Stores SQL   │  │ Stores web   │  │ Stores code  │
│ insights via │  │ patterns via │  │ issues via   │
│ HTTP POST    │  │ HTTP POST    │  │ HTTP POST    │
└──────────────┘  └──────────────┘  └──────────────┘
```

## How It Works

### 1. ONE Hub Instance (ProjectKnowledge)

**There is only ONE ProjectKnowledge instance running per developer machine:**

```bash
# This ONE instance provides BOTH:
# - STDIO interface for Claude Code
# - HTTP API on port 5100 for federation
dotnet run
```

This single instance:
- Manages THE knowledge database (one database for all projects)
- Runs HTTP API on port 5100 automatically
- Handles ALL knowledge storage and retrieval
- Is THE central knowledge hub

### 2. MANY Client MCP Servers

**Other MCP servers are CLIENTS that send knowledge to the hub:**

```csharp
// In SQL Analyzer MCP Server (CLIENT)
public class SqlAnalyzerTool : McpToolBase<...>
{
    private readonly HttpClient _httpClient;
    
    public SqlAnalyzerTool()
    {
        // This connects to THE ProjectKnowledge hub
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5100")
        };
    }
    
    protected override async Task<Result> ExecuteInternalAsync(...)
    {
        // Do SQL analysis...
        var issue = "Found missing index on Users table";
        
        // Send knowledge to THE hub
        await _httpClient.PostAsJsonAsync("/api/knowledge/store", new
        {
            type = "TechnicalDebt",
            content = issue,
            source = "sql-analyzer"
        });
    }
}
```

### 3. NOT Multiple ProjectKnowledge Instances

**Common Misunderstanding:**
```
❌ WRONG: ProjectKnowledge ←→ ProjectKnowledge ←→ ProjectKnowledge
          (peer-to-peer network of ProjectKnowledge instances)

✅ RIGHT: SQL MCP → ProjectKnowledge ← Web MCP
          CodeSearch MCP ↗        ↖ Custom Scripts
          (Many clients, ONE hub)
```

## Implementation Pattern

### The Hub (ProjectKnowledge) - Program.cs

```csharp
public static async Task Main(string[] args)
{
    var builder = McpServer.CreateBuilder()
        .WithServerInfo("ProjectKnowledge", "1.0.0");
    
    // STDIO mode for Claude Code
    builder.UseStdioTransport();
    
    // Auto-start HTTP service for receiving federation data
    builder.UseAutoService(config =>
    {
        config.ServiceId = "projectknowledge-http";
        config.ExecutablePath = Assembly.GetExecutingAssembly().Location;
        config.Arguments = new[] { "--mode", "http" };
        config.Port = 5100;  // THE federation port
    });
    
    // This is THE hub - it receives and stores ALL knowledge
    builder.RegisterToolType<StoreKnowledgeTool>();
    builder.RegisterToolType<SearchKnowledgeTool>();
    
    await builder.RunAsync();
}
```

### A Client (Any Other MCP Server)

```csharp
// This is a CLIENT that sends knowledge to the hub
public class WebAnalyzerMcpServer
{
    public static async Task Main(string[] args)
    {
        var builder = McpServer.CreateBuilder()
            .WithServerInfo("WebAnalyzer", "1.0.0");
        
        builder.UseStdioTransport();
        
        // Register this server's own tools
        builder.RegisterToolType<AnalyzeWebPageTool>();
        
        // NO HTTP API needed - this is a client!
        // It SENDS to ProjectKnowledge, doesn't receive
        
        await builder.RunAsync();
    }
}

public class AnalyzeWebPageTool : McpToolBase<...>
{
    protected override async Task<Result> ExecuteInternalAsync(...)
    {
        // Analyze web page...
        var issue = "Found broken links";
        
        // Send to THE ProjectKnowledge hub
        using var client = new HttpClient();
        await client.PostAsJsonAsync(
            "http://localhost:5100/api/knowledge/store",
            new { type = "TechnicalDebt", content = issue }
        );
    }
}
```

## Federation API Endpoints

### The Hub Provides (ProjectKnowledge HTTP API)

```
http://localhost:5100/
├── GET  /api/knowledge/health          # Check if hub is running
├── POST /api/knowledge/store           # Store knowledge
├── GET  /api/knowledge/search          # Search knowledge
├── POST /api/knowledge/batch           # Batch store
└── POST /api/knowledge/contribute      # External contribution
```

### Clients Call These Endpoints

```csharp
// Client example: Store knowledge
POST http://localhost:5100/api/knowledge/store
{
    "type": "TechnicalDebt",
    "content": "Database needs optimization",
    "source": "sql-analyzer",
    "metadata": {
        "database": "CustomerDB",
        "severity": "high"
    }
}

// Client example: Search knowledge
GET http://localhost:5100/api/knowledge/search?query=database
```

## Key Points to Understand

### 1. Single Source of Truth
- ONE ProjectKnowledge instance
- ONE SQLite database
- ONE HTTP API endpoint (port 5100)
- ALL knowledge goes here

### 2. Many Contributors
- SQL MCP servers are clients
- Web MCP servers are clients
- CodeSearch MCP is a client
- Custom scripts are clients
- They all SEND to the hub

### 3. Not Distributed
- No ProjectKnowledge talking to ProjectKnowledge
- No synchronization between instances
- No peer discovery
- Just hub and spokes

### 4. Auto-Service is for Dual Mode
The auto-service feature makes ONE instance run in TWO modes:
```
ProjectKnowledge Instance
├── STDIO mode (for Claude Code tools)
└── HTTP mode (for federation API)
    └── Both from the SAME process/database
```

## Common Mistakes to Avoid

### ❌ Mistake 1: Multiple ProjectKnowledge Instances
```csharp
// WRONG: Don't create multiple ProjectKnowledge instances
var pk1 = new ProjectKnowledge("workspace1");
var pk2 = new ProjectKnowledge("workspace2");
pk1.FederateWith(pk2);  // NO! This is not how it works
```

### ❌ Mistake 2: ProjectKnowledge as a Library
```csharp
// WRONG: ProjectKnowledge is not a library to embed
class SqlAnalyzer : ProjectKnowledge  // NO!
{
    // Don't inherit or embed ProjectKnowledge
}
```

### ❌ Mistake 3: Complex Federation Protocol
```csharp
// WRONG: No complex federation protocol needed
federationManager.RegisterPeer(remoteEndpoint);
federationManager.SyncKnowledge();  // NO! Just HTTP POST
```

### ✅ Correct: Simple HTTP Client
```csharp
// RIGHT: Just make HTTP calls to the hub
var client = new HttpClient();
await client.PostAsJsonAsync(
    "http://localhost:5100/api/knowledge/store",
    new { type = "WorkNote", content = "My insight" }
);
```

## Testing Federation

### Step 1: Start the Hub
```bash
# Terminal 1: Start ProjectKnowledge (THE hub)
cd "C:\source\COA ProjectKnowledge MCP"
dotnet run
# This starts BOTH STDIO and HTTP (via auto-service)
```

### Step 2: Test the HTTP API
```bash
# Terminal 2: Test that hub is receiving
curl http://localhost:5100/api/knowledge/health
# Should return: {"status":"healthy"}

curl -X POST http://localhost:5100/api/knowledge/store \
  -H "Content-Type: application/json" \
  -d '{"type":"WorkNote","content":"Test from curl","source":"test"}'
# Should return: {"success":true,"knowledgeId":"..."}
```

### Step 3: Clients Send Knowledge
```csharp
// In ANY other MCP server or script
using var client = new HttpClient();
var response = await client.PostAsJsonAsync(
    "http://localhost:5100/api/knowledge/store",
    new
    {
        type = "TechnicalDebt",
        content = "Found SQL injection vulnerability",
        source = "security-scanner",
        metadata = new Dictionary<string, string>
        {
            ["severity"] = "critical",
            ["file"] = "UserController.cs"
        }
    }
);
```

## Summary

**ProjectKnowledge is a HUB, not a peer:**
- ONE instance per developer machine
- Runs BOTH STDIO (for Claude) AND HTTP (for federation)
- ALL other MCP servers are CLIENTS that send knowledge to it
- No ProjectKnowledge-to-ProjectKnowledge communication
- Simple HTTP POST to store, GET to search

Think of it like a database server:
- ProjectKnowledge = MySQL Server (the hub)
- Other MCP servers = Applications using MySQL (the clients)
- Federation API = SQL protocol (how clients talk to hub)

There's ONE database server, MANY applications using it.