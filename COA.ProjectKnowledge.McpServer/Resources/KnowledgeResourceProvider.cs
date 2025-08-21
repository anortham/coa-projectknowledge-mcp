using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Protocol;
using COA.ProjectKnowledge.McpServer.Services;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Resources;

/// <summary>
/// Provides URI-addressable resources for large knowledge data sets.
/// Handles exports and search results that exceed token limits.
/// Uses the framework's IResourceCache for proper lifetime management.
/// </summary>
public class KnowledgeResourceProvider : IResourceProvider
{
    private readonly KnowledgeService _knowledgeService;
    private readonly IResourceCache<ReadResourceResult> _resourceCache;
    private readonly ILogger<KnowledgeResourceProvider> _logger;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(15);

    public string Scheme => "knowledge";
    public string Name => "Knowledge Resources";
    public string Description => "Provides access to large knowledge datasets, exports, and search results";

    public KnowledgeResourceProvider(
        KnowledgeService knowledgeService,
        IResourceCache<ReadResourceResult> resourceCache,
        ILogger<KnowledgeResourceProvider> logger)
    {
        _knowledgeService = knowledgeService;
        _resourceCache = resourceCache;
        _logger = logger;
    }

    public bool CanHandle(string uri)
    {
        return uri?.StartsWith($"{Scheme}://", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public Task<List<Resource>> ListResourcesAsync(CancellationToken cancellationToken = default)
    {
        // The framework manages the cache, we don't need to list cached resources
        // Return an empty list as cached resources are accessed directly by URI
        return Task.FromResult(new List<Resource>());
    }

    public async Task<ReadResourceResult?> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(uri))
            return null;

        try
        {
            // Check cache first - the framework handles expiration
            var cachedResult = await _resourceCache.GetAsync(uri);
            if (cachedResult != null)
            {
                _logger.LogDebug("Returning cached resource: {Uri}", uri);
                return cachedResult;
            }

            // Parse URI to determine resource type
            var parsedUri = ParseResourceUri(uri);
            if (parsedUri == null)
            {
                _logger.LogWarning("Invalid resource URI format: {Uri}", uri);
                return null;
            }

            // For stored resources (search, timeline), we can't regenerate them
            if (parsedUri.Type == "search" || parsedUri.Type == "timeline")
            {
                _logger.LogWarning("Stored resource expired and cannot be regenerated: {Uri}", uri);
                return new ReadResourceResult
                {
                    Contents = new List<ResourceContent>
                    {
                        new ResourceContent
                        {
                            Uri = uri,
                            MimeType = "text/plain",
                            Text = "Resource has expired and is no longer available. Please run the operation again."
                        }
                    }
                };
            }

            // Try to generate resource content for types that can be regenerated
            var content = parsedUri.Type switch
            {
                "export" when parsedUri.Parameters.Count > 0 => await GenerateExportResource(parsedUri.Id, parsedUri.Parameters, cancellationToken),
                _ => null
            };

            if (content == null)
            {
                _logger.LogWarning("Could not generate content for resource: {Uri}", uri);
                return new ReadResourceResult
                {
                    Contents = new List<ResourceContent>
                    {
                        new ResourceContent
                        {
                            Uri = uri,
                            MimeType = "text/plain",
                            Text = "Resource not found or could not be generated."
                        }
                    }
                };
            }

            // Create result and cache it
            var result = new ReadResourceResult
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

            // Cache the generated resource
            await _resourceCache.SetAsync(uri, result, _cacheExpiry);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading resource: {Uri}", uri);
            return new ReadResourceResult
            {
                Contents = new List<ResourceContent>
                {
                    new ResourceContent
                    {
                        Uri = uri,
                        MimeType = "text/plain",
                        Text = $"Error reading resource: {ex.Message}"
                    }
                }
            };
        }
    }

    /// <summary>
    /// Stores data as a resource and returns the URI for accessing it.
    /// </summary>
    public async Task<string> StoreAsResourceAsync<T>(string type, string id, T data, string? description = null)
    {
        var uri = $"{Scheme}://{type}/{id}";
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var result = new ReadResourceResult
        {
            Contents = new List<ResourceContent>
            {
                new ResourceContent
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = json
                }
            }
        };

        // Store in cache with configured expiration
        await _resourceCache.SetAsync(uri, result, _cacheExpiry);
        
        _logger.LogDebug("Stored resource: {Uri} ({Size} bytes)", uri, json.Length);
        return uri;
    }
    
    /// <summary>
    /// Synchronous version for backward compatibility
    /// </summary>
    public string StoreAsResource<T>(string type, string id, T data, string? description = null)
    {
        return StoreAsResourceAsync(type, id, data, description).GetAwaiter().GetResult();
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

    private class ParsedResourceUri
    {
        public string Type { get; set; } = "";
        public string Id { get; set; } = "";
        public Dictionary<string, string> Parameters { get; set; } = new();
    }

    private class InternalResourceContent
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string MimeType { get; set; } = "";
        public string Text { get; set; } = "";
    }
}