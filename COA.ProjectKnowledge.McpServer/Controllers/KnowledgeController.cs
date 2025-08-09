using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace COA.ProjectKnowledge.McpServer.Controllers;

[ApiController]
[Route("api/knowledge")]
public class KnowledgeController : ControllerBase
{
    private readonly KnowledgeService _knowledgeService;
    private readonly FederationService _federationService;
    private readonly ILogger<KnowledgeController> _logger;
    
    public KnowledgeController(
        KnowledgeService knowledgeService,
        FederationService federationService,
        ILogger<KnowledgeController> logger)
    {
        _knowledgeService = knowledgeService;
        _federationService = federationService;
        _logger = logger;
    }
    
    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        try
        {
            var stats = await _knowledgeService.GetStatsAsync();
            return Ok(new HealthCheckResponse
            {
                Status = "Healthy",
                Version = "1.0.0",
                KnowledgeCount = stats.TotalItems,
                Workspace = stats.Workspace
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(500, new { Status = "Unhealthy", Error = ex.Message });
        }
    }
    
    /// <summary>
    /// Search for knowledge within current workspace
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int maxResults = 50)
    {
        try
        {
            var request = new SearchKnowledgeRequest
            {
                Query = query,
                MaxResults = maxResults
            };
            var results = await _knowledgeService.SearchKnowledgeAsync(request);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for query: {Query}", query);
            return StatusCode(500, new { Error = "Search failed", Message = ex.Message });
        }
    }
    
    /// <summary>
    /// Search for knowledge across multiple workspaces/projects
    /// </summary>
    [HttpGet("search/cross-project")]
    public async Task<IActionResult> CrossProjectSearch(
        [FromQuery] string query, 
        [FromQuery] string[]? workspaces = null, 
        [FromQuery] int maxResults = 50)
    {
        try
        {
            var request = new CrossWorkspaceSearchRequest
            {
                Query = query,
                Workspaces = workspaces,
                MaxResults = maxResults
            };
            var results = await _knowledgeService.SearchAcrossWorkspacesAsync(request);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cross-project search failed for query: {Query}", query);
            return StatusCode(500, new { Error = "Cross-project search failed", Message = ex.Message });
        }
    }
    
    /// <summary>
    /// Get list of available workspaces/projects
    /// </summary>
    [HttpGet("workspaces")]
    public async Task<IActionResult> GetWorkspaces()
    {
        try
        {
            var workspaces = await _knowledgeService.GetAvailableWorkspacesAsync();
            return Ok(new { Workspaces = workspaces });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available workspaces");
            return StatusCode(500, new { Error = "Failed to get workspaces", Message = ex.Message });
        }
    }
    
