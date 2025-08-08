# Storage Alternatives for ProjectKnowledge

## The SQLite JSON Problem

You've identified a real issue: SQLite with JSON columns requires:
1. Read JSON string from database
2. Deserialize to object
3. Modify object
4. Serialize back to JSON
5. Write JSON string to database

This is inefficient and leads to:
- Performance overhead
- Potential serialization errors
- Lost type safety
- Concurrency issues
- Complex querying

## Alternative 1: LiteDB (Recommended for Simplicity)

Despite the concerns we found earlier, LiteDB might actually be perfect for ProjectKnowledge because:
- **Direct object storage** - No JSON serialization needed
- **LINQ queries** - Type-safe querying
- **Embedded** - Single file, no server
- **Document updates** - Modify fields directly

### LiteDB Implementation

```csharp
using LiteDB;

public class KnowledgeDatabase
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<Knowledge> _knowledge;
    
    public KnowledgeDatabase(string dbPath)
    {
        _db = new LiteDatabase(dbPath);
        _knowledge = _db.GetCollection<Knowledge>("knowledge");
        
        // Create indexes
        _knowledge.EnsureIndex(x => x.Type);
        _knowledge.EnsureIndex(x => x.Workspace);
        _knowledge.EnsureIndex(x => x.CreatedAt);
    }
    
    public async Task<Knowledge> StoreAsync(Knowledge knowledge)
    {
        _knowledge.Insert(knowledge);
        return knowledge;
    }
    
    public async Task UpdateFieldAsync(string id, string field, object value)
    {
        // Direct field update without full serialization!
        _knowledge.UpdateMany(
            x => x.Id == id,
            x => new Knowledge { /* set specific field */ }
        );
    }
    
    public async Task<List<Knowledge>> SearchAsync(string query, string workspace)
    {
        // LINQ queries with no deserialization needed
        return _knowledge
            .Find(x => x.Workspace == workspace && 
                      (x.Content.Contains(query) || x.Type.Contains(query)))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }
    
    // Partial updates without full object serialization
    public async Task AddTagAsync(string id, string tag)
    {
        var doc = _knowledge.FindById(id);
        doc.Tags.Add(tag);
        _knowledge.Update(doc);  // Only the changed field is updated
    }
}

// Models work directly - no JSON attributes needed
public class Knowledge
{
    [BsonId]
    public string Id { get; set; }
    public string Type { get; set; }
    public string Content { get; set; }
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<CodeSnippet> CodeSnippets { get; set; } = new();
    
    // Methods work naturally
    public void AddMetadata(string key, object value)
    {
        Metadata[key] = value;  // Direct manipulation, no JSON!
    }
}
```

### LiteDB Pros:
- **No serialization overhead** - Works with POCOs directly
- **Atomic updates** - Update single fields efficiently
- **LINQ support** - Type-safe queries
- **Embedded** - No external dependencies
- **ACID compliant** - Better than you might think

### LiteDB Cons (and mitigations):
- **695 open issues** → Most are feature requests, not bugs
- **Single writer** → Fine for MCP server (single process)
- **.NET only** → Perfect for your use case

## Alternative 2: Entity Framework Core with SQLite

Use EF Core with a properly normalized schema instead of JSON:

