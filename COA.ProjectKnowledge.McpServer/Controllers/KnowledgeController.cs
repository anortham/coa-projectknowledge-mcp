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
    /// Search for knowledge
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int maxResults = 50)
    {
        try
        {
            var results = await _knowledgeService.SearchAsync(query, maxResults: maxResults);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for query: {Query}", query);
            return StatusCode(500, new { Error = "Search failed", Message = ex.Message });
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
            var knowledge = await _knowledgeService.GetByIdAsync(id);
            if (knowledge == null)
            {
                return NotFound(new { Error = "Knowledge not found", Id = id });
            }
            return Ok(knowledge);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get knowledge: {Id}", id);
            return StatusCode(500, new { Error = "Failed to retrieve knowledge", Message = ex.Message });
        }
    }
    
    /// <summary>
    /// Store new knowledge (for federation)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Store([FromBody] Knowledge knowledge)
    {
        try
        {
            // Validate API key if required
            if (!await ValidateApiKeyAsync())
            {
                return Unauthorized(new { Error = "Invalid or missing API key" });
            }
            
            var stored = await _knowledgeService.StoreAsync(knowledge);
            return CreatedAtAction(nameof(GetById), new { id = stored.Id }, stored);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store knowledge");
            return StatusCode(500, new { Error = "Failed to store knowledge", Message = ex.Message });
        }
    }
    
    /// <summary>
    /// Federated search across workspaces
    /// </summary>
    [HttpGet("federated/search")]
    public async Task<IActionResult> FederatedSearch([FromQuery] string query, [FromQuery] int maxResultsPerPeer = 10)
    {
        try
        {
            var results = await _federationService.SearchFederatedAsync(query, maxResultsPerPeer);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Federated search failed for query: {Query}", query);
            return StatusCode(500, new { Error = "Federated search failed", Message = ex.Message });
        }
    }
    
    /// <summary>
    /// Share knowledge with federation peers
    /// </summary>
    [HttpPost("{id}/share")]
    public async Task<IActionResult> Share(string id, [FromBody] List<string>? peerIds = null)
    {
        try
        {
            var result = await _federationService.ShareKnowledgeAsync(id, peerIds);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to share knowledge: {Id}", id);
            return StatusCode(500, new { Error = "Failed to share knowledge", Message = ex.Message });
        }
    }
    
    /// <summary>
    /// Get federation peer health status
    /// </summary>
    [HttpGet("federation/health")]
    public async Task<IActionResult> GetPeerHealth()
    {
        try
        {
            var health = await _federationService.GetPeerHealthAsync();
            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get peer health");
            return StatusCode(500, new { Error = "Failed to get peer health", Message = ex.Message });
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