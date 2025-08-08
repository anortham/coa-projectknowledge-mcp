# Federation Guide: Connecting Multiple MCP Servers to ProjectKnowledge

## Overview

Federation allows multiple MCP servers and tools to contribute knowledge to the centralized ProjectKnowledge system. This guide explains how to integrate your MCP server with ProjectKnowledge.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                   ProjectKnowledge                        │
│                  (Central Knowledge Hub)                  │
│                   HTTP API on port 5100                   │
└─────────────┬──────────────┬──────────────┬──────────────┘
              │              │              │
    Federation API   Federation API   Federation API
              │              │              │
    ┌─────────▼──────┐ ┌────▼──────┐ ┌────▼──────┐
    │  SQL Analyzer  │ │  Web Tool │ │  Custom   │
    │  MCP Server    │ │  MCP      │ │  Scripts  │
    └────────────────┘ └───────────┘ └───────────┘
```

## Quick Start

### For MCP Server Developers

1. **Add COA.Mcp.Client package**:
```xml
<PackageReference Include="COA.Mcp.Client" Version="1.4.2" />
```

2. **Create federation client**:
```csharp
using COA.Mcp.Client;

public class KnowledgeFederationClient
{
    private readonly McpHttpClient _client;
    
    public KnowledgeFederationClient()
    {
        _client = new McpHttpClient(new McpClientOptions
        {
            BaseUrl = "http://localhost:5100",
            ClientInfo = new ClientInfo 
            { 
                Name = "YourMcpServer", 
                Version = "1.0.0" 
            }
        });
    }
    
    public async Task<string> StoreKnowledgeAsync(
        string content, 
        string type = "ProjectInsight")
    {
        await _client.ConnectAsync();
        
        var result = await _client.CallToolAsync("store_knowledge", new
        {
            content = content,
            type = type,
            source = "YourMcpServer"
        });
        
        return result.Id;
    }
}
```

3. **Use in your MCP tools**:
```csharp
[McpServerTool(Name = "analyze_database")]
public async Task<object> AnalyzeDatabase(AnalyzeParams parameters)
{
    // Your analysis logic
    var insights = AnalyzeSchema(parameters.ConnectionString);
    
    // Store insights in ProjectKnowledge
    var federation = new KnowledgeFederationClient();
    foreach (var insight in insights)
    {
        await federation.StoreKnowledgeAsync(
            insight.Description,
            "ProjectInsight"
        );
    }
    
    return new { success = true, insights = insights.Count };
}
```

## Integration Patterns

### Pattern 1: Event-Driven Federation

Automatically store knowledge when certain events occur:

```csharp
public class EventDrivenFederation
{
    private readonly KnowledgeFederationClient _federation;
    
    public EventDrivenFederation()
    {
        _federation = new KnowledgeFederationClient();
        
        // Subscribe to events
        DatabaseAnalyzer.OnIssueFound += StoreIssue;
        CodeReviewer.OnPatternDetected += StorePattern;
    }
    
    private async void StoreIssue(object sender, IssueEventArgs e)
    {
        await _federation.StoreKnowledgeAsync(
            $"Database issue: {e.Description}",
            "TechnicalDebt"
        );
    }
    
    private async void StorePattern(object sender, PatternEventArgs e)
    {
        await _federation.StoreKnowledgeAsync(
            $"Code pattern detected: {e.Pattern}",
            "ProjectInsight"
        );
    }
}
```

### Pattern 2: Batch Federation

Collect knowledge and send in batches:

```csharp
public class BatchFederation
{
    private readonly List<Knowledge> _batch = new();
    private readonly Timer _timer;
    
    public BatchFederation()
    {
        // Send batch every 5 minutes
        _timer = new Timer(SendBatch, null, 
            TimeSpan.FromMinutes(5), 
            TimeSpan.FromMinutes(5));
    }
    
    public void AddKnowledge(string content, string type)
    {
        _batch.Add(new Knowledge { Content = content, Type = type });
        
        // Send immediately if batch is large
        if (_batch.Count >= 50)
        {
            SendBatch(null);
        }
    }
    
