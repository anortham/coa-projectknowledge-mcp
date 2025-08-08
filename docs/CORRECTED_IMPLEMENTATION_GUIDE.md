# COA ProjectKnowledge MCP - CORRECTED Implementation Guide

## ⚠️ IMPORTANT: This is the corrected guide using actual COA MCP Framework patterns

The previous guide had incorrect assumptions about the framework. This guide uses the actual patterns from COA MCP Framework 1.4.2.

## Framework Requirements

- **.NET 9.0** (not .NET 8.0)
- **COA.Mcp.Framework** - The actual framework uses inheritance-based tools, not attributes
- **SQLite** for storage
- Tools inherit from `McpToolBase<TParams, TResult>`

## Correct Project Structure

```
C:\source\COA ProjectKnowledge MCP\
├── COA.ProjectKnowledge.McpServer\
│   ├── Program.cs
│   ├── COA.ProjectKnowledge.McpServer.csproj
│   ├── Models\
│   │   ├── Knowledge.cs
│   │   ├── Checkpoint.cs
│   │   └── Checklist.cs
│   ├── Services\
│   │   ├── KnowledgeService.cs
│   │   ├── CheckpointService.cs
│   │   └── ChecklistService.cs
│   ├── Storage\
│   │   └── KnowledgeDatabase.cs
│   ├── Tools\
│   │   ├── StoreKnowledgeTool.cs
│   │   ├── SearchKnowledgeTool.cs
│   │   ├── CheckpointTool.cs
│   │   └── ChecklistTool.cs
│   └── Results\
│       ├── KnowledgeResult.cs
│       ├── CheckpointResult.cs
│       └── ChecklistResult.cs
└── appsettings.json
```

## 1. Project File (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference the framework project directly for now -->
    <ProjectReference Include="..\..\COA MCP Framework\src\COA.Mcp.Framework\COA.Mcp.Framework.csproj" />
    
    <!-- SQLite -->
    <PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.0" />
    
    <!-- Hosting and DI -->
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.0.0" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

## 2. Program.cs - CORRECT Pattern

```csharp
using COA.Mcp.Framework.Server;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Storage;
using COA.ProjectKnowledge.McpServer.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Load configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Create builder using static factory method
var builder = McpServer.CreateBuilder()
    .WithServerInfo("ProjectKnowledge", "1.0.0", "Simplified knowledge management for development teams");

// Configure logging
builder.ConfigureLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Register configuration
builder.Services.AddSingleton<IConfiguration>(configuration);

// Register services
builder.Services.AddSingleton<KnowledgeDatabase>();
builder.Services.AddSingleton<KnowledgeService>();
builder.Services.AddSingleton<CheckpointService>();
builder.Services.AddSingleton<ChecklistService>();
builder.Services.AddSingleton<WorkspaceResolver>();

// Configure transport based on command line args
var args = Environment.GetCommandLineArgs();
if (args.Contains("--http"))
{
    // HTTP mode for federation
    builder.UseHttpTransport(options =>
    {
        options.Port = configuration.GetValue<int>("Federation:Port", 5100);
        options.Host = "localhost";
        options.EnableWebSocket = true;
        options.EnableCors = true;
    });
    
    Console.WriteLine($"Starting ProjectKnowledge HTTP API on port {options.Port}");
}
else
{
    // Default STDIO mode for Claude Code
    builder.UseStdioTransport();
    
    // Auto-start HTTP service if configured
    if (configuration.GetValue<bool>("Federation:AutoStart", true))
    {
        // Note: Auto-service start would be added here when available in framework
        // For now, users need to manually start HTTP instance
    }
}

// Register tools - CORRECT pattern using RegisterToolType
builder.RegisterToolType<StoreKnowledgeTool>();
builder.RegisterToolType<SearchKnowledgeTool>();
builder.RegisterToolType<CheckpointStoreTool>();
builder.RegisterToolType<CheckpointGetTool>();
builder.RegisterToolType<ChecklistCreateTool>();
builder.RegisterToolType<ChecklistUpdateTool>();

// Initialize database before starting
var serviceProvider = builder.Services.BuildServiceProvider();
var database = serviceProvider.GetRequiredService<KnowledgeDatabase>();
await database.InitializeAsync();

// Run the server
await builder.RunAsync();
```

## 3. Tool Implementation - CORRECT Pattern

### StoreKnowledgeTool.cs

