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