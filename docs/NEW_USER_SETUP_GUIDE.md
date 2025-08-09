# ProjectKnowledge - New User Setup Guide

## Overview
ProjectKnowledge is a **system-level MCP server** that provides centralized knowledge management for ALL your development projects. Think of it like installing Docker or Git - you install it once and use it everywhere.

## 🚀 Initial Setup (One-Time)

### Step 1: Choose Installation Location
```bash
# Create a dedicated folder for MCP servers (NOT inside any project)
mkdir C:\tools\mcp-servers
cd C:\tools\mcp-servers
```

### Step 2: Clone/Install ProjectKnowledge
```bash
# Option A: Clone from your repo
git clone https://github.com/yourorg/COA-ProjectKnowledge-MCP.git ProjectKnowledge

# Option B: Copy from source
xcopy "C:\source\COA ProjectKnowledge MCP" "C:\tools\mcp-servers\ProjectKnowledge" /E /I

cd ProjectKnowledge
```

### Step 3: Build ProjectKnowledge
```bash
# Build the server
dotnet build -c Release

# Or publish as self-contained
dotnet publish -c Release -o publish
```

### Step 4: Configure Claude Code MCP Settings

Add to your Claude Code MCP configuration file:

**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`
**Mac:** `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "projectknowledge": {
      "command": "dotnet",
      "args": ["C:\\tools\\mcp-servers\\ProjectKnowledge\\publish\\COA.ProjectKnowledge.McpServer.dll", "stdio"],
      "env": {
        "PROJECTKNOWLEDGE_DB_PATH": "C:\\Users\\%USERNAME%\\.coa\\knowledge\\workspace.db"
      }
    },
    
    // Your other project-specific MCP servers
    "codesearch": {
      "command": "dotnet",
      "args": ["C:\\source\\COA CodeSearch MCP\\publish\\COA.CodeSearch.McpServer.dll", "stdio"]
    },
    
    "sql-analyzer": {
      "command": "dotnet",
      "args": ["C:\\source\\SQL-Analyzer-MCP\\publish\\SQL.Analyzer.McpServer.dll", "stdio"]
    }
  }
}
```

### Step 5: Database Location

ProjectKnowledge will automatically create its database at:
```
C:\Users\%USERNAME%\.coa\knowledge\workspace.db
```

This location is:
- ✅ Outside any project directory
- ✅ In your user profile (survives project deletions)
- ✅ Accessible from any project
- ✅ Backed up with your user data

## 📦 What Gets Installed

```
C:\tools\mcp-servers\ProjectKnowledge\     # The MCP server (system-level)
C:\Users\%USERNAME%\.coa\knowledge\        # Your knowledge database
    └── workspace.db                        # All knowledge from all projects
    └── backups\                           # Automatic backups
    └── exports\                           # Markdown exports
```

## 🎯 Daily Usage

### Starting Your Work Day

When Claude Code starts, ProjectKnowledge automatically starts and:
1. Loads in STDIO mode for Claude Code tools
2. Starts HTTP API on port 5100 for federation
3. Connects to your knowledge database
4. Ready to serve ALL your projects

### Working on Different Projects

```bash
# Monday: Working on CustomerPortal
cd C:\source\CustomerPortal
# Claude Code uses ProjectKnowledge - knowledge tagged with "CustomerPortal"

# Tuesday: Working on AdminDashboard  
cd C:\source\AdminDashboard
# Same ProjectKnowledge instance - knowledge tagged with "AdminDashboard"

# Wednesday: Working on new SQLAnalyzer
cd C:\source\SQLAnalyzer
# Still same ProjectKnowledge - all knowledge accumulates
```

### Knowledge Accumulation

After a week of work:
```
ProjectKnowledge Database contains:
├── CustomerPortal/
│   ├── 15 Technical Debt items
│   ├── 8 Architectural Decisions
│   └── 23 Checkpoints
├── AdminDashboard/
│   ├── 7 Technical Debt items
│   ├── 3 Project Insights
│   └── 12 Checkpoints
└── SQLAnalyzer/
    ├── 22 Database insights
    └── 5 Performance issues