```csharp
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class StoreKnowledgeTool : McpToolBase<StoreKnowledgeParameters, StoreKnowledgeResult>
{
    private readonly KnowledgeService _knowledgeService;
    private readonly ILogger<StoreKnowledgeTool> _logger;

    public StoreKnowledgeTool(
        KnowledgeService knowledgeService,
        ILogger<StoreKnowledgeTool> logger)
    {
        _knowledgeService = knowledgeService;
        _logger = logger;
    }

    public override string Name => "store_knowledge";
    public override string Description => "Store knowledge in the centralized knowledge base";
    public override ToolCategory Category => ToolCategory.DataManagement;

    protected override async Task<StoreKnowledgeResult> ExecuteInternalAsync(
        StoreKnowledgeParameters parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use built-in validation
            var content = ValidateRequired(parameters.Content, nameof(parameters.Content));
            
            // Create knowledge entry
            var knowledge = new Knowledge
            {
                Type = parameters.Type ?? KnowledgeTypes.WorkNote,
                Content = content,
                CodeSnippets = parameters.CodeSnippets ?? new List<CodeSnippet>()
            };

            // Add metadata
            if (!string.IsNullOrEmpty(parameters.Status))
                knowledge.SetMetadata("status", parameters.Status);
            if (!string.IsNullOrEmpty(parameters.Priority))
                knowledge.SetMetadata("priority", parameters.Priority);
            if (parameters.Tags != null && parameters.Tags.Length > 0)
                knowledge.SetMetadata("tags", parameters.Tags);

            // Store knowledge
            var stored = await _knowledgeService.StoreAsync(knowledge);

            _logger.LogInformation("Stored {Type} knowledge: {Id}", stored.Type, stored.Id);

            return new StoreKnowledgeResult
            {
                Success = true,
                Id = stored.Id,
                Type = stored.Type,
                Message = $"Successfully stored {stored.Type} knowledge"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store knowledge");
            
            return new StoreKnowledgeResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "STORE_FAILED",
                    Message = ex.Message,
                    Recovery = new RecoveryInfo
                    {
                        Steps = new[] { "Check the content is valid", "Verify the knowledge type is supported" },
                        SuggestedActions = new List<SuggestedAction>
                        {
                            new() { Tool = "search_knowledge", Description = "Search for existing knowledge" }
                        }
                    }
                }
            };
        }
    }
}

public class StoreKnowledgeParameters
{
    [Required]
    [Description("The knowledge content to store")]
    public string Content { get; set; } = string.Empty;

    [Description("Knowledge type: Checkpoint, Checklist, TechnicalDebt, ProjectInsight, or WorkNote")]
    public string? Type { get; set; }

    [Description("Code snippets with syntax information")]
    public List<CodeSnippet>? CodeSnippets { get; set; }

    [Description("Status of the knowledge item")]
    public string? Status { get; set; }

    [Description("Priority level (high, medium, low)")]
    public string? Priority { get; set; }

    [Description("Tags for categorization")]
    public string[]? Tags { get; set; }
}

public class StoreKnowledgeResult : ToolResultBase
{
    public override string Operation => "store_knowledge";
    
    public string? Id { get; set; }
    public string? Type { get; set; }
    public string? Message { get; set; }
}
```

### SearchKnowledgeTool.cs

```csharp
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class SearchKnowledgeTool : McpToolBase<SearchKnowledgeParameters, SearchKnowledgeResult>
{
    private readonly KnowledgeService _knowledgeService;

    public SearchKnowledgeTool(KnowledgeService knowledgeService)
    {
        _knowledgeService = knowledgeService;
    }

    public override string Name => "search_knowledge";
    public override string Description => "Search the knowledge base";
    public override ToolCategory Category => ToolCategory.DataRetrieval;

    protected override async Task<SearchKnowledgeResult> ExecuteInternalAsync(
        SearchKnowledgeParameters parameters,
        CancellationToken cancellationToken)
    {
        // Validate required parameters
        var query = ValidateRequired(parameters.Query, nameof(parameters.Query));
        
        // Perform search
        var results = await _knowledgeService.SearchAsync(
            query,
            parameters.Workspace,
            parameters.MaxResults ?? 50
        );

        return new SearchKnowledgeResult
        {
            Success = true,
            Query = query,
            Results = results.Select(k => new KnowledgeItem
            {
                Id = k.Id,
                Type = k.Type,
                Content = k.Content,
                CreatedAt = k.CreatedAt,
                Tags = k.Tags
            }).ToList(),
            TotalFound = results.Count
        };
    }
}

public class SearchKnowledgeParameters
{
    [Required]
    [Description("Search query to find knowledge")]
    public string Query { get; set; } = string.Empty;

    [Description("Workspace to search in (optional)")]
    public string? Workspace { get; set; }

    [Description("Maximum number of results to return")]
    [Range(1, 500)]
    public int? MaxResults { get; set; }
}

public class SearchKnowledgeResult : ToolResultBase
{
    public override string Operation => "search_knowledge";
    
    public string Query { get; set; } = string.Empty;
    public List<KnowledgeItem> Results { get; set; } = new();
    public int TotalFound { get; set; }
}

public class KnowledgeItem
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string[]? Tags { get; set; }
}
```

