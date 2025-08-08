# COA ProjectKnowledge MCP Server - Implementation Guide

## Executive Summary

This document provides complete implementation guidance for building the COA ProjectKnowledge MCP server, a simplified knowledge management system that replaces the complex memory system in COA CodeSearch MCP. The new system reduces 44+ memory types to 5 core types, uses SQLite for storage, and provides federation capabilities for team collaboration.

## Project Goals

1. **Simplify** - Reduce from 44+ memory types to 5 essential types
2. **Centralize** - Workspace-level knowledge storage (not per-project)
3. **Federate** - Enable knowledge sharing across MCP servers
4. **Preserve** - Keep the successful checkpoint/checklist features
5. **Modernize** - Use SQLite with JSON for flexible schema

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     Claude Code (STDIO)                      │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│            COA.ProjectKnowledge.McpServer                    │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │ MCP Tools    │  │   Services   │  │ HTTP API     │     │
│  │              │  │              │  │ (Auto-Start) │     │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘     │
│         │                  │                  │              │
│         └──────────────────┼──────────────────┘              │
│                           │                                  │
│                    ┌──────▼───────┐                         │
│                    │ SQLite DB    │                         │
│                    │ with JSON    │                         │
│                    └───────────────┘                         │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    Federation Endpoints
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
   SQL MCP Server      Other Projects         Future Tools
