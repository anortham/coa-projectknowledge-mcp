using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Data.Entities;
using COA.ProjectKnowledge.McpServer.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Services;

public class CheckpointServiceEF
{
    private readonly KnowledgeDbContext _context;
    private readonly IWorkspaceResolver _workspaceResolver;

    public CheckpointServiceEF(KnowledgeDbContext context, IWorkspaceResolver workspaceResolver)
    {
        _context = context;
        _workspaceResolver = workspaceResolver;
    }

    public async Task<Checkpoint> CreateCheckpointAsync(string content, string? sessionId = null, List<string>? activeFiles = null)
    {
        var workspace = _workspaceResolver.GetCurrentWorkspace();
        sessionId ??= Guid.NewGuid().ToString();
        
        var checkpoint = new Checkpoint
        {
            Id = GenerateChronologicalId(),
            SessionId = sessionId,
            Content = content,
            ActiveFiles = activeFiles ?? new List<string>(),
            CreatedAt = DateTime.UtcNow,
            Workspace = workspace,
            AccessCount = 0
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
            UpdatedAt = checkpoint.CreatedAt,
            AccessedAt = checkpoint.CreatedAt,
            AccessCount = checkpoint.AccessCount
        };

        _context.Knowledge.Add(entity);
        await _context.SaveChangesAsync();

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
            query = query.OrderByDescending(k => k.CreatedAt);
        }
        else
        {
            query = query.OrderByDescending(k => k.CreatedAt);
        }

        var entity = await query.FirstOrDefaultAsync();
        if (entity == null) return null;

        // Update access tracking
        entity.AccessedAt = DateTime.UtcNow;
        entity.AccessCount++;
        await _context.SaveChangesAsync();

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
            .OrderByDescending(k => k.CreatedAt)
            .Take(maxResults)
            .ToListAsync();

        return entities.Select(ConvertToCheckpoint).ToList();
    }

    private Checkpoint ConvertToCheckpoint(KnowledgeEntity entity)
    {
        var metadata = !string.IsNullOrEmpty(entity.Metadata) 
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entity.Metadata)
            : new Dictionary<string, JsonElement>();

        return new Checkpoint
        {
            Id = entity.Id,
            SessionId = metadata.TryGetValue("SessionId", out var sessionId) 
                ? sessionId.GetString() ?? string.Empty 
                : string.Empty,
            Content = entity.Content,
            ActiveFiles = metadata.TryGetValue("ActiveFiles", out var activeFiles) 
                ? JsonSerializer.Deserialize<List<string>>(activeFiles.GetRawText()) ?? new List<string>()
                : new List<string>(),
            CreatedAt = entity.CreatedAt,
            Workspace = entity.Workspace,
            AccessCount = entity.AccessCount
        };
    }

    private string GenerateChronologicalId()
    {
        var timestamp = DateTime.UtcNow;
        var random = Random.Shared.Next(1000, 9999);
        return $"{timestamp:yyyyMMddHHmmssfff}-{random}";
    }
}