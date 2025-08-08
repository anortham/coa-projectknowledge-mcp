using COA.ProjectKnowledge.McpServer.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Services;

/// <summary>
/// Service for federating knowledge across multiple workspaces
/// </summary>
public class FederationService
{
    private readonly KnowledgeService _knowledgeService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FederationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly List<FederationPeer> _peers = new();
    
    public FederationService(
        KnowledgeService knowledgeService,
        IConfiguration configuration,
        ILogger<FederationService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _knowledgeService = knowledgeService;
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("Federation");
        
        LoadPeersFromConfiguration();
    }
    
    private void LoadPeersFromConfiguration()
    {
        var peersSection = _configuration.GetSection("ProjectKnowledge:Federation:Peers");
        if (peersSection.Exists())
        {
            var peers = peersSection.Get<List<FederationPeer>>();
            if (peers != null)
            {
                _peers.AddRange(peers);
                _logger.LogInformation("Loaded {Count} federation peers", _peers.Count);
            }
        }
    }
    
    /// <summary>
    /// Search for knowledge across all federated workspaces
    /// </summary>
    public async Task<FederatedSearchResult> SearchFederatedAsync(string query, int maxResultsPerPeer = 10)
    {
        var result = new FederatedSearchResult
        {
            Query = query,
            Timestamp = DateTime.UtcNow
        };
        
        // Search local workspace first
        try
        {
            var localResults = await _knowledgeService.SearchAsync(query, maxResults: maxResultsPerPeer);
            result.Results.Add(new WorkspaceSearchResult
            {
                WorkspaceId = "local",
                WorkspaceName = "Local Workspace",
                Items = localResults,
                IsLocal = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching local workspace");
        }
        
        // Search remote peers in parallel
        var searchTasks = _peers.Select(peer => SearchPeerAsync(peer, query, maxResultsPerPeer));
        var peerResults = await Task.WhenAll(searchTasks);
        
        result.Results.AddRange(peerResults.Where(r => r != null)!);
        result.TotalResults = result.Results.Sum(r => r.Items.Count);
        
        return result;
    }
    
    private async Task<WorkspaceSearchResult?> SearchPeerAsync(FederationPeer peer, string query, int maxResults)
    {
        try
        {
            var url = $"{peer.BaseUrl}/api/knowledge/search?query={Uri.EscapeDataString(query)}&maxResults={maxResults}";
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(peer.ApiKey))
            {
                request.Headers.Add("X-API-Key", peer.ApiKey);
            }
            
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var items = await response.Content.ReadFromJsonAsync<List<Knowledge>>();
                return new WorkspaceSearchResult
                {
                    WorkspaceId = peer.WorkspaceId,
                    WorkspaceName = peer.Name,
                    Items = items ?? new List<Knowledge>(),
                    IsLocal = false
                };
            }
            
            _logger.LogWarning("Failed to search peer {Name}: {Status}", peer.Name, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching peer {Name}", peer.Name);
        }
        
        return null;
    }
    
    /// <summary>
    /// Share knowledge with federated peers
    /// </summary>
    public async Task<ShareResult> ShareKnowledgeAsync(string knowledgeId, List<string>? peerIds = null)
    {
        var knowledge = await _knowledgeService.GetByIdAsync(knowledgeId);
        if (knowledge == null)
        {
            return new ShareResult
            {
                Success = false,
                Message = "Knowledge not found"
            };
        }
        
        var targetPeers = peerIds != null 
            ? _peers.Where(p => peerIds.Contains(p.WorkspaceId)).ToList()
            : _peers;
        
        var shareTasks = targetPeers.Select(peer => ShareWithPeerAsync(peer, knowledge));
        var results = await Task.WhenAll(shareTasks);
        
        return new ShareResult
        {
            Success = results.All(r => r),
            SharedWith = results.Count(r => r),
            TotalPeers = targetPeers.Count,
            Message = $"Shared with {results.Count(r => r)} of {targetPeers.Count} peers"
        };
    }
    
    private async Task<bool> ShareWithPeerAsync(FederationPeer peer, Knowledge knowledge)
    {
        try
        {
            var url = $"{peer.BaseUrl}/api/knowledge";
            
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(knowledge)
            };
            
            if (!string.IsNullOrEmpty(peer.ApiKey))
            {
                request.Headers.Add("X-API-Key", peer.ApiKey);
            }
            
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully shared knowledge {Id} with {Peer}", 
                    knowledge.Id, peer.Name);
                return true;
            }
            
            _logger.LogWarning("Failed to share with peer {Name}: {Status}", 
                peer.Name, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing with peer {Name}", peer.Name);
        }
        
        return false;
    }
    
    /// <summary>
    /// Get health status of all federation peers
    /// </summary>
    public async Task<List<PeerHealthStatus>> GetPeerHealthAsync()
    {
        var healthChecks = _peers.Select(CheckPeerHealthAsync);
        return (await Task.WhenAll(healthChecks)).ToList();
    }
    
    private async Task<PeerHealthStatus> CheckPeerHealthAsync(FederationPeer peer)
    {
        var status = new PeerHealthStatus
        {
            WorkspaceId = peer.WorkspaceId,
            Name = peer.Name,
            CheckedAt = DateTime.UtcNow
        };
        
        try
        {
            var url = $"{peer.BaseUrl}/api/knowledge/health";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            if (!string.IsNullOrEmpty(peer.ApiKey))
            {
                request.Headers.Add("X-API-Key", peer.ApiKey);
            }
            
            var response = await _httpClient.SendAsync(request);
            status.IsHealthy = response.IsSuccessStatusCode;
            status.ResponseTime = (int)response.Headers.Age?.TotalMilliseconds!;
            
            if (response.IsSuccessStatusCode)
            {
                var health = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
                status.Version = health?.Version;
                status.KnowledgeCount = health?.KnowledgeCount ?? 0;
            }
        }
        catch (Exception ex)
        {
            status.IsHealthy = false;
            status.Error = ex.Message;
        }
        
        return status;
    }
}

// Federation Models
public class FederationPeer
{
    public string WorkspaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool Enabled { get; set; } = true;
}

public class FederatedSearchResult
{
    public string Query { get; set; } = string.Empty;
    public List<WorkspaceSearchResult> Results { get; set; } = new();
    public int TotalResults { get; set; }
    public DateTime Timestamp { get; set; }
}

public class WorkspaceSearchResult
{
    public string WorkspaceId { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public List<Knowledge> Items { get; set; } = new();
    public bool IsLocal { get; set; }
}

public class ShareResult
{
    public bool Success { get; set; }
    public int SharedWith { get; set; }
    public int TotalPeers { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class PeerHealthStatus
{
    public string WorkspaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public string? Version { get; set; }
    public int? KnowledgeCount { get; set; }
    public int? ResponseTime { get; set; }
    public string? Error { get; set; }
    public DateTime CheckedAt { get; set; }
}

public class HealthCheckResponse
{
    public string Status { get; set; } = "Healthy";
    public string Version { get; set; } = "1.0.0";
    public int KnowledgeCount { get; set; }
    public string Workspace { get; set; } = string.Empty;
}