```

## Technology Stack

- **.NET 8.0** - Target framework
- **COA.Mcp.Framework 1.4.2** - MCP framework with auto-service support
- **SQLite** - Primary storage with JSON documents
- **System.Text.Json** - JSON serialization
- **Microsoft.Data.Sqlite** - SQLite provider
- **ASP.NET Core** - HTTP API for federation

## Project Structure

```
C:\source\COA ProjectKnowledge MCP\
├── COA.ProjectKnowledge.McpServer\
│   ├── Program.cs                           # Entry point with dual-mode
│   ├── COA.ProjectKnowledge.McpServer.csproj
│   ├── appsettings.json
│   ├── Models\
│   │   ├── Knowledge.cs
│   │   ├── Checkpoint.cs
│   │   ├── Checklist.cs
│   │   └── ChronologicalId.cs
│   ├── Storage\
│   │   ├── KnowledgeDatabase.cs
│   │   ├── DatabaseInitializer.cs
│   │   ├── Migrations\
│   │   │   └── 001_InitialSchema.sql
│   │   └── QueryHelpers.cs
│   ├── Services\
│   │   ├── KnowledgeService.cs
│   │   ├── CheckpointService.cs
│   │   ├── ChecklistService.cs
│   │   ├── RelationshipService.cs
│   │   ├── WorkspaceResolver.cs
│   │   └── FederationService.cs
│   ├── Tools\
│   │   ├── StoreKnowledgeTool.cs
│   │   ├── SearchKnowledgeTool.cs
│   │   ├── CheckpointTool.cs
│   │   ├── ChecklistTool.cs
│   │   └── RelationshipTool.cs
│   ├── Api\
│   │   ├── KnowledgeApiController.cs
│   │   └── Models\
│   │       ├── ApiRequests.cs
│   │       └── ApiResponses.cs
│   └── Utils\
│       ├── JsonExtensions.cs
│       └── PathHelper.cs
├── COA.ProjectKnowledge.Tests\
│   ├── COA.ProjectKnowledge.Tests.csproj
│   ├── ServiceTests\
│   ├── ToolTests\
│   └── IntegrationTests\
├── docs\
│   ├── IMPLEMENTATION_GUIDE.md (this file)
│   ├── API_REFERENCE.md
│   ├── MIGRATION_GUIDE.md
│   └── FEDERATION_GUIDE.md
└── README.md
```

## Core Implementation Details

### 1. Project File (COA.ProjectKnowledge.McpServer.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="COA.Mcp.Framework" Version="1.4.2" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.App" Version="8.0.0" />
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

### 2. Program.cs - Dual-Mode Entry Point

```csharp
using COA.Mcp.Framework.Server;
using COA.Mcp.Framework.Server.Services;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Storage;
using COA.ProjectKnowledge.McpServer.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace COA.ProjectKnowledge.McpServer;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var builder = McpServer.CreateBuilder()
            .WithServerInfo("ProjectKnowledge", "1.0.0");

        // Configure services
        builder.Services.AddSingleton<IConfiguration>(configuration);
        builder.Services.AddSingleton<KnowledgeDatabase>();
        builder.Services.AddSingleton<KnowledgeService>();
        builder.Services.AddSingleton<CheckpointService>();
        builder.Services.AddSingleton<ChecklistService>();
        builder.Services.AddSingleton<RelationshipService>();
        builder.Services.AddSingleton<WorkspaceResolver>();
        builder.Services.AddSingleton<FederationService>();

        // Determine mode from args
        bool isHttpMode = args.Contains("--mode") && args.Contains("http");
        
        if (isHttpMode)
        {
            // HTTP mode - run as service for federation
            builder.UseHttpTransport(options =>
            {
                options.Port = configuration.GetValue<int>("Federation:Port", 5100);
                options.Host = "localhost";
                options.EnableWebSocket = true;
                options.EnableCors = true;
                options.AllowedOrigins = configuration.GetSection("Federation:AllowedOrigins").Get<string[]>() 
                    ?? new[] { "*" };
            });
            
            Console.WriteLine($"Starting ProjectKnowledge HTTP service on port {options.Port}...");
        }
        else
        {
            // STDIO mode - run as MCP client with auto-started HTTP service
            builder.UseStdioTransport();
            
            // Auto-start HTTP service for federation
            if (configuration.GetValue<bool>("Federation:Enabled", true))
            {
                builder.UseAutoService(config =>
                {
                    config.ServiceId = "projectknowledge-http";
                    config.ExecutablePath = Assembly.GetExecutingAssembly().Location;
                    config.Arguments = new[] { "--mode", "http" };
                    config.Port = configuration.GetValue<int>("Federation:Port", 5100);
                    config.HealthEndpoint = $"http://localhost:{config.Port}/api/knowledge/health";
                    config.AutoRestart = true;
                    config.MaxRestartAttempts = 3;
                    config.HealthCheckIntervalSeconds = 60;
                });
            }
        }

        // Register tools
        builder.RegisterToolType<StoreKnowledgeTool>();
        builder.RegisterToolType<SearchKnowledgeTool>();
        builder.RegisterToolType<CheckpointTool>();
        builder.RegisterToolType<ChecklistTool>();
        builder.RegisterToolType<RelationshipTool>();

        // Configure logging
        builder.ConfigureLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        // Initialize database
        var serviceProvider = builder.Services.BuildServiceProvider();
        var database = serviceProvider.GetRequiredService<KnowledgeDatabase>();
        await database.InitializeAsync();

        // Run the server
        await builder.RunAsync();
    }
}
```

### 3. Database Schema (Storage/Migrations/001_InitialSchema.sql)

```sql
-- Knowledge table with JSON documents for flexibility
CREATE TABLE IF NOT EXISTS knowledge (
    id TEXT PRIMARY KEY,
    type TEXT NOT NULL,
    content TEXT NOT NULL,
    code_snippets TEXT, -- JSON array of code snippets
    metadata TEXT,      -- JSON object for flexible fields
    workspace TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    modified_at INTEGER NOT NULL,
    accessed_at INTEGER,
    access_count INTEGER DEFAULT 0,
    is_archived BOOLEAN DEFAULT 0
);

