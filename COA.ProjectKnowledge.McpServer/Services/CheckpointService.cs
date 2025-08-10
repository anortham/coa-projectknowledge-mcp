using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Data.Entities;
using COA.ProjectKnowledge.McpServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Services;

public class CheckpointService
{
    private readonly KnowledgeDbContext _context;
    private readonly IWorkspaceResolver _workspaceResolver;
    private readonly RealTimeNotificationService _notificationService;
    private readonly ILogger<CheckpointService> _logger;

    public CheckpointService(KnowledgeDbContext context, IWorkspaceResolver workspaceResolver, RealTimeNotificationService notificationService, ILogger<CheckpointService> logger)
    {
        _context = context;
        _workspaceResolver = workspaceResolver;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Checkpoint> CreateCheckpointAsync(string content, string? sessionId = null, List<string>? activeFiles = null)
    {
        var workspace = _workspaceResolver.GetCurrentWorkspace();
        sessionId ??= Guid.NewGuid().ToString();
        
        var checkpoint = new Checkpoint
        {
            Content = content,
            SessionId = sessionId,
            ActiveFiles = (activeFiles ?? new List<string>()).ToArray(),
            Workspace = workspace
        };

        var entity = new KnowledgeEntity
        {
            Id = checkpoint.Id,
            Type = KnowledgeTypes.Checkpoint,
            Content = checkpoint.Content,
            Metadata = JsonSerializer.Serialize(new
            {
                checkpoint.SessionId,
                checkpoint.ActiveFiles
            }),
            Tags = JsonSerializer.Serialize(new List<string> { "checkpoint", sessionId }),
            Priority = "normal",
            Status = "active",
            Workspace = checkpoint.Workspace,
            CreatedAt = checkpoint.CreatedAt,
            ModifiedAt = checkpoint.CreatedAt,
            AccessedAt = checkpoint.CreatedAt,
            AccessCount = checkpoint.AccessCount
        };

        _context.Knowledge.Add(entity);
        await _context.SaveChangesAsync();

        // Broadcast real-time notification for new checkpoint
        _ = Task.Run(async () =>
        {
            try
            {
                await _notificationService.BroadcastCheckpointCreatedAsync(checkpoint.Id, checkpoint.SessionId, checkpoint.Workspace);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast checkpoint creation notification for checkpoint {CheckpointId} in session {SessionId}", checkpoint.Id, checkpoint.SessionId);
                // The checkpoint was saved successfully, notification failure shouldn't affect the main operation
            }
        });

        return checkpoint;
    }

    public async Task<Checkpoint?> GetCheckpointAsync(string? checkpointId = null, string? sessionId = null)
    {
        var workspace = _workspaceResolver.GetCurrentWorkspace();
        
        IQueryable<KnowledgeEntity> query = _context.Knowledge
            .Where(k => k.Type == KnowledgeTypes.Checkpoint && k.Workspace == workspace);

        if (!string.IsNullOrEmpty(checkpointId))
        {
            query = query.Where(k => k.Id == checkpointId);
        }
        else if (!string.IsNullOrEmpty(sessionId))
        {
            query = query.Where(k => k.Metadata != null && k.Metadata.Contains($"\"SessionId\":\"{sessionId}\""));
            query = query.OrderByDescending(k => k.Id); // Use chronological ID for natural sorting
        }
        else
        {
            query = query.OrderByDescending(k => k.Id); // Use chronological ID for natural sorting
        }

        var entity = await query.FirstOrDefaultAsync();
        if (entity == null) return null;

        // Update access tracking efficiently
        var now = DateTime.UtcNow;
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Knowledge SET AccessedAt = @now, AccessCount = AccessCount + 1 WHERE Id = @id",
            new Microsoft.Data.Sqlite.SqliteParameter("@now", now),
            new Microsoft.Data.Sqlite.SqliteParameter("@id", entity.Id));

        return ConvertToCheckpoint(entity);
    }

    public async Task<List<Checkpoint>> ListCheckpointsAsync(string? sessionId = null, int maxResults = 20)
    {
        var workspace = _workspaceResolver.GetCurrentWorkspace();
        
        IQueryable<KnowledgeEntity> query = _context.Knowledge
            .Where(k => k.Type == KnowledgeTypes.Checkpoint && k.Workspace == workspace);

        if (!string.IsNullOrEmpty(sessionId))
        {
            query = query.Where(k => k.Metadata != null && k.Metadata.Contains($"\"SessionId\":\"{sessionId}\""));
        }

        var entities = await query
            .OrderByDescending(k => k.Id) // Use chronological ID for natural sorting
            .Take(maxResults)
            .ToListAsync();

        return entities.Select(ConvertToCheckpoint).ToList();
    }

    private Checkpoint ConvertToCheckpoint(KnowledgeEntity entity)
    {
        var metadata = !string.IsNullOrEmpty(entity.Metadata) 
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entity.Metadata) ?? new Dictionary<string, JsonElement>()
            : new Dictionary<string, JsonElement>();

        return new Checkpoint
        {
            Id = entity.Id,
            SessionId = metadata.TryGetValue("SessionId", out var sessionId) 
                ? sessionId.GetString() ?? string.Empty 
                : string.Empty,
            Content = entity.Content,
            ActiveFiles = metadata.TryGetValue("ActiveFiles", out var activeFiles) 
                ? JsonSerializer.Deserialize<string[]>(activeFiles.GetRawText()) ?? Array.Empty<string>()
                : Array.Empty<string>(),
            CreatedAt = entity.CreatedAt,
            Workspace = entity.Workspace ?? string.Empty,
            AccessCount = entity.AccessCount
        };
    }

    // Removed duplicate method - use ChronologicalId.Generate() instead
    
}