### CheckpointStoreTool.cs

```csharp
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class CheckpointStoreTool : McpToolBase<CheckpointStoreParameters, CheckpointStoreResult>
{
    private readonly CheckpointService _checkpointService;

    public CheckpointStoreTool(CheckpointService checkpointService)
    {
        _checkpointService = checkpointService;
    }

    public override string Name => "store_checkpoint";
    public override string Description => "Store a session checkpoint for later restoration";
    public override ToolCategory Category => ToolCategory.SessionManagement;

    protected override async Task<CheckpointStoreResult> ExecuteInternalAsync(
        CheckpointStoreParameters parameters,
        CancellationToken cancellationToken)
    {
        var content = ValidateRequired(parameters.Content, nameof(parameters.Content));
        var sessionId = ValidateRequired(parameters.SessionId, nameof(parameters.SessionId));

        var checkpoint = await _checkpointService.StoreCheckpointAsync(
            content,
            sessionId,
            parameters.ActiveFiles
        );

        return new CheckpointStoreResult
        {
            Success = true,
            CheckpointId = checkpoint.Id,
            SequenceNumber = checkpoint.SequenceNumber,
            SessionId = checkpoint.SessionId,
            Message = $"Checkpoint #{checkpoint.SequenceNumber} stored for session {sessionId}"
        };
    }
}

public class CheckpointStoreParameters
{
    [Required]
    [Description("The checkpoint content/state to store")]
    public string Content { get; set; } = string.Empty;

    [Required]
    [Description("Session identifier")]
    public string SessionId { get; set; } = string.Empty;

    [Description("Files that were active at checkpoint time")]
    public string[]? ActiveFiles { get; set; }
}

public class CheckpointStoreResult : ToolResultBase
{
    public override string Operation => "store_checkpoint";
    
    public string CheckpointId { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
```

## 4. Service Implementation

### KnowledgeService.cs

```csharp
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Storage;
using Microsoft.Extensions.Logging;

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
}
```

### WorkspaceResolver.cs

```csharp
namespace COA.ProjectKnowledge.McpServer.Services;

public class WorkspaceResolver
{
    private readonly IConfiguration _configuration;
    
    public WorkspaceResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetCurrentWorkspace()
    {
        // Try to find workspace from current directory
        var currentDir = Directory.GetCurrentDirectory();
        
        // Look for .git folder or solution file
        var dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                dir.GetFiles("*.sln").Any())
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        // Fallback to configured default
        return _configuration["ProjectKnowledge:Workspace:DefaultWorkspace"] ?? "default";
    }
}
```

## 5. Database Layer

### KnowledgeDatabase.cs