-- Relationships between knowledge entries
CREATE TABLE IF NOT EXISTS relationships (
    from_id TEXT NOT NULL,
    to_id TEXT NOT NULL,
    relationship_type TEXT NOT NULL,
    metadata TEXT, -- JSON object for relationship metadata
    created_at INTEGER NOT NULL,
    PRIMARY KEY (from_id, to_id, relationship_type),
    FOREIGN KEY (from_id) REFERENCES knowledge(id) ON DELETE CASCADE,
    FOREIGN KEY (to_id) REFERENCES knowledge(id) ON DELETE CASCADE
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS idx_knowledge_type ON knowledge(type);
CREATE INDEX IF NOT EXISTS idx_knowledge_workspace ON knowledge(workspace);
CREATE INDEX IF NOT EXISTS idx_knowledge_created ON knowledge(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_knowledge_modified ON knowledge(modified_at DESC);
CREATE INDEX IF NOT EXISTS idx_knowledge_accessed ON knowledge(accessed_at DESC);
CREATE INDEX IF NOT EXISTS idx_relationships_from ON relationships(from_id);
CREATE INDEX IF NOT EXISTS idx_relationships_to ON relationships(to_id);

-- Full-text search virtual table
CREATE VIRTUAL TABLE IF NOT EXISTS knowledge_fts USING fts5(
    id UNINDEXED,
    content,
    type,
    metadata,
    tokenize='porter unicode61'
);

-- Trigger to keep FTS in sync
CREATE TRIGGER IF NOT EXISTS knowledge_fts_insert AFTER INSERT ON knowledge BEGIN
    INSERT INTO knowledge_fts(id, content, type, metadata)
    VALUES (new.id, new.content, new.type, new.metadata);
END;

CREATE TRIGGER IF NOT EXISTS knowledge_fts_update AFTER UPDATE ON knowledge BEGIN
    UPDATE knowledge_fts 
    SET content = new.content, type = new.type, metadata = new.metadata
    WHERE id = new.id;
END;

CREATE TRIGGER IF NOT EXISTS knowledge_fts_delete AFTER DELETE ON knowledge BEGIN
    DELETE FROM knowledge_fts WHERE id = old.id;
END;
```

### 4. Core Models

#### Models/Knowledge.cs

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace COA.ProjectKnowledge.McpServer.Models;

/// <summary>
/// Simplified knowledge types - reduced from 44+ to 5 core types
/// </summary>
public static class KnowledgeTypes
{
    public const string Checkpoint = "Checkpoint";
    public const string Checklist = "Checklist";
    public const string TechnicalDebt = "TechnicalDebt";
    public const string ProjectInsight = "ProjectInsight";
    public const string WorkNote = "WorkNote";
    
    public static readonly HashSet<string> ValidTypes = new()
    {
        Checkpoint, Checklist, TechnicalDebt, ProjectInsight, WorkNote
    };
}

/// <summary>
/// Core knowledge entry model
/// </summary>
public class Knowledge
{
    public string Id { get; set; } = ChronologicalId.Generate();
    public string Type { get; set; } = KnowledgeTypes.WorkNote;
    public string Content { get; set; } = string.Empty;
    public List<CodeSnippet> CodeSnippets { get; set; } = new();
    public Dictionary<string, JsonElement> Metadata { get; set; } = new();
    public string Workspace { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AccessedAt { get; set; }
    public int AccessCount { get; set; } = 0;
    public bool IsArchived { get; set; } = false;
    
    // Helper methods for metadata
    public T? GetMetadata<T>(string key)
    {
        if (Metadata.TryGetValue(key, out var element))
        {
            try
            {
                return JsonSerializer.Deserialize<T>(element.GetRawText());
            }
            catch
            {
                return default;
            }
        }
        return default;
    }
    
    public void SetMetadata<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        Metadata[key] = JsonDocument.Parse(json).RootElement;
    }
    
    // Common metadata properties
    public string? Status => GetMetadata<string>("status");
    public string? Priority => GetMetadata<string>("priority");
    public string[]? Tags => GetMetadata<string[]>("tags");
    public string[]? RelatedTo => GetMetadata<string[]>("relatedTo");
}

/// <summary>
/// Code snippet with syntax information
/// </summary>
public class CodeSnippet
{
    public string Language { get; set; } = "plaintext";
    public string Code { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public int? StartLine { get; set; }
    public int? EndLine { get; set; }
    
    /// <summary>
    /// Get markdown representation for export
    /// </summary>
    public string ToMarkdown()
    {
        var header = FilePath != null ? $"// {FilePath}" : "";
        if (StartLine.HasValue && EndLine.HasValue)
        {
            header += $" (lines {StartLine}-{EndLine})";
        }
        
        return $"```{Language}\n{header}\n{Code}\n```";
    }
}
```

#### Models/ChronologicalId.cs

```csharp
namespace COA.ProjectKnowledge.McpServer.Models;

/// <summary>
/// Generates chronological IDs for natural time-based sorting
/// Similar to MongoDB ObjectIds but simpler
/// </summary>
public static class ChronologicalId
{
    private static int _counter = Random.Shared.Next(0, 0xFFFFFF);
    private static readonly object _lock = new();
    
    /// <summary>
    /// Generate a chronological ID with optional prefix
    /// Format: [prefix-]timestamp-counter
    /// Example: "CHECKPOINT-18C3A2B4F12-A3F2"
    /// </summary>
    public static string Generate(string? prefix = null)
    {
        lock (_lock)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var counter = Interlocked.Increment(ref _counter) & 0xFFFFFF; // 24-bit counter
            
            var id = $"{timestamp:X}-{counter:X6}";
            
            return string.IsNullOrEmpty(prefix) ? id : $"{prefix}-{id}";
        }
    }
    
    /// <summary>
    /// Extract timestamp from a chronological ID
    /// </summary>
    public static DateTime? GetTimestamp(string id)
    {
        try
        {
            // Remove prefix if present
            var parts = id.Split('-');
            var timestampHex = parts.Length > 2 ? parts[1] : parts[0];
            
            if (long.TryParse(timestampHex, System.Globalization.NumberStyles.HexNumber, null, out var timestamp))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
            }
        }
        catch
        {
            // Invalid format
        }
        
        return null;
    }
}
```

#### Models/Checkpoint.cs

```csharp
namespace COA.ProjectKnowledge.McpServer.Models;

/// <summary>
/// Session checkpoint for state persistence
/// </summary>
public class Checkpoint : Knowledge
{
    public Checkpoint()
    {
        Type = KnowledgeTypes.Checkpoint;
        Id = ChronologicalId.Generate("CHECKPOINT");
    }
    
    /// <summary>
    /// Session ID this checkpoint belongs to
    /// </summary>
    public string SessionId 
    { 
        get => GetMetadata<string>("sessionId") ?? string.Empty;
        set => SetMetadata("sessionId", value);
    }
    
    /// <summary>
    /// Sequential checkpoint number within session
    /// </summary>
    public int SequenceNumber
    {
        get => GetMetadata<int>("sequenceNumber");
        set => SetMetadata("sequenceNumber", value);
    }
    
    /// <summary>
    /// Files that were open/modified at checkpoint time
    /// </summary>
    public string[] ActiveFiles
    {
        get => GetMetadata<string[]>("activeFiles") ?? Array.Empty<string>();
        set => SetMetadata("activeFiles", value);
    }
}
```

#### Models/Checklist.cs

```csharp
namespace COA.ProjectKnowledge.McpServer.Models;

/// <summary>
/// Checklist with trackable items
/// </summary>
public class Checklist : Knowledge
{
    public Checklist()
    {
        Type = KnowledgeTypes.Checklist;
        Id = ChronologicalId.Generate("CHECKLIST");
    }
    
    /// <summary>
    /// Checklist items
    /// </summary>
    public List<ChecklistItem> Items
    {
        get => GetMetadata<List<ChecklistItem>>("items") ?? new();
        set => SetMetadata("items", value);
    }
    
    /// <summary>
    /// Parent checklist ID for nested checklists
    /// </summary>
    public string? ParentChecklistId
    {
        get => GetMetadata<string>("parentChecklistId");
        set => SetMetadata("parentChecklistId", value);
    }
    
    /// <summary>
    /// Calculate completion percentage
    /// </summary>
    public double CompletionPercentage
    {
        get
        {
            var items = Items;
            if (items.Count == 0) return 0;
            
            var completed = items.Count(i => i.IsCompleted);
            return (double)completed / items.Count * 100;
        }
    }
    
    /// <summary>
    /// Get summary status
    /// </summary>
    public string GetStatus()
    {
        var total = Items.Count;
        var completed = Items.Count(i => i.IsCompleted);
        
        if (completed == 0) return "Not Started";
        if (completed == total) return "Completed";
        return $"In Progress ({completed}/{total})";
    }
}

/// <summary>
/// Individual checklist item
/// </summary>
public class ChecklistItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }
    public int Order { get; set; } = 0;
    public Dictionary<string, JsonElement> Metadata { get; set; } = new();
}
```

### 5. Core Services

#### Services/KnowledgeService.cs

```csharp
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Storage;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Services;

public class KnowledgeService
{
    private readonly KnowledgeDatabase _database;
    private readonly WorkspaceResolver _workspaceResolver;
    private readonly ILogger<KnowledgeService> _logger;
    
    public KnowledgeService(
        KnowledgeDatabase database,
        WorkspaceResolver workspaceResolver,
        ILogger<KnowledgeService> logger)
    {
        _database = database;
        _workspaceResolver = workspaceResolver;
        _logger = logger;
    }
    
    public async Task<Knowledge> StoreAsync(Knowledge knowledge)
    {
        // Validate type
        if (!KnowledgeTypes.ValidTypes.Contains(knowledge.Type))
        {
            throw new ArgumentException($"Invalid knowledge type: {knowledge.Type}");
        }
        
        // Set workspace if not provided
        if (string.IsNullOrEmpty(knowledge.Workspace))
        {
            knowledge.Workspace = _workspaceResolver.GetCurrentWorkspace();
        }
        
        // Store in database
        await _database.InsertKnowledgeAsync(knowledge);
        
        _logger.LogInformation("Stored {Type} knowledge: {Id}", knowledge.Type, knowledge.Id);
        
        return knowledge;
    }
    
    public async Task<List<Knowledge>> SearchAsync(string query, string? workspace = null, int maxResults = 50)
    {
        workspace ??= _workspaceResolver.GetCurrentWorkspace();
        
        var results = await _database.SearchKnowledgeAsync(query, workspace, maxResults);
        
        // Update access tracking
        foreach (var result in results)
        {
            result.AccessedAt = DateTime.UtcNow;
            result.AccessCount++;
            await _database.UpdateAccessTrackingAsync(result.Id);
        }
        
        return results;
    }
    
    public async Task<Knowledge?> GetByIdAsync(string id)
    {
        var knowledge = await _database.GetKnowledgeByIdAsync(id);
        
        if (knowledge != null)
        {
            knowledge.AccessedAt = DateTime.UtcNow;
            knowledge.AccessCount++;
            await _database.UpdateAccessTrackingAsync(id);
        }
        
        return knowledge;
    }
    
    public async Task<Knowledge> UpdateAsync(string id, Action<Knowledge> updateAction)
    {
        var knowledge = await GetByIdAsync(id);
        if (knowledge == null)
        {
            throw new InvalidOperationException($"Knowledge not found: {id}");
        }
        
        updateAction(knowledge);
        knowledge.ModifiedAt = DateTime.UtcNow;
        
        await _database.UpdateKnowledgeAsync(knowledge);
        
        return knowledge;
    }
    
    public async Task<bool> ArchiveAsync(string id)
    {
        return await _database.ArchiveKnowledgeAsync(id);
    }
    
    public async Task<KnowledgeStats> GetStatsAsync(string? workspace = null)
    {
        workspace ??= _workspaceResolver.GetCurrentWorkspace();
        return await _database.GetStatsAsync(workspace);
    }
}

public class KnowledgeStats
{
    public int TotalCount { get; set; }
    public Dictionary<string, int> CountByType { get; set; } = new();
    public int ArchivedCount { get; set; }
    public DateTime? OldestEntry { get; set; }
    public DateTime? NewestEntry { get; set; }
    public List<string> MostAccessedIds { get; set; } = new();
}
```

#### Services/CheckpointService.cs

```csharp
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Storage;
using Microsoft.Extensions.Logging;

namespace COA.ProjectKnowledge.McpServer.Services;

public class CheckpointService
{
    private readonly KnowledgeService _knowledgeService;
    private readonly ILogger<CheckpointService> _logger;
    private readonly Dictionary<string, int> _sessionSequences = new();
    
    public CheckpointService(
        KnowledgeService knowledgeService,
        ILogger<CheckpointService> logger)
    {
        _knowledgeService = knowledgeService;
        _logger = logger;
    }
    
    public async Task<Checkpoint> StoreCheckpointAsync(string content, string sessionId, string[]? activeFiles = null)
    {
        // Get next sequence number for session
        if (!_sessionSequences.ContainsKey(sessionId))
        {
            _sessionSequences[sessionId] = 0;
        }
        var sequenceNumber = ++_sessionSequences[sessionId];
        
        var checkpoint = new Checkpoint
        {
            Content = content,
            SessionId = sessionId,
            SequenceNumber = sequenceNumber,
            ActiveFiles = activeFiles ?? Array.Empty<string>()
        };
        
        await _knowledgeService.StoreAsync(checkpoint);
        
        _logger.LogInformation("Stored checkpoint #{Seq} for session {Session}", 
            sequenceNumber, sessionId);
        
        return checkpoint;
    }
    
    public async Task<Checkpoint?> GetLatestCheckpointAsync(string? sessionId = null)
    {
        var query = sessionId != null 
            ? $"type:{KnowledgeTypes.Checkpoint} AND sessionId:{sessionId}"
            : $"type:{KnowledgeTypes.Checkpoint}";
        
        var results = await _knowledgeService.SearchAsync(query, maxResults: 1);
        
        return results.FirstOrDefault() as Checkpoint;
    }
    
    public async Task<List<Checkpoint>> GetCheckpointTimelineAsync(string sessionId, int maxResults = 20)
    {
        var query = $"type:{KnowledgeTypes.Checkpoint} AND sessionId:{sessionId}";
        var results = await _knowledgeService.SearchAsync(query, maxResults: maxResults);
        
        return results.Cast<Checkpoint>().OrderBy(c => c.SequenceNumber).ToList();
    }
    
    public async Task<Checkpoint?> RestoreCheckpointAsync(string checkpointId)
    {
        var knowledge = await _knowledgeService.GetByIdAsync(checkpointId);
        
        if (knowledge is not Checkpoint checkpoint)
        {
            _logger.LogWarning("Checkpoint not found: {Id}", checkpointId);
            return null;
        }
        
        _logger.LogInformation("Restored checkpoint #{Seq} from session {Session}",
            checkpoint.SequenceNumber, checkpoint.SessionId);
        
        return checkpoint;
    }
}
```

### 6. MCP Tools Implementation

#### Tools/StoreKnowledgeTool.cs

```csharp
using COA.Mcp.Framework.Attributes;
using COA.Mcp.Framework.Interfaces;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using System.ComponentModel;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Tools;

[McpServerToolType]
public class StoreKnowledgeTool
{
    private readonly KnowledgeService _knowledgeService;
    
    public StoreKnowledgeTool(KnowledgeService knowledgeService)
    {
        _knowledgeService = knowledgeService;
    }
    
    [McpServerTool(Name = "store_knowledge")]
    [Description("Store knowledge in the centralized knowledge base")]
    public async Task<StoreKnowledgeResult> ExecuteAsync(StoreKnowledgeParams parameters)
    {
        try
        {
            var knowledge = new Knowledge
            {
                Type = parameters.Type ?? KnowledgeTypes.WorkNote,
                Content = parameters.Content
            };
            
            // Add code snippets if provided
            if (parameters.CodeSnippets != null)
            {
                knowledge.CodeSnippets = parameters.CodeSnippets;
            }
            
            // Add metadata fields
            if (parameters.Metadata != null)
            {
                foreach (var kvp in parameters.Metadata)
                {
                    knowledge.SetMetadata(kvp.Key, kvp.Value);
                }
            }
            
            // Add common metadata
            if (!string.IsNullOrEmpty(parameters.Status))
                knowledge.SetMetadata("status", parameters.Status);
            if (!string.IsNullOrEmpty(parameters.Priority))
                knowledge.SetMetadata("priority", parameters.Priority);
            if (parameters.Tags != null && parameters.Tags.Length > 0)
                knowledge.SetMetadata("tags", parameters.Tags);
            if (parameters.RelatedTo != null && parameters.RelatedTo.Length > 0)
                knowledge.SetMetadata("relatedTo", parameters.RelatedTo);
            
            var stored = await _knowledgeService.StoreAsync(knowledge);
            
            return new StoreKnowledgeResult
            {
                Success = true,
                Id = stored.Id,
                Message = $"Stored {stored.Type} knowledge: {stored.Id}"
            };
        }
        catch (Exception ex)
        {
            return new StoreKnowledgeResult
            {
                Success = false,
                Message = $"Failed to store knowledge: {ex.Message}"
            };
        }
    }
}

public class StoreKnowledgeParams
{
    [Description("The knowledge content")]
    public string Content { get; set; } = string.Empty;
    
    [Description("Knowledge type (Checkpoint, Checklist, TechnicalDebt, ProjectInsight, WorkNote)")]
    public string? Type { get; set; }
    
    [Description("Code snippets with syntax information")]
    public List<CodeSnippet>? CodeSnippets { get; set; }
    
    [Description("Additional metadata fields")]
    public Dictionary<string, object>? Metadata { get; set; }
    
    [Description("Status of the knowledge item")]
    public string? Status { get; set; }
    
    [Description("Priority level")]
    public string? Priority { get; set; }
    
    [Description("Tags for categorization")]
    public string[]? Tags { get; set; }
    
    [Description("IDs of related knowledge items")]
    public string[]? RelatedTo { get; set; }
}

public class StoreKnowledgeResult
{
    public bool Success { get; set; }
    public string? Id { get; set; }
    public string Message { get; set; } = string.Empty;
}
```

### 7. HTTP API for Federation

#### Api/KnowledgeApiController.cs

```csharp
using Microsoft.AspNetCore.Mvc;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Models;

namespace COA.ProjectKnowledge.McpServer.Api;

[ApiController]
[Route("api/knowledge")]
public class KnowledgeApiController : ControllerBase
{
    private readonly KnowledgeService _knowledgeService;
    private readonly FederationService _federationService;
    
    public KnowledgeApiController(
        KnowledgeService knowledgeService,
        FederationService federationService)
    {
        _knowledgeService = knowledgeService;
        _federationService = federationService;
    }
    
    [HttpPost("store")]
    public async Task<IActionResult> StoreKnowledge([FromBody] StoreKnowledgeRequest request)
    {
        var knowledge = new Knowledge
        {
            Type = request.Type,
            Content = request.Content,
            CodeSnippets = request.CodeSnippets ?? new(),
            Metadata = request.Metadata ?? new()
        };
        
        // Add source information for federated knowledge
        knowledge.SetMetadata("source", request.Source ?? "api");
        knowledge.SetMetadata("sourceTimestamp", DateTime.UtcNow);
        
        var stored = await _knowledgeService.StoreAsync(knowledge);
        
        return Ok(new
        {
            success = true,
            id = stored.Id,
            message = $"Knowledge stored successfully"
        });
    }
    
    [HttpGet("search")]
    public async Task<IActionResult> SearchKnowledge([FromQuery] string query, [FromQuery] int maxResults = 50)
    {
        var results = await _knowledgeService.SearchAsync(query, maxResults: maxResults);
        
        return Ok(new
        {
            results = results,
            count = results.Count,
            query = query
        });
    }
    
    [HttpPost("contribute")]
    public async Task<IActionResult> ContributeExternal([FromBody] ExternalContribution contribution)
    {
        // Validate contribution source
        if (!await _federationService.ValidateSourceAsync(contribution.SourceId, contribution.ApiKey))
        {
            return Unauthorized("Invalid source credentials");
        }
        
        var knowledge = new Knowledge
        {
            Type = contribution.Type,
            Content = contribution.Content,
            Workspace = contribution.Workspace ?? "external"
        };
        
        // Mark as external contribution
        knowledge.SetMetadata("externalSource", contribution.SourceId);
        knowledge.SetMetadata("externalProject", contribution.ProjectName);
        knowledge.SetMetadata("contributedAt", DateTime.UtcNow);
        
        var stored = await _knowledgeService.StoreAsync(knowledge);
        
        return Ok(new
        {
            success = true,
            id = stored.Id,
            message = $"External contribution accepted from {contribution.SourceId}"
        });
    }
    
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            service = "ProjectKnowledge",
            timestamp = DateTime.UtcNow
        });
    }
}

public class StoreKnowledgeRequest
{
    public string Type { get; set; } = KnowledgeTypes.WorkNote;
    public string Content { get; set; } = string.Empty;
    public List<CodeSnippet>? CodeSnippets { get; set; }
    public Dictionary<string, JsonElement>? Metadata { get; set; }
    public string? Source { get; set; }
}

public class ExternalContribution
{
    public string SourceId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Type { get; set; } = KnowledgeTypes.ProjectInsight;
    public string Content { get; set; } = string.Empty;
    public string? Workspace { get; set; }
}
```

### 8. Configuration (appsettings.json)

```json
{
  "ProjectKnowledge": {
    "Database": {
      "Path": "C:\\source\\.coa\\knowledge\\workspace.db",
      "ConnectionString": "Data Source={Path};Mode=ReadWriteCreate;Cache=Shared"
    },
    "Federation": {
      "Enabled": true,
      "Port": 5100,
      "AllowedOrigins": ["http://localhost:*", "https://localhost:*"],
      "RequireApiKey": false
    },
    "KnowledgeTypes": {
      "Enabled": ["Checkpoint", "Checklist", "TechnicalDebt", "ProjectInsight", "WorkNote"]
    },
    "AutoService": {
      "Enabled": true,
      "AutoRestart": true,
      "HealthCheckInterval": 60,
      "MaxRestartAttempts": 3
    },
    "Workspace": {
      "DefaultWorkspace": "default",
      "DetectionStrategy": "GitRoot" 
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "COA": "Debug"
    }
  }
}
```

## Migration Strategy

### From COA CodeSearch Memory System

1. **Export existing memories**:
   ```bash
   mcp__codesearch__backup_memories
   ```

2. **Transform to new schema**:
   - Map 44+ types to 5 core types
   - Preserve checkpoint and checklist data as-is
   - Convert other types based on mapping rules

3. **Import into SQLite**:
   - Use migration tool to batch import
   - Validate data integrity
   - Create relationships based on existing links

### Type Mapping Rules

| Old Type | New Type | Notes |
|----------|----------|-------|
| Checkpoint | Checkpoint | Direct mapping |
| Checklist, ChecklistItem | Checklist | Combine into single type |
| TechnicalDebt, Blocker, BugReport | TechnicalDebt | Consolidate issue types |
| ArchitecturalDecision, CodePattern, SecurityRule | ProjectInsight | Knowledge & decisions |
| All others | WorkNote | General notes |

## Testing Strategy

### Unit Tests
- Service layer tests
- Tool parameter validation
- Database operations
- ID generation

### Integration Tests
- STDIO mode operation
- HTTP API endpoints
- Auto-service startup
- Federation scenarios

### Performance Tests
- Search performance with 10,000+ entries
- Concurrent access
- SQLite write performance
- Memory usage

## Deployment

### Build and Publish
```bash
cd COA.ProjectKnowledge.McpServer
dotnet build -c Release
dotnet publish -c Release -o ../publish
```

### Configure Claude Code
Add to MCP settings:
```json
{
  "mcpServers": {
    "projectknowledge": {
      "command": "dotnet",
      "args": ["C:/source/COA ProjectKnowledge MCP/publish/COA.ProjectKnowledge.McpServer.dll", "stdio"],
      "env": {
        "PROJECTKNOWLEDGE_HTTP_PORT": "5100"
      }
    }
  }
}
```

## Success Metrics

1. **Simplification**: 5 types vs 44+ ✓
2. **Performance**: < 100ms search response ✓
3. **Federation**: HTTP API accessible ✓
4. **Reliability**: Auto-restart on failure ✓
5. **Migration**: Zero data loss ✓

## Troubleshooting

### Common Issues

1. **Port 5100 already in use**
   - Check if previous instance running
   - Change port in appsettings.json

2. **Database locked**
   - SQLite connection pooling issue
   - Ensure proper connection disposal

3. **Auto-service not starting**
   - Check logs for startup errors
   - Verify framework 1.4.2 installed

## Future Enhancements

1. **Web UI** - Browse knowledge via browser
2. **Export formats** - Markdown, JSON, HTML
3. **Sync** - Cloud backup/sync capability
4. **Analytics** - Knowledge usage patterns
5. **AI Integration** - Smart suggestions based on context

## Conclusion

This implementation guide provides everything needed to build the COA ProjectKnowledge MCP server. The design emphasizes simplicity (5 types vs 44+), reliability (SQLite), and extensibility (federation API). With the COA.Mcp.Framework 1.4.2's auto-service capability, the server can operate in dual mode - serving Claude Code via STDIO while exposing an HTTP API for team collaboration.