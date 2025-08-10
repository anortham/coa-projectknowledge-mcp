using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Protocol;
using COA.ProjectKnowledge.McpServer.Services;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Resources;

/// <summary>
/// Provides URI-addressable resources for large knowledge data sets.
/// Handles exports, search results, and checkpoint data that exceed token limits.
/// </summary>
public class KnowledgeResourceProvider : IResourceProvider
{
    private readonly KnowledgeService _knowledgeService;
    private readonly CheckpointService _checkpointService;
    private readonly ILogger<KnowledgeResourceProvider> _logger;
    private readonly Dictionary<string, CachedResource> _resourceCache = new();
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(15);

    public string Scheme => "knowledge";
    public string Name => "Knowledge Resources";
    public string Description => "Provides access to large knowledge datasets, exports, and search results";

    public KnowledgeResourceProvider(
        KnowledgeService knowledgeService,
        CheckpointService checkpointService,
        ILogger<KnowledgeResourceProvider> logger)
    {
        _knowledgeService = knowledgeService;
        _checkpointService = checkpointService;
        _logger = logger;
    }

    public bool CanHandle(string uri)
    {
        return uri?.StartsWith($"{Scheme}://", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public Task<List<Resource>> ListResourcesAsync(CancellationToken cancellationToken = default)
    {
        var resources = new List<Resource>();

        // Clean expired cache entries
        CleanExpiredCache();

        // Add cached resources
        foreach (var cached in _resourceCache.Values.Where(c => !c.IsExpired))
        {
            resources.Add(new Resource
            {
                Uri = cached.Uri,
                Name = cached.Name,
                Description = cached.Description,
                MimeType = cached.MimeType
            });
        }

        return Task.FromResult(resources);
    }

    public async Task<ReadResourceResult?> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(uri))
            return null;

        try
        {
            // Check cache first
            if (_resourceCache.TryGetValue(uri, out var cached) && !cached.IsExpired)
            {
                _logger.LogDebug("Returning cached resource: {Uri}", uri);
                return new ReadResourceResult
                {
                    Contents = new List<ResourceContent>
                    {
                        new ResourceContent
                        {
                            Uri = uri,
                            MimeType = cached.MimeType,
                            Text = cached.Content
                        }
                    }
                };
            }

            // Parse URI to determine resource type
            var parsedUri = ParseResourceUri(uri);
            if (parsedUri == null)
            {
                _logger.LogWarning("Invalid resource URI format: {Uri}", uri);
                return null;
            }

            // Generate resource content based on type
            var content = parsedUri.Type switch
            {
                "export" => await GenerateExportResource(parsedUri.Id, parsedUri.Parameters, cancellationToken),
                "search" => await GenerateSearchResource(parsedUri.Id, parsedUri.Parameters, cancellationToken),
                "checkpoint" => await GenerateCheckpointResource(parsedUri.Id, cancellationToken),
                "session" => await GenerateSessionResource(parsedUri.Id, cancellationToken),
                _ => null
            };

            if (content == null)
            {
                _logger.LogWarning("Could not generate content for resource: {Uri}", uri);
                return null;
            }

            // Cache the resource
            CacheResource(uri, content.Name, content.Description, content.MimeType, content.Text);

            return new ReadResourceResult
            {
                Contents = new List<ResourceContent>
                {
                    new ResourceContent
                    {
                        Uri = uri,
                        MimeType = content.MimeType,
                        Text = content.Text
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading resource: {Uri}", uri);
            return null;
        }
    }

    /// <summary>
    /// Stores data as a resource and returns the URI for accessing it.
    /// </summary>
    public string StoreAsResource<T>(string type, string id, T data, string? description = null)
    {
        var uri = $"{Scheme}://{type}/{id}";
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var name = type switch
        {
            "export" => $"Knowledge Export {id}",
            "search" => $"Search Results {id}",
            "checkpoint" => $"Checkpoint {id}",
            _ => $"{type} {id}"
        };

        CacheResource(uri, name, description ?? $"Resource for {type}", "application/json", json);
        
        _logger.LogDebug("Stored resource: {Uri} ({Size} bytes)", uri, json.Length);
        return uri;
    }

    private async Task<InternalResourceContent?> GenerateExportResource(string id, Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        // Retrieve export data based on parameters
        var workspace = parameters.GetValueOrDefault("workspace");
        var filterType = parameters.GetValueOrDefault("type");
        
        var items = await _knowledgeService.GetAllAsync(workspace, cancellationToken);
        
        if (!string.IsNullOrEmpty(filterType))
        {
            items = items.Where(k => k.Type.Equals(filterType, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var json = JsonSerializer.Serialize(items, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new InternalResourceContent
        {
            Name = $"Knowledge Export - {workspace ?? "All Workspaces"}",
            Description = $"Export of {items.Count} knowledge items",
            MimeType = "application/json",
            Text = json
        };
    }

    private async Task<InternalResourceContent?> GenerateSearchResource(string id, Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var query = parameters.GetValueOrDefault("query", "");
        var workspace = parameters.GetValueOrDefault("workspace");
        var maxResults = int.Parse(parameters.GetValueOrDefault("max", "100") ?? "100");

        var results = await _knowledgeService.SearchAsync(query, workspace, maxResults, cancellationToken);

        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new InternalResourceContent
        {
            Name = $"Search Results - '{query}'",
            Description = $"Found {results.Count} matching items",
            MimeType = "application/json",
            Text = json
        };
    }

    private async Task<InternalResourceContent?> GenerateCheckpointResource(string id, CancellationToken cancellationToken)
    {
        var checkpoint = await _checkpointService.GetCheckpointAsync(id);
        if (checkpoint == null)
            return null;

        var json = JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new InternalResourceContent
        {
            Name = $"Checkpoint - {checkpoint.SessionId}",
            Description = checkpoint.Content?.Substring(0, Math.Min(100, checkpoint.Content.Length)) + "...",
            MimeType = "application/json",
            Text = json
        };
    }

    private async Task<InternalResourceContent?> GenerateSessionResource(string sessionId, CancellationToken cancellationToken)
    {
        var checkpoints = await _checkpointService.ListCheckpointsAsync(sessionId, 50);

        var json = JsonSerializer.Serialize(checkpoints, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new InternalResourceContent
        {
            Name = $"Session History - {sessionId}",
            Description = $"Contains {checkpoints.Count} checkpoints",
            MimeType = "application/json",
            Text = json
        };
    }

    private ParsedResourceUri? ParseResourceUri(string uri)
    {
        try
        {
            var uriWithoutScheme = uri.Substring($"{Scheme}://".Length);
            var parts = uriWithoutScheme.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length == 0)
                return null;

            var result = new ParsedResourceUri
            {
                Type = parts[0],
                Id = parts.Length > 1 ? parts[1] : "",
                Parameters = new Dictionary<string, string>()
            };

            // Parse query parameters if present
            if (parts.Length > 2)
            {
                var queryString = string.Join("/", parts.Skip(2));
                var queryParts = queryString.Split('&');
                foreach (var part in queryParts)
                {
                    var kvp = part.Split('=');
                    if (kvp.Length == 2)
                    {
                        result.Parameters[kvp[0]] = Uri.UnescapeDataString(kvp[1]);
                    }
                }
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private void CacheResource(string uri, string name, string description, string mimeType, string content)
    {
        _resourceCache[uri] = new CachedResource
        {
            Uri = uri,
            Name = name,
            Description = description,
            MimeType = mimeType,
            Content = content,
            CachedAt = DateTime.UtcNow
        };

        // Clean old entries if cache is getting large
        if (_resourceCache.Count > 100)
        {
            CleanExpiredCache();
        }
    }

    private void CleanExpiredCache()
    {
        var expired = _resourceCache
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expired)
        {
            _resourceCache.Remove(key);
        }

        _logger.LogDebug("Cleaned {Count} expired cache entries", expired.Count);
    }

    private class ParsedResourceUri
    {
        public string Type { get; set; } = "";
        public string Id { get; set; } = "";
        public Dictionary<string, string> Parameters { get; set; } = new();
    }

    private class CachedResource
    {
        public string Uri { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string MimeType { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime CachedAt { get; set; }
        
        public bool IsExpired => DateTime.UtcNow - CachedAt > TimeSpan.FromMinutes(15);
    }

    private class InternalResourceContent
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string MimeType { get; set; } = "";
        public string Text { get; set; } = "";
    }
}