```

## 🔧 Federation Setup (Optional)

### For SQL Team Members

Your SQL MCP servers can contribute knowledge:

```csharp
// In any SQL analyzer tool
public async Task AnalyzeDatabase(string connectionString)
{
    var issues = FindIssues(connectionString);
    
    // Send to ProjectKnowledge hub
    using var client = new HttpClient();
    foreach (var issue in issues)
    {
        await client.PostAsJsonAsync(
            "http://localhost:5100/api/knowledge/store",
            new
            {
                type = "TechnicalDebt",
                content = issue.Description,
                source = "sql-analyzer",
                metadata = new Dictionary<string, string>
                {
                    ["database"] = issue.Database,
                    ["severity"] = issue.Severity
                }
            }
        );
    }
}
```

### For Custom Scripts

Any script can contribute:

```powershell
# PowerShell script example
$knowledge = @{
    type = "ProjectInsight"
    content = "Build takes 45 seconds on average"
    source = "build-script"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5100/api/knowledge/store" `
                  -Method Post `
                  -Body $knowledge `
                  -ContentType "application/json"
```

## 🔍 Verifying Installation

### Check 1: Claude Code Recognition
In any project, in Claude Code:
```
Can you store this as technical debt: "Need to refactor UserService"
```

Should respond with:
```
Stored as TechnicalDebt with ID: [timestamp-based-id]
```

### Check 2: HTTP API Health
```bash
curl http://localhost:5100/api/knowledge/health
```

Should return:
```json
{"status":"healthy","service":"ProjectKnowledge","timestamp":"..."}
```

### Check 3: Database Location
```bash
dir C:\Users\%USERNAME%\.coa\knowledge\
```

Should show:
```
workspace.db    (your knowledge database)
```

## 📊 Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│                   Developer Machine                      │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  System Level:                                          │
│  ┌──────────────────────────────────────────┐          │
│  │   C:\tools\mcp-servers\ProjectKnowledge   │          │
│  │   (ONE instance for ALL projects)         │          │
│  └────────────────┬─────────────────────────┘          │
│                   │                                     │
│                   ├── STDIO (Claude Code)              │
│                   └── HTTP :5100 (Federation)          │
│                                                         │
│  User Data:                                            │
│  ┌──────────────────────────────────────────┐          │
│  │   C:\Users\%USERNAME%\.coa\knowledge\     │          │
│  │   workspace.db (ONE database)             │          │
│  └──────────────────────────────────────────┘          │
│                                                         │
│  Projects (multiple):                                  │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐     │
│  │  Project A  │ │  Project B  │ │  Project C  │     │
│  │             │ │             │ │             │     │
│  │ Uses same   │ │ Uses same   │ │ Uses same   │     │
│  │ ProjectKnow │ │ ProjectKnow │ │ ProjectKnow │     │
│  └─────────────┘ └─────────────┘ └─────────────┘     │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

## ❓ Common Questions

### Q: Do I need to install ProjectKnowledge in each project?
**A: No!** Install once at system level, use everywhere.

### Q: What if I delete a project folder?
**A: Your knowledge is safe!** It's stored in `~/.coa/knowledge/`, not in project folders.

### Q: Can I have different knowledge for different clients/companies?
**A: Yes!** Configure different databases:
```json
{
  "env": {
    "PROJECTKNOWLEDGE_DB_PATH": "C:\\knowledge\\client1\\workspace.db"
  }
}
```

### Q: What about team sharing?
**A: Options:**
1. Each developer has their own local knowledge
2. Share knowledge exports via Git
3. Future: Team knowledge server

### Q: How do I backup?
**A: Multiple ways:**
1. Automatic backups in `.coa/knowledge/backups/`
2. Export to Markdown: `mcp__projectknowledge__export_knowledge`
3. Copy `workspace.db` file
4. Commit exports to Git

## 🚨 Important Notes

1. **NOT Per-Project** - Don't copy ProjectKnowledge into each project
2. **System Service** - Think of it like Docker or Git
3. **One Database** - All knowledge in one place
4. **Always Running** - Starts with Claude Code, runs all day
5. **Federation Ready** - Other tools can send knowledge to it

## 🎉 Setup Complete!

You now have:
- ✅ Centralized knowledge management
- ✅ Works across all projects
- ✅ Accumulates insights over time
- ✅ Federation endpoint for other tools
- ✅ Automatic workspace detection

Start working on any project and ProjectKnowledge will automatically:
- Store checkpoints and insights
- Track technical debt
- Build your knowledge base
- Learn from all your projects

## Need Help?

- Check logs: `C:\Users\%USERNAME%\.coa\knowledge\logs\`
- Test health: `curl http://localhost:5100/api/knowledge/health`
- View database: Use any SQLite viewer on `workspace.db`
- Export knowledge: `mcp__projectknowledge__export_knowledge --format markdown`