    private async void SendBatch(object state)
    {
        if (_batch.Count == 0) return;
        
        var client = new HttpClient();
        var json = JsonSerializer.Serialize(_batch);
        
        var response = await client.PostAsync(
            "http://localhost:5100/api/knowledge/batch",
            new StringContent(json, Encoding.UTF8, "application/json")
        );
        
        if (response.IsSuccessStatusCode)
        {
            _batch.Clear();
        }
    }
}
```

### Pattern 3: Filtered Federation

Only federate important knowledge:

```csharp
public class FilteredFederation
{
    private readonly KnowledgeFederationClient _federation;
    
    public async Task ProcessAndFederateAsync(AnalysisResult result)
    {
        // Only federate high-priority items
        if (result.Severity >= Severity.High)
        {
            await _federation.StoreKnowledgeAsync(
                result.Description,
                "TechnicalDebt"
            );
        }
        
        // Only federate architectural decisions
        if (result.Category == "Architecture")
        {
            await _federation.StoreKnowledgeAsync(
                result.Description,
                "ProjectInsight"
            );
        }
    }
}
```

## SQL Team Integration Example

Complete example for SQL-focused MCP servers:

```csharp
using COA.Mcp.Framework.Server;
using COA.Mcp.Client;

namespace COA.SqlAnalyzer.McpServer;

[McpServerToolType]
public class SqlAnalyzerTool
{
    private readonly McpHttpClient _knowledgeClient;
    
    public SqlAnalyzerTool()
    {
        _knowledgeClient = new McpHttpClient(new McpClientOptions
        {
            BaseUrl = "http://localhost:5100",
            ClientInfo = new ClientInfo 
            { 
                Name = "SqlAnalyzer", 
                Version = "1.0.0" 
            }
        });
    }
    
    [McpServerTool(Name = "analyze_sql_schema")]
    public async Task<object> AnalyzeSchema(SchemaParams parameters)
    {
        var insights = new List<string>();
        
        using var connection = new SqlConnection(parameters.ConnectionString);
        await connection.OpenAsync();
        
        // Check for missing indexes
        var missingIndexes = await FindMissingIndexes(connection);
        foreach (var index in missingIndexes)
        {
            var insight = $"Missing index on {index.Table}.{index.Column}";
            insights.Add(insight);
            
            // Store in ProjectKnowledge
            await StoreKnowledge(insight, "TechnicalDebt", new
            {
                database = connection.Database,
                table = index.Table,
                column = index.Column,
                impact = "performance"
            });
        }
        
        // Check for redundant indexes
        var redundantIndexes = await FindRedundantIndexes(connection);
        foreach (var index in redundantIndexes)
        {
            var insight = $"Redundant index: {index.Name} on {index.Table}";
            insights.Add(insight);
            
            await StoreKnowledge(insight, "TechnicalDebt", new
            {
                database = connection.Database,
                table = index.Table,
                indexName = index.Name,
                impact = "storage"
            });
        }
        
        // Document schema
        var tables = await GetTableDocumentation(connection);
        foreach (var table in tables)
        {
            await StoreKnowledge(
                $"Table {table.Name}: {table.Description}",
                "ProjectInsight",
                new
                {
                    database = connection.Database,
                    tableName = table.Name,
                    columns = table.Columns,
                    rowCount = table.RowCount
                }
            );
        }
        
        return new
        {
            success = true,
            database = connection.Database,
            insightsFound = insights.Count,
            insights = insights
        };
    }
    
    private async Task StoreKnowledge(
        string content, 
        string type, 
        object metadata)
    {
        await _knowledgeClient.ConnectAsync();
        
        await _knowledgeClient.CallToolAsync("store_knowledge", new
        {
            content = content,
            type = type,
            metadata = metadata,
            source = "sql-analyzer",
            tags = new[] { "sql", "database", "schema" }
        });
    }
}
```

## Direct HTTP API Integration

For non-MCP tools or scripts:

### Python Example

```python
import requests
import json