```csharp
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Storage;

public class KnowledgeDatabase
{
    private readonly string _connectionString;
    private readonly ILogger<KnowledgeDatabase> _logger;

    public KnowledgeDatabase(IConfiguration configuration, ILogger<KnowledgeDatabase> logger)
    {
        var dbPath = configuration["ProjectKnowledge:Database:Path"] 
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                ".coa", "knowledge", "workspace.db");
        
        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        
        _connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared";
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS knowledge (
                id TEXT PRIMARY KEY,
                type TEXT NOT NULL,
                content TEXT NOT NULL,
                code_snippets TEXT,
                metadata TEXT,
                workspace TEXT NOT NULL,
                created_at INTEGER NOT NULL,
                modified_at INTEGER NOT NULL,
                accessed_at INTEGER,
                access_count INTEGER DEFAULT 0,
                is_archived BOOLEAN DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_knowledge_type ON knowledge(type);
            CREATE INDEX IF NOT EXISTS idx_knowledge_workspace ON knowledge(workspace);
            CREATE INDEX IF NOT EXISTS idx_knowledge_created ON knowledge(created_at DESC);
        ";

        using var command = new SqliteCommand(createTableSql, connection);
        await command.ExecuteNonQueryAsync();
        
        _logger.LogInformation("Database initialized at {Path}", _connectionString);
    }

    public async Task InsertKnowledgeAsync(Knowledge knowledge)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO knowledge (id, type, content, code_snippets, metadata, workspace, 
                                  created_at, modified_at, access_count, is_archived)
            VALUES (@id, @type, @content, @codeSnippets, @metadata, @workspace, 
                    @createdAt, @modifiedAt, 0, 0)";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", knowledge.Id);
        command.Parameters.AddWithValue("@type", knowledge.Type);
        command.Parameters.AddWithValue("@content", knowledge.Content);
        command.Parameters.AddWithValue("@codeSnippets", JsonSerializer.Serialize(knowledge.CodeSnippets));
        command.Parameters.AddWithValue("@metadata", JsonSerializer.Serialize(knowledge.Metadata));
        command.Parameters.AddWithValue("@workspace", knowledge.Workspace);
        command.Parameters.AddWithValue("@createdAt", knowledge.CreatedAt.Ticks);
        command.Parameters.AddWithValue("@modifiedAt", knowledge.ModifiedAt.Ticks);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<Knowledge>> SearchKnowledgeAsync(string query, string workspace, int maxResults)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Simple LIKE search for now (can be enhanced with FTS5)
        var sql = @"
            SELECT * FROM knowledge 
            WHERE workspace = @workspace 
              AND (content LIKE @query OR type LIKE @query OR metadata LIKE @query)
              AND is_archived = 0
            ORDER BY created_at DESC
            LIMIT @limit";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@workspace", workspace);
        command.Parameters.AddWithValue("@query", $"%{query}%");
        command.Parameters.AddWithValue("@limit", maxResults);

        var results = new List<Knowledge>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var knowledge = new Knowledge
            {
                Id = reader.GetString(0),
                Type = reader.GetString(1),
                Content = reader.GetString(2),
                Workspace = reader.GetString(5),
                CreatedAt = new DateTime(reader.GetInt64(6)),
                ModifiedAt = new DateTime(reader.GetInt64(7)),
                AccessCount = reader.GetInt32(9)
            };

            // Deserialize JSON fields
            if (!reader.IsDBNull(3))
            {
                var snippetsJson = reader.GetString(3);
                knowledge.CodeSnippets = JsonSerializer.Deserialize<List<CodeSnippet>>(snippetsJson) ?? new();
            }

            if (!reader.IsDBNull(4))
            {
                var metadataJson = reader.GetString(4);
                knowledge.Metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(metadataJson) ?? new();
            }

            results.Add(knowledge);
        }

        return results;
    }

    public async Task UpdateAccessTrackingAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            UPDATE knowledge 
            SET accessed_at = @now, access_count = access_count + 1
            WHERE id = @id";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@now", DateTime.UtcNow.Ticks);
        command.Parameters.AddWithValue("@id", id);

        await command.ExecuteNonQueryAsync();
    }
}
```

## 6. Models

### Knowledge.cs

```csharp
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Models;

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

public class Knowledge
{
    public string Id { get; set; } = GenerateId();
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

    private static string GenerateId()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = Random.Shared.Next(1000, 9999);
        return $"{timestamp:X}-{random:X4}";
    }

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

    public string[]? Tags => GetMetadata<string[]>("tags");
}

public class CodeSnippet
{
    public string Language { get; set; } = "plaintext";
    public string Code { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public int? StartLine { get; set; }
    public int? EndLine { get; set; }
}
```

## 7. Configuration (appsettings.json)

```json
{
  "ProjectKnowledge": {
    "Database": {
      "Path": "C:\\source\\.coa\\knowledge\\workspace.db"
    },
    "Federation": {
      "AutoStart": true,
      "Port": 5100
    },
    "Workspace": {
      "DefaultWorkspace": "default"
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

## Key Differences from Previous Guide

1. **Tools inherit from `McpToolBase<TParams, TResult>`** - NOT attribute-based
2. **Use `RegisterToolType<T>()`** - NOT attribute discovery
3. **Override properties** like `Name`, `Description`, `Category`
4. **Implement `ExecuteInternalAsync`** - NOT decorated methods
5. **Return `ToolResultBase`-derived types** with standardized error handling
6. **Use built-in validation** - `ValidateRequired()`, `ValidateRange()`
7. **.NET 9.0** - NOT .NET 8.0

## Testing the Implementation

```bash
# Build the project
dotnet build

# Run in STDIO mode (for Claude Code)
dotnet run

# Run in HTTP mode (for federation)
dotnet run -- --http

# Test a tool manually
echo '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"store_knowledge","arguments":{"content":"Test knowledge","type":"WorkNote"}},"id":1}' | dotnet run
```

## Common Errors and Solutions

### Error: Tool not found
**Solution**: Ensure tool is registered with `builder.RegisterToolType<YourTool>()`

### Error: Missing required parameter
**Solution**: Use `ValidateRequired()` in tool implementation

### Error: Type not found
**Solution**: Tools must inherit from `McpToolBase<TParams, TResult>`

This corrected guide should work with the actual COA MCP Framework patterns!