using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Storage;

public class KnowledgeDatabase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<KnowledgeDatabase> _logger;
    private readonly IPathResolutionService _pathResolution;
    private readonly string _connectionString;
    
    public KnowledgeDatabase(
        IConfiguration configuration, 
        ILogger<KnowledgeDatabase> logger,
        IPathResolutionService pathResolution)
    {
        _configuration = configuration;
        _logger = logger;
        _pathResolution = pathResolution;
        
        var dbPath = configuration["ProjectKnowledge:Database:Path"] 
            ?? Path.Combine(_pathResolution.GetKnowledgePath(), "workspace.db");
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            _pathResolution.EnsureDirectoryExists(directory);
        }
        
        _connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared";
    }
    
    public async Task InitializeAsync()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            // Read and execute migration script
            var migrationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                "Storage", "Migrations", "001_InitialSchema.sql");
            
            if (File.Exists(migrationPath))
            {
                var sql = await File.ReadAllTextAsync(migrationPath);
                using var command = new SqliteCommand(sql, connection);
                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Database initialized successfully");
            }
            else
            {
                _logger.LogWarning("Migration script not found, using embedded schema");
                // Use embedded schema as fallback
                await ExecuteEmbeddedSchemaAsync(connection);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            throw;
        }
    }
    
    private async Task ExecuteEmbeddedSchemaAsync(SqliteConnection connection)
    {
        var commands = new[]
        {
            @"CREATE TABLE IF NOT EXISTS knowledge (
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
            )",
            @"CREATE TABLE IF NOT EXISTS relationships (
                from_id TEXT NOT NULL,
                to_id TEXT NOT NULL,
                relationship_type TEXT NOT NULL,
                metadata TEXT,
                created_at INTEGER NOT NULL,
                PRIMARY KEY (from_id, to_id, relationship_type),
                FOREIGN KEY (from_id) REFERENCES knowledge(id) ON DELETE CASCADE,
                FOREIGN KEY (to_id) REFERENCES knowledge(id) ON DELETE CASCADE
            )",
            "CREATE INDEX IF NOT EXISTS idx_knowledge_type ON knowledge(type)",
            "CREATE INDEX IF NOT EXISTS idx_knowledge_workspace ON knowledge(workspace)",
            "CREATE INDEX IF NOT EXISTS idx_knowledge_created ON knowledge(created_at DESC)"
        };
        
        foreach (var sql in commands)
        {
            using var command = new SqliteCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }
    
    public async Task<Knowledge> InsertKnowledgeAsync(Knowledge knowledge)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = @"
            INSERT INTO knowledge (
                id, type, content, code_snippets, metadata, workspace,
                created_at, modified_at, accessed_at, access_count, is_archived
            ) VALUES (
                @id, @type, @content, @codeSnippets, @metadata, @workspace,
                @createdAt, @modifiedAt, @accessedAt, @accessCount, @isArchived
            )";
        
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", knowledge.Id);
        command.Parameters.AddWithValue("@type", knowledge.Type);
        command.Parameters.AddWithValue("@content", knowledge.Content);
        command.Parameters.AddWithValue("@codeSnippets", 
            knowledge.CodeSnippets.Any() ? JsonSerializer.Serialize(knowledge.CodeSnippets) : DBNull.Value);
        command.Parameters.AddWithValue("@metadata", 
            knowledge.Metadata.Any() ? JsonSerializer.Serialize(knowledge.Metadata) : DBNull.Value);
        command.Parameters.AddWithValue("@workspace", knowledge.Workspace);
        command.Parameters.AddWithValue("@createdAt", 
            new DateTimeOffset(knowledge.CreatedAt).ToUnixTimeSeconds());
        command.Parameters.AddWithValue("@modifiedAt", 
            new DateTimeOffset(knowledge.ModifiedAt).ToUnixTimeSeconds());
        command.Parameters.AddWithValue("@accessedAt", 
            knowledge.AccessedAt.HasValue 
                ? new DateTimeOffset(knowledge.AccessedAt.Value).ToUnixTimeSeconds()
                : DBNull.Value);
        command.Parameters.AddWithValue("@accessCount", knowledge.AccessCount);
        command.Parameters.AddWithValue("@isArchived", knowledge.IsArchived);
        
        await command.ExecuteNonQueryAsync();
        return knowledge;
    }
    
    public async Task<Knowledge?> GetKnowledgeByIdAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = "SELECT * FROM knowledge WHERE id = @id";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapKnowledgeFromReader(reader);
        }
        
        return null;
    }
    
    public async Task<List<Knowledge>> SearchKnowledgeAsync(string query, string workspace, int maxResults)
    {
        var results = new List<Knowledge>();
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        // Try FTS search first if available
        try
        {
            var ftsSql = @"
                SELECT k.* FROM knowledge k
                INNER JOIN knowledge_fts f ON k.id = f.id
                WHERE f.knowledge_fts MATCH @query
                AND k.workspace = @workspace
                AND k.is_archived = 0
                ORDER BY k.modified_at DESC
                LIMIT @limit";
            
            using var ftsCommand = new SqliteCommand(ftsSql, connection);
            ftsCommand.Parameters.AddWithValue("@query", query);
            ftsCommand.Parameters.AddWithValue("@workspace", workspace);
            ftsCommand.Parameters.AddWithValue("@limit", maxResults);
            
            using var reader = await ftsCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapKnowledgeFromReader(reader));
            }
        }
        catch
        {
            // Fallback to LIKE search if FTS is not available
            var likeSql = @"
                SELECT * FROM knowledge
                WHERE (content LIKE @query OR type LIKE @query)
                AND workspace = @workspace
                AND is_archived = 0
                ORDER BY modified_at DESC
                LIMIT @limit";
            
            using var likeCommand = new SqliteCommand(likeSql, connection);
            likeCommand.Parameters.AddWithValue("@query", $"%{query}%");
            likeCommand.Parameters.AddWithValue("@workspace", workspace);
            likeCommand.Parameters.AddWithValue("@limit", maxResults);
            
            using var reader = await likeCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapKnowledgeFromReader(reader));
            }
        }
        
        return results;
    }
    
    public async Task UpdateKnowledgeAsync(Knowledge knowledge)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = @"
            UPDATE knowledge SET
                type = @type,
                content = @content,
                code_snippets = @codeSnippets,
                metadata = @metadata,
                modified_at = @modifiedAt
            WHERE id = @id";
        
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", knowledge.Id);
        command.Parameters.AddWithValue("@type", knowledge.Type);
        command.Parameters.AddWithValue("@content", knowledge.Content);
        command.Parameters.AddWithValue("@codeSnippets", 
            knowledge.CodeSnippets.Any() ? JsonSerializer.Serialize(knowledge.CodeSnippets) : DBNull.Value);
        command.Parameters.AddWithValue("@metadata", 
            knowledge.Metadata.Any() ? JsonSerializer.Serialize(knowledge.Metadata) : DBNull.Value);
        command.Parameters.AddWithValue("@modifiedAt", 
            new DateTimeOffset(knowledge.ModifiedAt).ToUnixTimeSeconds());
        
        await command.ExecuteNonQueryAsync();
    }
    
    public async Task UpdateAccessTrackingAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = @"
            UPDATE knowledge SET
                accessed_at = @accessedAt,
                access_count = access_count + 1
            WHERE id = @id";
        
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@accessedAt", 
            new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
        
        await command.ExecuteNonQueryAsync();
    }
    
    public async Task<bool> ArchiveKnowledgeAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = "UPDATE knowledge SET is_archived = 1 WHERE id = @id";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        
        var affected = await command.ExecuteNonQueryAsync();
        return affected > 0;
    }
    
    public async Task<KnowledgeStats> GetStatsAsync(string workspace)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var stats = new KnowledgeStats
        {
            Workspace = workspace
        };
        
        // Get counts
        var sql = @"
            SELECT 
                COUNT(*) as total,
                COUNT(CASE WHEN is_archived = 1 THEN 1 END) as archived,
                MIN(created_at) as oldest,
                MAX(created_at) as newest
            FROM knowledge
            WHERE workspace = @workspace";
        
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@workspace", workspace);
        
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            stats.TotalCount = reader.GetInt32(0);
            stats.ArchivedCount = reader.GetInt32(1);
            
            if (!reader.IsDBNull(2))
            {
                stats.OldestEntry = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(2)).UtcDateTime;
            }
            if (!reader.IsDBNull(3))
            {
                stats.NewestEntry = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)).UtcDateTime;
            }
        }
        
        // Get counts by type
        var typeSql = @"
            SELECT type, COUNT(*) as count
            FROM knowledge
            WHERE workspace = @workspace AND is_archived = 0
            GROUP BY type";
        
        using var typeCommand = new SqliteCommand(typeSql, connection);
        typeCommand.Parameters.AddWithValue("@workspace", workspace);
        
        using var typeReader = await typeCommand.ExecuteReaderAsync();
        while (await typeReader.ReadAsync())
        {
            stats.CountByType[typeReader.GetString(0)] = typeReader.GetInt32(1);
        }
        
        // Get most accessed
        var accessSql = @"
            SELECT id FROM knowledge
            WHERE workspace = @workspace AND is_archived = 0
            ORDER BY access_count DESC
            LIMIT 10";
        
        using var accessCommand = new SqliteCommand(accessSql, connection);
        accessCommand.Parameters.AddWithValue("@workspace", workspace);
        
        using var accessReader = await accessCommand.ExecuteReaderAsync();
        while (await accessReader.ReadAsync())
        {
            stats.MostAccessedIds.Add(accessReader.GetString(0));
        }
        
        return stats;
    }
    
    private Knowledge MapKnowledgeFromReader(SqliteDataReader reader)
    {
        var type = reader.GetString(reader.GetOrdinal("type"));
        
        // Create the specific type based on the type field
        Knowledge knowledge = type switch
        {
            KnowledgeTypes.Checkpoint => new Checkpoint(),
            KnowledgeTypes.Checklist => new Checklist(),
            _ => new Knowledge()
        };
        
        // Set common properties
        knowledge.Id = reader.GetString(reader.GetOrdinal("id"));
        knowledge.Type = type;
        knowledge.Content = reader.GetString(reader.GetOrdinal("content"));
        knowledge.Workspace = reader.GetString(reader.GetOrdinal("workspace"));
        knowledge.CreatedAt = DateTimeOffset.FromUnixTimeSeconds(
            reader.GetInt64(reader.GetOrdinal("created_at"))).UtcDateTime;
        knowledge.ModifiedAt = DateTimeOffset.FromUnixTimeSeconds(
            reader.GetInt64(reader.GetOrdinal("modified_at"))).UtcDateTime;
        knowledge.AccessCount = reader.GetInt32(reader.GetOrdinal("access_count"));
        knowledge.IsArchived = reader.GetBoolean(reader.GetOrdinal("is_archived"));
        
        // Parse optional fields
        var accessedAtOrdinal = reader.GetOrdinal("accessed_at");
        if (!reader.IsDBNull(accessedAtOrdinal))
        {
            knowledge.AccessedAt = DateTimeOffset.FromUnixTimeSeconds(
                reader.GetInt64(accessedAtOrdinal)).UtcDateTime;
        }
        
        var codeSnippetsOrdinal = reader.GetOrdinal("code_snippets");
        if (!reader.IsDBNull(codeSnippetsOrdinal))
        {
            var json = reader.GetString(codeSnippetsOrdinal);
            knowledge.CodeSnippets = JsonSerializer.Deserialize<List<CodeSnippet>>(json) ?? new();
        }
        
        var metadataOrdinal = reader.GetOrdinal("metadata");
        if (!reader.IsDBNull(metadataOrdinal))
        {
            var json = reader.GetString(metadataOrdinal);
            knowledge.Metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new();
        }
        
        return knowledge;
    }
    
    public async Task InsertRelationshipAsync(Relationship relationship)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = @"
            INSERT OR REPLACE INTO relationships (
                from_id, to_id, relationship_type, metadata, created_at
            ) VALUES (
                @fromId, @toId, @relationshipType, @metadata, @createdAt
            )";
        
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@fromId", relationship.FromId);
        command.Parameters.AddWithValue("@toId", relationship.ToId);
        command.Parameters.AddWithValue("@relationshipType", relationship.RelationshipType);
        command.Parameters.AddWithValue("@metadata", 
            relationship.Metadata.Any() ? JsonSerializer.Serialize(relationship.Metadata) : DBNull.Value);
        command.Parameters.AddWithValue("@createdAt", 
            new DateTimeOffset(relationship.CreatedAt).ToUnixTimeSeconds());
        
        await command.ExecuteNonQueryAsync();
    }
    
    public async Task<List<Relationship>> GetRelationshipsAsync(string knowledgeId, RelationshipDirection direction)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        string sql = direction switch
        {
            RelationshipDirection.From => "SELECT * FROM relationships WHERE from_id = @id",
            RelationshipDirection.To => "SELECT * FROM relationships WHERE to_id = @id",
            _ => "SELECT * FROM relationships WHERE from_id = @id OR to_id = @id"
        };
        
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", knowledgeId);
        
        var relationships = new List<Relationship>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var relationship = new Relationship
            {
                FromId = reader.GetString(reader.GetOrdinal("from_id")),
                ToId = reader.GetString(reader.GetOrdinal("to_id")),
                RelationshipType = reader.GetString(reader.GetOrdinal("relationship_type")),
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(
                    reader.GetInt64(reader.GetOrdinal("created_at"))).UtcDateTime
            };
            
            var metadataOrdinal = reader.GetOrdinal("metadata");
            if (!reader.IsDBNull(metadataOrdinal))
            {
                var json = reader.GetString(metadataOrdinal);
                relationship.Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
            }
            
            relationships.Add(relationship);
        }
        
        return relationships;
    }
    
    public async Task<bool> DeleteRelationshipAsync(string fromId, string toId, string relationshipType)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = @"
            DELETE FROM relationships 
            WHERE from_id = @fromId 
            AND to_id = @toId 
            AND relationship_type = @relationshipType";
        
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@fromId", fromId);
        command.Parameters.AddWithValue("@toId", toId);
        command.Parameters.AddWithValue("@relationshipType", relationshipType);
        
        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }
}

public class KnowledgeStats
{
    public int TotalCount { get; set; }
    public int TotalItems => TotalCount; // Alias for compatibility
    public Dictionary<string, int> CountByType { get; set; } = new();
    public int ArchivedCount { get; set; }
    public DateTime? OldestEntry { get; set; }
    public DateTime? NewestEntry { get; set; }
    public List<string> MostAccessedIds { get; set; } = new();
    public string Workspace { get; set; } = string.Empty;
}