class ProjectKnowledgeClient:
    def __init__(self, base_url="http://localhost:5100"):
        self.base_url = base_url
        self.session = requests.Session()
    
    def store_knowledge(self, content, knowledge_type="WorkNote", **metadata):
        """Store knowledge in ProjectKnowledge"""
        
        payload = {
            "type": knowledge_type,
            "content": content,
            "metadata": metadata,
            "source": "python-script"
        }
        
        response = self.session.post(
            f"{self.base_url}/api/knowledge/store",
            json=payload
        )
        
        if response.status_code == 200:
            result = response.json()
            return result.get("id")
        else:
            raise Exception(f"Failed to store knowledge: {response.text}")
    
    def search_knowledge(self, query, max_results=50):
        """Search knowledge base"""
        
        params = {
            "query": query,
            "maxResults": max_results
        }
        
        response = self.session.get(
            f"{self.base_url}/api/knowledge/search",
            params=params
        )
        
        if response.status_code == 200:
            return response.json().get("results", [])
        else:
            raise Exception(f"Search failed: {response.text}")

# Usage
client = ProjectKnowledgeClient()

# Store analysis result
knowledge_id = client.store_knowledge(
    "Database query optimization needed in OrderService",
    "TechnicalDebt",
    priority="high",
    component="OrderService",
    estimatedHours=4
)

print(f"Stored knowledge: {knowledge_id}")

# Search for related issues
results = client.search_knowledge("OrderService performance")
for result in results:
    print(f"- {result['type']}: {result['content']}")