```csharp
public class KnowledgeContext : DbContext
{
    public DbSet<Knowledge> Knowledge { get; set; }
    public DbSet<KnowledgeMetadata> Metadata { get; set; }
    public DbSet<CodeSnippet> CodeSnippets { get; set; }
    public DbSet<Tag> Tags { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite("Data Source=knowledge.db");
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Proper relationships instead of JSON
        modelBuilder.Entity<Knowledge>()
            .HasMany(k => k.Metadata)
            .WithOne(m => m.Knowledge)
            .HasForeignKey(m => m.KnowledgeId);
            
        modelBuilder.Entity<Knowledge>()
            .HasMany(k => k.CodeSnippets)
            .WithOne(c => c.Knowledge)
            .HasForeignKey(c => c.KnowledgeId);
    }
}

// Normalized models
public class Knowledge
{
    public string Id { get; set; }
    public string Type { get; set; }
    public string Content { get; set; }
    public string Workspace { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties - no JSON!
    public virtual ICollection<KnowledgeMetadata> Metadata { get; set; }
    public virtual ICollection<CodeSnippet> CodeSnippets { get; set; }
    public virtual ICollection<Tag> Tags { get; set; }
}

public class KnowledgeMetadata
{
    public int Id { get; set; }
    public string KnowledgeId { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
    public virtual Knowledge Knowledge { get; set; }
}

// Usage - no serialization!
public class KnowledgeService
{
    private readonly KnowledgeContext _context;
    
    public async Task UpdateMetadataAsync(string knowledgeId, string key, string value)
    {
        // Direct database update - no JSON serialization!
        var metadata = await _context.Metadata
            .FirstOrDefaultAsync(m => m.KnowledgeId == knowledgeId && m.Key == key);
            
        if (metadata != null)
        {
            metadata.Value = value;
        }
        else
        {
            _context.Metadata.Add(new KnowledgeMetadata
            {
                KnowledgeId = knowledgeId,
                Key = key,
                Value = value
            });
        }
        
        await _context.SaveChangesAsync();
    }
    
    public async Task<Knowledge> GetWithMetadataAsync(string id)
    {
        // Eager load related data
        return await _context.Knowledge
            .Include(k => k.Metadata)
            .Include(k => k.CodeSnippets)
            .Include(k => k.Tags)
            .FirstOrDefaultAsync(k => k.Id == id);
    }
}
```

### EF Core Pros:
- **No JSON serialization** - Properly normalized
- **Lazy/eager loading** - Load only what you need
- **LINQ queries** - Full type safety
- **Migrations** - Schema versioning built-in
- **Change tracking** - Automatic dirty checking

### EF Core Cons:
- **More complex schema** - Multiple tables
- **Learning curve** - If not familiar with EF
- **Overhead** - More than raw SQL

## Alternative 3: Hybrid Approach - SQLite with Separate Metadata Table

Keep core fields in columns, only variable data in JSON:

```sql
-- Main table with core fields as columns
CREATE TABLE knowledge (
    id TEXT PRIMARY KEY,
    type TEXT NOT NULL,
    content TEXT NOT NULL,
    workspace TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    modified_at INTEGER NOT NULL,
    -- Only truly dynamic data in JSON
    extra_data TEXT
);

-- Separate table for frequently updated metadata
CREATE TABLE knowledge_metadata (
    knowledge_id TEXT NOT NULL,
    key TEXT NOT NULL,
    value TEXT NOT NULL,
    PRIMARY KEY (knowledge_id, key),
    FOREIGN KEY (knowledge_id) REFERENCES knowledge(id)
);

-- Separate table for code snippets
CREATE TABLE code_snippets (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    knowledge_id TEXT NOT NULL,
    language TEXT,
    code TEXT,
    file_path TEXT,
    start_line INTEGER,
    FOREIGN KEY (knowledge_id) REFERENCES knowledge(id)
);
```

```csharp
public class HybridKnowledgeDatabase
{
    public async Task UpdateMetadataAsync(string knowledgeId, string key, string value)
    {
        // Direct SQL update - no JSON involved!
        var sql = @"
            INSERT OR REPLACE INTO knowledge_metadata (knowledge_id, key, value)
            VALUES (@id, @key, @value)";
            
        using var cmd = new SqliteCommand(sql, _connection);
        cmd.Parameters.AddWithValue("@id", knowledgeId);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        await cmd.ExecuteNonQueryAsync();
    }
    
    public async Task<Dictionary<string, string>> GetMetadataAsync(string knowledgeId)
    {
        var sql = "SELECT key, value FROM knowledge_metadata WHERE knowledge_id = @id";
        using var cmd = new SqliteCommand(sql, _connection);
        cmd.Parameters.AddWithValue("@id", knowledgeId);
        
        var metadata = new Dictionary<string, string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            metadata[reader.GetString(0)] = reader.GetString(1);
        }
        return metadata;
    }
}
```

