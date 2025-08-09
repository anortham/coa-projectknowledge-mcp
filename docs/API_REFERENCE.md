# COA ProjectKnowledge API Reference

## MCP Tools (STDIO Interface)

These tools are available when running as an MCP server through Claude Code or other MCP clients.

### store_knowledge

Store knowledge in the centralized knowledge base.

**Parameters:**
```typescript
{
  content: string;           // Required: The knowledge content
  type?: string;             // Knowledge type (default: "WorkNote")
                            // Options: Checkpoint, Checklist, TechnicalDebt, 
                            //          ProjectInsight, WorkNote
  codeSnippets?: [{         // Optional: Code snippets with syntax info
    language: string;
    code: string;
    filePath?: string;
    startLine?: number;
    endLine?: number;
  }];
  metadata?: {              // Optional: Additional metadata fields
    [key: string]: any;
  };
  status?: string;          // Optional: Status (pending, done, etc.)
  priority?: string;        // Optional: Priority (high, medium, low)
  tags?: string[];          // Optional: Tags for categorization
  relatedTo?: string[];     // Optional: IDs of related knowledge
}
```

**Returns:**
```typescript
{
  success: boolean;
  id?: string;              // Knowledge ID if successful
  message: string;
}
```

**Example:**
```json
{
  "content": "Database connection pool should be increased to handle peak load",
  "type": "TechnicalDebt",
  "priority": "high",
  "tags": ["performance", "database"],
  "codeSnippets": [{
    "language": "csharp",
    "code": "services.AddDbContext<AppContext>(options => options.UseSqlServer(connString));",
    "filePath": "Startup.cs",
    "startLine": 45
  }]
}
```

---

### find_knowledge

Search the knowledge base with flexible queries.

**Parameters:**
```typescript
{
  query: string;            // Required: Search query
  workspace?: string;       // Optional: Workspace to search (default: current)
  types?: string[];         // Optional: Filter by knowledge types
  maxResults?: number;      // Optional: Max results (default: 50)
  includeArchived?: boolean;// Optional: Include archived items (default: false)
  sortBy?: string;          // Optional: Sort field (created, modified, accessed)
  sortDescending?: boolean; // Optional: Sort order (default: true)
}
```

**Returns:**
```typescript
{
  results: Knowledge[];     // Array of matching knowledge entries
  count: number;           // Number of results returned
  totalFound: number;      // Total matches before limiting
}
```

**Example:**
```json
{
  "query": "database performance",
  "types": ["TechnicalDebt", "ProjectInsight"],
  "maxResults": 20
}
```

---

### store_checkpoint

Store a session checkpoint for later restoration.

**Parameters:**
```typescript
{
  content: string;          // Required: Checkpoint content/state
  sessionId: string;        // Required: Session identifier
  activeFiles?: string[];   // Optional: Files active at checkpoint
}
```

**Returns:**
```typescript
{
  success: boolean;
  checkpoint: {
    id: string;            // Checkpoint ID
    sequenceNumber: number; // Sequential number in session
    timestamp: string;     // When checkpoint was created
  };
  message: string;
}
```

---

### get_latest_checkpoint

Retrieve the most recent checkpoint.

**Parameters:**
```typescript
{
  sessionId?: string;       // Optional: Filter by session (default: any)
}
```

**Returns:**
```typescript
{
  found: boolean;
  checkpoint?: {
    id: string;
    content: string;
    sessionId: string;
    sequenceNumber: number;
    activeFiles: string[];
    createdAt: string;
  };
}
```

---

### create_checklist

Create a new checklist with items.

**Parameters:**
```typescript
{
  title: string;            // Required: Checklist title
  items: string[];          // Required: List of checklist items
  parentId?: string;        // Optional: Parent checklist for nesting
}
```

**Returns:**
```typescript
{
  success: boolean;
  checklist: {
    id: string;
    title: string;
    items: [{
      id: string;
      content: string;
      isCompleted: boolean;
      order: number;
    }];
    completionPercentage: number;
  };
}
```

---

### update_checklist_item

Update a checklist item's completion status.

**Parameters:**
```typescript
{
  checklistId: string;      // Required: Checklist ID
  itemId: string;           // Required: Item ID
  isCompleted: boolean;     // Required: Completion status
}
```

**Returns:**
```typescript
{
  success: boolean;
  updatedItem: {
    id: string;
    content: string;
    isCompleted: boolean;
    completedAt?: string;
  };
  checklistStatus: string;  // Overall checklist status
}
```

---

### link_knowledge

Create relationships between knowledge entries.

**Parameters:**
```typescript
{
  fromId: string;           // Required: Source knowledge ID
  toId: string;             // Required: Target knowledge ID
  relationshipType: string; // Required: Type of relationship
                           // Options: relatedTo, implements, blocks,
                           //          supersedes, references, etc.
  metadata?: object;        // Optional: Relationship metadata
}
```