```

### PowerShell Example

```powershell
function Store-ProjectKnowledge {
    param(
        [string]$Content,
        [string]$Type = "WorkNote",
        [hashtable]$Metadata = @{}
    )
    
    $body = @{
        type = $Type
        content = $Content
        metadata = $Metadata
        source = "powershell-script"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod `
        -Uri "http://localhost:5100/api/knowledge/store" `
        -Method Post `
        -Body $body `
        -ContentType "application/json"
    
    return $response.id
}

# Usage
$knowledgeId = Store-ProjectKnowledge `
    -Content "Backup script needs error handling" `
    -Type "TechnicalDebt" `
    -Metadata @{
        script = "backup.ps1"
        priority = "medium"
    }

Write-Host "Stored knowledge: $knowledgeId"
```

### Bash/cURL Example

```bash
#!/bin/bash

store_knowledge() {
    local content="$1"
    local type="${2:-WorkNote}"
    
    response=$(curl -s -X POST http://localhost:5100/api/knowledge/store \
        -H "Content-Type: application/json" \
        -d "{
            \"type\": \"$type\",
            \"content\": \"$content\",
            \"source\": \"bash-script\"
        }")
    
    echo "$response" | jq -r '.id'
}

# Usage
knowledge_id=$(store_knowledge "CI/CD pipeline needs optimization" "TechnicalDebt")
echo "Stored knowledge: $knowledge_id"
```

## Authentication & Security

### API Key Authentication

When enabled in ProjectKnowledge configuration:

```csharp
// C# with API key
var client = new McpHttpClient(new McpClientOptions
{
    BaseUrl = "http://localhost:5100",
    Authentication = new AuthenticationOptions
    {
        Type = AuthenticationType.ApiKey,
        ApiKey = "your-api-key-here",
        ApiKeyHeader = "X-API-Key"
    }
});
```

```python
# Python with API key
headers = {
    "X-API-Key": "your-api-key-here"
}
response = requests.post(url, json=data, headers=headers)
```

### JWT Authentication

For enterprise environments:

```csharp
var client = new McpHttpClient(new McpClientOptions
{
    BaseUrl = "http://localhost:5100",
    Authentication = new AuthenticationOptions
    {
        Type = AuthenticationType.Jwt,
        JwtToken = "eyJhbGciOiJIUzI1NiIs..."
    }
});
```

## Best Practices

### 1. Use Appropriate Knowledge Types

- **Checkpoint**: Session state only
- **Checklist**: Task lists with trackable items
- **TechnicalDebt**: Issues, bugs, improvements needed
- **ProjectInsight**: Architectural decisions, patterns, documentation
- **WorkNote**: Everything else

### 2. Include Rich Metadata

```csharp
await StoreKnowledge(
    "Performance issue in query",
    "TechnicalDebt",
    new
    {
        component = "UserService",
        method = "GetActiveUsers",
        impact = "high",
        estimatedHours = 8,
        suggestedFix = "Add index on last_login column",
        relatedFiles = new[] { "UserService.cs", "UserRepository.cs" }
    }
);
```

### 3. Batch Operations

Instead of:
```csharp
// Bad: Many individual calls
foreach (var issue in issues)
{
    await StoreKnowledge(issue.Description, "TechnicalDebt");
}
```

Do:
```csharp
// Good: Batch operation
var batch = issues.Select(i => new
{
    content = i.Description,
    type = "TechnicalDebt",
    metadata = new { severity = i.Severity }
}).ToList();

await client.PostAsync("/api/knowledge/batch", batch);
```

### 4. Handle Failures Gracefully

```csharp
public async Task<bool> TryStoreKnowledge(string content, string type)
{
    try
    {
        await _federation.StoreKnowledgeAsync(content, type);
        return true;
    }
    catch (HttpRequestException ex)
    {
        // Log but don't fail the main operation
        _logger.LogWarning($"Failed to federate knowledge: {ex.Message}");
        
        // Queue for retry
        _retryQueue.Enqueue(new { content, type, timestamp = DateTime.UtcNow });
        
        return false;
    }
}
```

### 5. Search Before Store

Avoid duplicates:

```csharp
public async Task<string> StoreUniqueKnowledge(string content, string type)
{
    // Search for existing
    var existing = await SearchKnowledge(content, maxResults: 1);
    
    if (existing.Any(k => k.Content == content))
    {
        // Return existing ID
        return existing.First().Id;
    }
    
    // Store new
    return await StoreKnowledge(content, type);
}
```

## Monitoring Federation

### Health Checks

```csharp
public async Task<bool> IsProjectKnowledgeAvailable()
{
    try
    {
        var response = await _httpClient.GetAsync(
            "http://localhost:5100/api/knowledge/health");
        return response.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}
```

### Metrics

Track federation performance:

```csharp
public class FederationMetrics
{
    public int TotalStored { get; set; }
    public int FailedAttempts { get; set; }
    public double AverageResponseTime { get; set; }
    public DateTime LastSuccessful { get; set; }
    public Queue<string> RecentErrors { get; set; } = new(10);
}
```

## Troubleshooting

### Connection Refused

```
Error: Connection refused on localhost:5100
```

**Solution**:
1. Verify ProjectKnowledge is running
2. Check auto-service started: `netstat -an | findstr :5100`
3. Review logs for startup errors

### Authentication Failed

```
Error: 401 Unauthorized
```

**Solution**:
1. Check API key is correct
2. Verify authentication is configured correctly
3. Ensure API key header name matches configuration

### Rate Limiting

```
Error: 429 Too Many Requests
```

**Solution**:
1. Implement exponential backoff
2. Batch operations
3. Cache frequently accessed data

## Advanced Federation Scenarios

### Multi-Workspace Federation

```csharp
// Store knowledge in specific workspace
await StoreKnowledge(
    content: "SQL optimization needed",
    type: "TechnicalDebt",
    workspace: "sql-projects"  // Separate workspace for SQL team
);
```

### Cross-Project Relationships

```csharp
// Link knowledge across projects
await CreateRelationship(
    fromId: "WEB-18C3A2B4F12",
    toId: "SQL-18C3A2B4F13",
    relationshipType: "depends-on",
    metadata: new
    {
        reason = "Web API depends on SQL stored procedure"
    }
);
```

### Subscription to Updates

```javascript
// WebSocket subscription for real-time updates
const ws = new WebSocket('ws://localhost:5100/ws');

ws.onopen = () => {
    ws.send(JSON.stringify({
        action: 'subscribe',
        types: ['TechnicalDebt', 'ProjectInsight'],
        workspace: 'current'
    }));
};

ws.onmessage = (event) => {
    const update = JSON.parse(event.data);
    console.log(`New ${update.type}: ${update.content}`);
};
```

## Conclusion

Federation enables ProjectKnowledge to serve as a central knowledge hub for all your development tools and MCP servers. By following these patterns and best practices, you can create a comprehensive knowledge system that captures insights from across your entire development ecosystem.