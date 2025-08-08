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