    /// <summary>
    /// Get knowledge by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        try
        {
            var response = await _knowledgeService.GetKnowledgeAsync(id);
            if (!response.Success || response.Knowledge == null)
            {
                return NotFound(new { Error = "Knowledge not found", Id = id });
            }
            return Ok(response.Knowledge);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get knowledge: {Id}", id);
            return StatusCode(500, new { Error = "Failed to retrieve knowledge", Message = ex.Message });
        }
    }
    
    /// <summary>
    /// Store new knowledge (supports federation from MCP clients)
    /// </summary>
    [HttpPost("store")]
    public async Task<IActionResult> Store([FromBody] StoreKnowledgeRequest request)
    {
        try
        {
            // Validate API key if required
            if (!await ValidateApiKeyAsync())
            {
                return Unauthorized(new { Error = "Invalid or missing API key" });
            }
            
            // Check if this is from a federation client (has source in metadata)
            var clientSource = request.Metadata?.GetValueOrDefault("source");
            var clientWorkspace = request.Metadata?.GetValueOrDefault("workspace");
            StoreKnowledgeResponse response;
            
            if (!string.IsNullOrEmpty(clientSource))
            {
                // Use federation service for client requests with workspace
                response = await _federationService.StoreFromClientAsync(request, clientSource, clientWorkspace);
            }
            else
            {
                // Direct storage for internal requests
                response = await _knowledgeService.StoreKnowledgeAsync(request);
            }
            
            if (!response.Success)
            {
                return StatusCode(500, new { Error = "Failed to store knowledge", Message = response.Error });
            }
            
            return CreatedAtAction(nameof(GetById), new { id = response.KnowledgeId }, new { Id = response.KnowledgeId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store knowledge");
            return StatusCode(500, new { Error = "Failed to store knowledge", Message = ex.Message });
        }
    }

    /// <summary>
    /// Batch store multiple knowledge items (federation support)
    /// </summary>
    [HttpPost("batch")]
    public async Task<IActionResult> BatchStore([FromBody] List<StoreKnowledgeRequest> requests)
    {
        try
        {
            // Validate API key if required
            if (!await ValidateApiKeyAsync())
            {
                return Unauthorized(new { Error = "Invalid or missing API key" });
            }

            var results = new List<object>();
            
            foreach (var request in requests)
            {
                try
                {
                    // Check if this is from a federation client
                    var clientSource = request.Metadata?.GetValueOrDefault("source");
                    var clientWorkspace = request.Metadata?.GetValueOrDefault("workspace");
                    StoreKnowledgeResponse response;
                    
                    if (!string.IsNullOrEmpty(clientSource))
                    {
                        response = await _federationService.StoreFromClientAsync(request, clientSource, clientWorkspace);
                    }
                    else
                    {
                        response = await _knowledgeService.StoreKnowledgeAsync(request);
                    }
                    
                    results.Add(new { success = true, id = response.KnowledgeId, message = response.Message });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to store knowledge item in batch");
                    results.Add(new { success = false, error = ex.Message });
                }
            }
            
            return Ok(new { results, total = requests.Count, successful = results.Count(r => (bool)((dynamic)r).success) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process batch store");
            return StatusCode(500, new { Error = "Batch store failed", Message = ex.Message });
        }
    }

    /// <summary>
    /// Contribute knowledge from external systems (simplified federation endpoint)
    /// </summary>
    [HttpPost("contribute")]
    public async Task<IActionResult> ContributeExternal([FromBody] ExternalContribution contribution)
    {
        try
        {
            // Validate API key if required
            if (!await ValidateApiKeyAsync())
            {
                return Unauthorized(new { Error = "Invalid or missing API key" });
            }
            
            var request = new StoreKnowledgeRequest
            {
                Type = contribution.Type,
                Content = contribution.Content,
                Metadata = new Dictionary<string, string>
                {
                    ["source"] = contribution.SourceId,
                    ["projectName"] = contribution.ProjectName,
                    ["contributedAt"] = DateTime.UtcNow.ToString("O")
                }
            };
            
            var response = await _federationService.StoreFromClientAsync(request, contribution.SourceId, contribution.ProjectName);
            
            return Ok(new
            {
                success = response.Success,
                id = response.KnowledgeId,
                message = $"Contribution accepted from {contribution.SourceId}",
                error = response.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process external contribution");
            return StatusCode(500, new { Error = "Contribution failed", Message = ex.Message });
        }
    }
    
    /// <summary>
    /// Get federation statistics (central hub info)
    /// </summary>
    [HttpGet("federation/stats")]
    public async Task<IActionResult> GetFederationStats()
    {
        try
        {
            var stats = await _federationService.GetFederationStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get federation stats");
            return StatusCode(500, new { Error = "Failed to get federation stats", Message = ex.Message });
        }
    }
    
    /// <summary>
    /// Get workspace statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var stats = await _knowledgeService.GetStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get statistics");
            return StatusCode(500, new { Error = "Failed to get statistics", Message = ex.Message });
        }
    }
    
    private Task<bool> ValidateApiKeyAsync()
    {
        var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var requireApiKey = configuration.GetValue<bool>("ProjectKnowledge:Federation:RequireApiKey");
        
        if (!requireApiKey)
        {
            return Task.FromResult(true);
        }
        
        if (!Request.Headers.TryGetValue("X-API-Key", out var apiKey))
        {
            return Task.FromResult(false);
        }
        
        var validApiKey = configuration["ProjectKnowledge:Federation:ApiKey"];
        return Task.FromResult(!string.IsNullOrEmpty(validApiKey) && apiKey == validApiKey);
    }
}