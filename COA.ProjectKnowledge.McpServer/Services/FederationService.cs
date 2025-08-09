using COA.ProjectKnowledge.McpServer.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Services;

/// <summary>
/// Central hub federation service for managing knowledge from multiple MCP clients
/// </summary>
public class FederationService
{
    private readonly KnowledgeService _knowledgeService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FederationService> _logger;
    private readonly List<string> _allowedSources;
    
    public FederationService(
        KnowledgeService knowledgeService,
        IConfiguration configuration,
        ILogger<FederationService> logger)
    {
        _knowledgeService = knowledgeService;
        _configuration = configuration;
        _logger = logger;
        
        _allowedSources = LoadAllowedSources();
    }
    
    private List<string> LoadAllowedSources()
    {
        var sources = _configuration.GetSection("ProjectKnowledge:Federation:ClientSources")
            .Get<List<string>>() ?? new List<string>();
            
        _logger.LogInformation("Loaded {Count} allowed federation sources", sources.Count);
        return sources;
    }
    
    /// <summary>
    /// Store knowledge from a federated client with source tracking
    /// </summary>
    public async Task<StoreKnowledgeResponse> StoreFromClientAsync(
        StoreKnowledgeRequest request, 
        string clientSource,
        string? workspace = null)
    {
        // Validate source if configured
        if (_allowedSources.Any() && !_allowedSources.Contains(clientSource))
        {
            _logger.LogWarning("Rejected knowledge from unauthorized source: {Source}", clientSource);
            return new StoreKnowledgeResponse
            {
                Success = false,
                Error = $"Unauthorized source: {clientSource}"
            };
        }
        
        // Add source metadata
        var metadata = new Dictionary<string, object>
        {
            ["source"] = clientSource,
            ["receivedAt"] = DateTime.UtcNow
        };
        
        // Preserve existing metadata if any
        if (request.Metadata != null)
        {
            foreach (var kvp in request.Metadata)
            {
                metadata[kvp.Key] = kvp.Value; // Proper key-value handling
            }
        }
        
        // Create modified request with source tracking and workspace
        var federatedRequest = new StoreKnowledgeRequest
        {
            Type = request.Type,
            Content = request.Content,
            Tags = request.Tags,
            Status = request.Status,
            Priority = request.Priority,
            CodeSnippets = request.CodeSnippets,
            RelatedTo = request.RelatedTo,
            Workspace = workspace // Set the workspace from federation client
        };
        
        var result = await _knowledgeService.StoreKnowledgeAsync(federatedRequest);
        
        if (result.Success)
        {
            _logger.LogInformation("Stored federated knowledge from {Source}: {Type} - {Content}", 
                clientSource, request.Type, request.Content.Substring(0, Math.Min(50, request.Content.Length)));
        }
        else
        {
            _logger.LogWarning("Failed to store federated knowledge from {Source}: {Error}", 
                clientSource, result.Error);
        }
        
        return result;
    }
    
    /// <summary>
    /// Search knowledge with optional source filtering
    /// </summary>
    public async Task<SearchKnowledgeResponse> SearchFromClientAsync(
        SearchKnowledgeRequest request,
        string clientSource,
        bool includeSourceMetadata = false)
    {
        var result = await _knowledgeService.SearchKnowledgeAsync(request);
        
        // Optionally filter results or add source information
        if (includeSourceMetadata && result.Success && result.Items != null)
        {
            // Note: This would require extending the search result model to include metadata
            _logger.LogDebug("Search request from {Source} returned {Count} results", 
                clientSource, result.Items.Count);
        }
        
        return result;
    }
    
    /// <summary>
    /// Get statistics about federated knowledge sources
    /// </summary>
    public async Task<FederationStats> GetFederationStatsAsync()
    {
        var stats = await _knowledgeService.GetStatsAsync();
        
        // TODO: Enhance with source-specific statistics once metadata tracking is improved
        return new FederationStats
        {
            TotalKnowledge = stats.TotalItems,
            AllowedSources = _allowedSources,
            LastUpdated = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Health check for federation service
    /// </summary>
    public Task<HealthCheckResponse> GetHealthAsync()
    {
        return Task.FromResult(new HealthCheckResponse
        {
            Status = "Healthy",
            Version = "1.0.0",
            KnowledgeCount = 0, // Will be filled by controller
            Workspace = "Central Hub"
        });
    }
}

// Federation models for central hub
public class FederationStats
{
    public int TotalKnowledge { get; set; }
    public List<string> AllowedSources { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

public class HealthCheckResponse
{
    public string Status { get; set; } = "Healthy";
    public string Version { get; set; } = "1.0.0";
    public int KnowledgeCount { get; set; }
    public string Workspace { get; set; } = string.Empty;
}