## Alternative 4: RavenDB Embedded (If you want power)

RavenDB has an embedded mode that's perfect for this:

```csharp
using Raven.Client.Documents;
using Raven.Embedded;

public class RavenKnowledgeDatabase
{
    private readonly IDocumentStore _store;
    
    public RavenKnowledgeDatabase(string dbPath)
    {
        EmbeddedServer.Instance.StartServer(new ServerOptions
        {
            DataDirectory = dbPath,
            ServerUrl = "http://127.0.0.1:0"
        });
        
        _store = EmbeddedServer.Instance.GetDocumentStore("Knowledge");
        _store.Initialize();
    }
    
    public async Task StoreAsync(Knowledge knowledge)
    {
        using var session = _store.OpenAsyncSession();
        await session.StoreAsync(knowledge);
        await session.SaveChangesAsync();
    }
    
    public async Task UpdateFieldAsync(string id, string field, object value)
    {
        using var session = _store.OpenAsyncSession();
        session.Advanced.Patch<Knowledge>(id, x => x.Field, value);
        await session.SaveChangesAsync();
    }
    
    public async Task<List<Knowledge>> SearchAsync(string query)
    {
        using var session = _store.OpenAsyncSession();
        return await session.Query<Knowledge>()
            .Search(x => x.Content, query)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }
}
```

### RavenDB Pros:
- **Document database** - Designed for this use case
- **ACID compliant** - Full transactions
- **Powerful queries** - Full-text search, map-reduce
- **Change tracking** - Automatic
- **Patch updates** - Update single fields efficiently

### RavenDB Cons:
- **Larger footprint** - ~100MB
- **Learning curve** - More complex
- **Licensing** - AGPL (free for your use case)

## Recommendation

For ProjectKnowledge, I recommend:

### 1. **LiteDB** (Best overall for your needs)
- Solves the JSON serialization problem completely
- Simple API that works with POCOs
- Single file deployment
- Good enough performance
- Yes, it has open issues, but it's been stable for years

### 2. **Hybrid SQLite** (If you must use SQLite)
- Keep core fields as columns
- Use separate metadata table
- Only use JSON for truly dynamic data
- No constant serialization for common operations

### 3. **EF Core with SQLite** (If you want Microsoft support)
- Properly normalized schema
- No JSON at all
- Full LINQ support
- Microsoft-supported

## Quick Decision Matrix

| Need | LiteDB | EF Core | Hybrid SQLite | RavenDB |
|------|---------|----------|---------------|----------|
| No JSON serialization | ✅ | ✅ | Partial | ✅ |
| Simple API | ✅ | ❌ | ❌ | ✅ |
| LINQ queries | ✅ | ✅ | ❌ | ✅ |
| Single file | ✅ | ✅ | ✅ | ❌ |
| Partial updates | ✅ | ✅ | ✅ | ✅ |
| Microsoft supported | ❌ | ✅ | ✅ | ❌ |
| Production proven | ✅ | ✅ | ✅ | ✅ |

## Migration Path from Current SQLite+JSON

```csharp
// 1. Read from SQLite+JSON
var oldKnowledge = ReadFromSQLite();

// 2. Store in new system (example with LiteDB)
using (var db = new LiteDatabase(@"C:\source\.coa\knowledge\workspace.litedb"))
{
    var col = db.GetCollection<Knowledge>("knowledge");
    col.InsertBulk(oldKnowledge);
}

// 3. Update your service layer
public class KnowledgeService
{
    private readonly ILiteCollection<Knowledge> _knowledge;
    
    public KnowledgeService(LiteDatabase db)
    {
        _knowledge = db.GetCollection<Knowledge>("knowledge");
    }
    
    // No more JSON serialization!
    public async Task UpdatePriorityAsync(string id, string priority)
    {
        var doc = _knowledge.FindById(id);
        doc.SetMetadata("priority", priority);  // Direct object manipulation
        _knowledge.Update(doc);  // Only changed fields updated
    }
}
```

The constant JSON serialization/deserialization is definitely a problem that needs solving. LiteDB or a properly normalized EF Core model would eliminate this entirely.