**Returns:**
```typescript
{
  success: boolean;
  relationship: {
    fromId: string;
    toId: string;
    type: string;
    createdAt: string;
  };
}
```

---

## HTTP API (Federation Interface)

These endpoints are available when the HTTP service is running (auto-started or standalone).

### POST /api/knowledge/store

Store knowledge via HTTP API.

**Request Body:**
```json
{
  "type": "ProjectInsight",
  "content": "Authentication should use JWT tokens",
  "codeSnippets": [...],
  "metadata": {...},
  "source": "sql-mcp-server"
}
```

**Response:**
```json
{
  "success": true,
  "id": "18C3A2B4F12-A3F2",
  "message": "Knowledge stored successfully"
}
```

---

### GET /api/knowledge/search

Search knowledge via HTTP API.

**Query Parameters:**
- `query` (required): Search query
- `maxResults` (optional): Maximum results (default: 50)

**Example:**
```
GET /api/knowledge/search?query=authentication&maxResults=10
```

**Response:**
```json
{
  "results": [...],
  "count": 10,
  "query": "authentication"
}
```

---

### POST /api/knowledge/contribute

Contribute knowledge from external MCP servers.

**Request Body:**
```json
{
  "sourceId": "sql-analyzer",
  "apiKey": "optional-api-key",
  "projectName": "CustomerDB",
  "type": "ProjectInsight",
  "content": "Customer table has redundant indexes",
  "workspace": "sql-projects"
}
```

**Response:**
```json
{
  "success": true,
  "id": "18C3A2B4F12-A3F2",
  "message": "External contribution accepted from sql-analyzer"
}
```

---

### GET /api/knowledge/health

Health check endpoint for monitoring.

**Response:**
```json
{
  "status": "healthy",
  "service": "ProjectKnowledge",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

---

## WebSocket API (Real-time Updates)

Connect to `ws://localhost:5100/ws` for real-time updates.

### Subscribe to Updates

**Send:**
```json
{
  "action": "subscribe",
  "workspace": "current",
  "types": ["TechnicalDebt", "Checklist"]
}
```

### Receive Updates

**Receive:**
```json
{
  "event": "knowledge_created",
  "data": {
    "id": "18C3A2B4F12-A3F2",
    "type": "TechnicalDebt",
    "content": "...",
    "createdAt": "2024-01-15T10:30:00Z"
  }
}
```

---

## Client Libraries

### C# Client Example

```csharp
using COA.Mcp.Client;

var client = new McpHttpClient(new McpClientOptions
{
    BaseUrl = "http://localhost:5100",
    ClientInfo = new ClientInfo 
    { 
        Name = "SQLAnalyzer", 
        Version = "1.0.0" 
    }
});

await client.ConnectAsync();

// Store knowledge
var result = await client.CallToolAsync("store_knowledge", new
{
    content = "Database schema insight",
    type = "ProjectInsight",
    source = "sql-analyzer"
});
```

### JavaScript Client Example

```javascript
const response = await fetch('http://localhost:5100/api/knowledge/store', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
  },
  body: JSON.stringify({
    type: 'ProjectInsight',
    content: 'Frontend performance improvement needed',
    source: 'web-analyzer'
  })
});

const result = await response.json();
console.log(`Stored knowledge: ${result.id}`);
```

---

## Error Responses

All endpoints return consistent error responses:

```json
{
  "success": false,
  "error": {
    "code": "INVALID_TYPE",
    "message": "Invalid knowledge type: CustomType",
    "details": {
      "validTypes": ["Checkpoint", "Checklist", "TechnicalDebt", "ProjectInsight", "WorkNote"]
    }
  }
}
```

### Error Codes

| Code | Description |
|------|-------------|
| INVALID_TYPE | Invalid knowledge type provided |
| NOT_FOUND | Knowledge entry not found |
| INVALID_PARAMS | Missing or invalid parameters |
| WORKSPACE_ERROR | Workspace resolution failed |
| DATABASE_ERROR | Database operation failed |
| AUTH_FAILED | Authentication/authorization failed |
| QUOTA_EXCEEDED | Storage or rate limit exceeded |

---

## Rate Limiting

HTTP API endpoints are rate-limited:
- **Default**: 100 requests per minute per IP
- **Authenticated**: 1000 requests per minute per API key

---

## Authentication (Optional)

When `Federation.RequireApiKey` is enabled in configuration:

```http
Authorization: Bearer your-api-key-here
```

Or:

```http
X-API-Key: your-api-key-here
```

---

## Pagination

For endpoints returning lists, use pagination parameters:

```typescript
{
  page?: number;      // Page number (1-based)
  pageSize?: number;  // Items per page (max: 100)
}
```

Response includes pagination metadata:

```json
{
  "results": [...],
  "pagination": {
    "page": 1,
    "pageSize": 50,
    "totalPages": 5,
    "totalItems": 234
  }
}
```