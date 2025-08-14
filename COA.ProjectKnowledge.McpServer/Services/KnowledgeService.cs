using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Data.Entities;
using COA.ProjectKnowledge.McpServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Services;

public class KnowledgeService
{
    private readonly KnowledgeDbContext _context;
    private readonly IWorkspaceResolver _workspaceResolver;
    private readonly ILogger<KnowledgeService> _logger;
    private readonly RealTimeNotificationService _notificationService;
    
    public KnowledgeService(
        KnowledgeDbContext context,
        IWorkspaceResolver workspaceResolver,
        ILogger<KnowledgeService> logger,
        RealTimeNotificationService notificationService)
    {
        _context = context;
        _workspaceResolver = workspaceResolver;
        _logger = logger;
        _notificationService = notificationService;
    }
    
    // Removed duplicate method - use ChronologicalId.Generate() instead
    
    public async Task<StoreKnowledgeResponse> StoreKnowledgeAsync(StoreKnowledgeRequest request)
    {
        try
        {
            // Validate type
            if (!KnowledgeTypes.ValidTypes.Contains(request.Type))
            {
                return new StoreKnowledgeResponse
                {
                    Success = false,
                    Error = $"Invalid knowledge type: {request.Type}. Valid types are: {string.Join(", ", KnowledgeTypes.ValidTypes)}"
                };
            }
            
            var id = ChronologicalId.Generate();
            var workspace = request.Workspace ?? _workspaceResolver.GetCurrentWorkspace();
            
            var entity = new KnowledgeEntity
            {
                Id = id,
                Type = request.Type,
                Content = request.Content,
                Workspace = workspace,
                Tags = request.Tags != null ? JsonSerializer.Serialize(request.Tags) : null,
                Status = request.Status,
                Priority = request.Priority,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                AccessCount = 0
            };
            
            // Handle metadata
            if (request.Metadata != null || request.CodeSnippets != null)
            {
                var metadata = new Dictionary<string, object>();
                
                if (request.Metadata != null)
                {
                    foreach (var kvp in request.Metadata)
                    {
                        metadata[kvp.Key] = kvp.Value;
                    }
                }
                
                if (request.CodeSnippets != null)
                {
                    metadata["CodeSnippets"] = request.CodeSnippets;
                }
                
                entity.Metadata = JsonSerializer.Serialize(metadata);
            }
            
            _context.Knowledge.Add(entity);
            
            // Handle relationships
            if (request.RelatedTo != null)
            {
                foreach (var relatedId in request.RelatedTo)
                {
                    var relationship = new RelationshipEntity
                    {
                        Id = Guid.NewGuid().ToString(),
                        FromId = id,
                        ToId = relatedId,
                        RelationshipType = "relates_to",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Relationships.Add(relationship);
                }
            }
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Stored {Type} knowledge: {Id}", request.Type, id);
            
            // Broadcast real-time notification for new knowledge
            var knowledgeItem = new KnowledgeSearchItem
            {
                Id = id,
                Type = request.Type,
                Content = request.Content,
                Tags = request.Tags?.ToArray() ?? Array.Empty<string>()
            };
            
            // Fire-and-forget notification (don't block the response)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _notificationService.BroadcastKnowledgeCreatedAsync(knowledgeItem, workspace);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to broadcast knowledge creation notification for {Id}", id);
                }
            });
            
            return new StoreKnowledgeResponse
            {
                Success = true,
                KnowledgeId = id,
                Message = $"Knowledge stored successfully with ID: {id}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing knowledge");
            return new StoreKnowledgeResponse
            {
                Success = false,
                Error = $"Failed to store knowledge: {ex.Message}"
            };
        }
    }
    
    public async Task<SearchKnowledgeResponse> SearchKnowledgeAsync(SearchKnowledgeRequest request)
    {
        try
        {
            var requestedWorkspace = request.Workspace ?? _workspaceResolver.GetCurrentWorkspace();
            var resolvedWorkspace = await ResolveWorkspaceNameAsync(requestedWorkspace);
            var workspace = resolvedWorkspace ?? requestedWorkspace;
            var query = _context.Knowledge.Where(k => k.Workspace == workspace);
            
            // Handle special query syntax
            if (!string.IsNullOrWhiteSpace(request.Query))
            {
                if (request.Query.StartsWith("type:"))
                {
                    var type = request.Query.Substring(5);
                    query = query.Where(k => k.Type == type);
                }
                else
                {
                    // Simple text search in content
                    var searchTerm = request.Query.ToLower();
                    query = query.Where(k => 
                        k.Content.ToLower().Contains(searchTerm) ||
                        (k.Tags != null && k.Tags.ToLower().Contains(searchTerm)));
                }
            }
            
            // Order by chronological ID descending (contains timestamp for natural sorting)
            query = query.OrderByDescending(k => k.Id);
            
            // Get total count before limiting
            var totalCount = await query.CountAsync();
            
            // Apply limit
            var entities = await query
                .Take(request.MaxResults ?? 50)
                .ToListAsync();
            
            // Update access tracking using EF Core (simpler and safer than raw SQL)
            if (entities.Any())
            {
                var now = DateTime.UtcNow;
                foreach (var entity in entities)
                {
                    entity.AccessedAt = now;
                    entity.AccessCount++;
                }
                await _context.SaveChangesAsync();
            }
            
            // Convert to response models
            var items = entities.Select(e => new KnowledgeSearchItem
            {
                Id = e.Id,
                Type = e.Type,
                Content = e.Content,
                Tags = e.Tags != null ? JsonSerializer.Deserialize<string[]>(e.Tags) : null,
                Status = e.Status,
                Priority = e.Priority,
                CreatedAt = e.CreatedAt,
                ModifiedAt = e.ModifiedAt,
                AccessCount = e.AccessCount
            }).ToList();
            
            return new SearchKnowledgeResponse
            {
                Success = true,
                Items = items,
                TotalCount = totalCount,
                Message = $"Found {items.Count} matching knowledge items"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching knowledge");
            return new SearchKnowledgeResponse
            {
                Success = false,
                Items = new List<KnowledgeSearchItem>(),
                Error = $"Failed to search knowledge: {ex.Message}"
            };
        }
    }
    
    public async Task<SearchKnowledgeResponse> SearchAcrossWorkspacesAsync(CrossWorkspaceSearchRequest request)
    {
        try
        {
            var query = _context.Knowledge.AsQueryable();
            
            // Apply workspace filter if specified
            if (request.Workspaces != null && request.Workspaces.Any())
            {
                query = query.Where(k => request.Workspaces.Contains(k.Workspace ?? string.Empty));
            }
            // If no workspaces specified, search across ALL workspaces (true cross-project search)
            
            // Handle special query syntax
            if (!string.IsNullOrWhiteSpace(request.Query))
            {
                if (request.Query.StartsWith("type:"))
                {
                    var type = request.Query.Substring(5);
                    query = query.Where(k => k.Type == type);
                }
                else if (request.Query.StartsWith("workspace:"))
                {
                    var workspace = request.Query.Substring(10).Trim('"');
                    query = query.Where(k => k.Workspace == workspace);
                }
                else if (request.Query.StartsWith("tag:"))
                {
                    var tag = request.Query.Substring(4).ToLower();
                    query = query.Where(k => k.Tags != null && k.Tags.ToLower().Contains(tag));
                }
                else
                {
                    // Simple text search in content across all workspaces
                    var searchTerm = request.Query.ToLower();
                    query = query.Where(k => 
                        k.Content.ToLower().Contains(searchTerm) ||
                        (k.Tags != null && k.Tags.ToLower().Contains(searchTerm)));
                }
            }
            
            // Order by chronological ID descending (contains timestamp for natural sorting)
            query = query.OrderByDescending(k => k.Id);
            
            // Get total count before limiting
            var totalCount = await query.CountAsync();
            
            // Apply limit
            var entities = await query
                .Take(request.MaxResults ?? 50)
                .ToListAsync();
            
            // Update access tracking
            if (entities.Any())
            {
                var now = DateTime.UtcNow;
                foreach (var entity in entities)
                {
                    entity.AccessedAt = now;
                    entity.AccessCount++;
                }
                await _context.SaveChangesAsync();
            }
            
            // Convert to response models with workspace information
            var items = entities.Select(e => new CrossWorkspaceSearchItem
            {
                Id = e.Id,
                Type = e.Type,
                Content = e.Content,
                Workspace = e.Workspace ?? "unknown",
                Tags = e.Tags != null ? JsonSerializer.Deserialize<string[]>(e.Tags) : null,
                Status = e.Status,
                Priority = e.Priority,
                CreatedAt = e.CreatedAt,
                ModifiedAt = e.ModifiedAt,
                AccessCount = e.AccessCount
            }).ToList();
            
            return new SearchKnowledgeResponse
            {
                Success = true,
                Items = items.Cast<KnowledgeSearchItem>().ToList(),
                TotalCount = totalCount,
                Message = $"Found {items.Count} matching knowledge items across {items.Select(i => i.Workspace).Distinct().Count()} workspaces"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching knowledge across workspaces");
            return new SearchKnowledgeResponse
            {
                Success = false,
                Items = new List<KnowledgeSearchItem>(),
                Error = $"Failed to search knowledge across workspaces: {ex.Message}"
            };
        }
    }
    
    public async Task<List<KnowledgeEntity>> GetAllAsync(string? workspace, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.Knowledge.AsQueryable();
            
            if (!string.IsNullOrEmpty(workspace))
            {
                query = query.Where(k => k.Workspace == workspace);
            }
            
            return await query.OrderByDescending(k => k.Id).ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all knowledge items");
            return new List<KnowledgeEntity>();
        }
    }
    
    public async Task<List<KnowledgeEntity>> SearchAsync(string query, string? workspace, int maxResults, CancellationToken cancellationToken = default)
    {
        try
        {
            var dbQuery = _context.Knowledge.AsQueryable();
            
            if (!string.IsNullOrEmpty(workspace))
            {
                dbQuery = dbQuery.Where(k => k.Workspace == workspace);
            }
            
            if (!string.IsNullOrWhiteSpace(query))
            {
                var searchTerm = query.ToLower();
                dbQuery = dbQuery.Where(k => 
                    k.Content.ToLower().Contains(searchTerm) ||
                    (k.Tags != null && k.Tags.ToLower().Contains(searchTerm)));
            }
            
            return await dbQuery
                .OrderByDescending(k => k.Id)
                .Take(maxResults)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching knowledge");
            return new List<KnowledgeEntity>();
        }
    }
    
    public async Task<List<string>> GetAvailableWorkspacesAsync()
    {
        try
        {
            var workspaces = await _context.Knowledge
                .Where(k => k.Workspace != null)
                .Select(k => k.Workspace!)
                .Distinct()
                .OrderBy(w => w)
                .ToListAsync();
            
            return workspaces;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available workspaces");
            return new List<string>();
        }
    }
    
    private async Task<string?> ResolveWorkspaceNameAsync(string workspaceName)
    {
        if (string.IsNullOrWhiteSpace(workspaceName))
            return null;
            
        // First try exact match
        var exactMatch = await _context.Knowledge
            .Where(k => k.Workspace == workspaceName)
            .Select(k => k.Workspace)
            .FirstOrDefaultAsync();
            
        if (exactMatch != null)
            return exactMatch;
        
        // If no exact match, try normalized matching
        var normalized = _workspaceResolver.NormalizeWorkspaceName(workspaceName);
        var allWorkspaces = await _context.Knowledge
            .Where(k => k.Workspace != null)
            .Select(k => k.Workspace!)
            .Distinct()
            .ToListAsync();
            
        // Find a workspace that normalizes to the same value
        foreach (var workspace in allWorkspaces)
        {
            if (_workspaceResolver.NormalizeWorkspaceName(workspace) == normalized)
            {
                return workspace;
            }
        }
        
        return null;
    }
    
    public async Task<GetKnowledgeResponse> GetKnowledgeAsync(string id)
    {
        try
        {
            var entity = await _context.Knowledge.FindAsync(id);
            
            if (entity == null)
            {
                return new GetKnowledgeResponse
                {
                    Success = false,
                    Error = $"Knowledge with ID {id} not found"
                };
            }
            
            // Update access tracking efficiently using bulk SQL update
            var now = DateTime.UtcNow;
            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE Knowledge SET AccessedAt = @now, AccessCount = AccessCount + 1 WHERE Id = @id",
                new Microsoft.Data.Sqlite.SqliteParameter("@now", now),
                new Microsoft.Data.Sqlite.SqliteParameter("@id", id));
            
            // Update local entity values to match database after bulk update
            entity.AccessedAt = now;
            entity.AccessCount++;
            
            var knowledge = new Knowledge
            {
                Id = entity.Id,
                Type = entity.Type,
                Content = entity.Content,
                Workspace = entity.Workspace ?? "",
                CreatedAt = entity.CreatedAt,
                ModifiedAt = entity.ModifiedAt,
                AccessedAt = entity.AccessedAt,
                AccessCount = entity.AccessCount,
                ArchivedAt = entity.ArchivedAt,
                ExpiresAt = entity.ExpiresAt
            };
            
            // Parse and set metadata
            if (!string.IsNullOrWhiteSpace(entity.Metadata))
            {
                knowledge.Metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entity.Metadata) 
                    ?? new Dictionary<string, JsonElement>();
            }
            
            // Set tags, status, priority through metadata
            if (!string.IsNullOrWhiteSpace(entity.Tags))
            {
                knowledge.SetMetadata("tags", JsonSerializer.Deserialize<string[]>(entity.Tags));
            }
            if (!string.IsNullOrWhiteSpace(entity.Status))
            {
                knowledge.SetMetadata("status", entity.Status);
            }
            if (!string.IsNullOrWhiteSpace(entity.Priority))
            {
                knowledge.SetMetadata("priority", entity.Priority);
            }
            
            return new GetKnowledgeResponse
            {
                Success = true,
                Knowledge = knowledge
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting knowledge");
            return new GetKnowledgeResponse
            {
                Success = false,
                Error = $"Failed to get knowledge: {ex.Message}"
            };
        }
    }
    
    public async Task<UpdateKnowledgeResponse> UpdateKnowledgeAsync(UpdateKnowledgeRequest request)
    {
        try
        {
            var entity = await _context.Knowledge.FindAsync(request.Id);
            
            if (entity == null)
            {
                return new UpdateKnowledgeResponse
                {
                    Success = false,
                    Error = $"Knowledge with ID {request.Id} not found"
                };
            }
            
            // Update fields
            if (!string.IsNullOrWhiteSpace(request.Content))
                entity.Content = request.Content;
            
            if (request.Tags != null)
                entity.Tags = JsonSerializer.Serialize(request.Tags);
            
            if (!string.IsNullOrWhiteSpace(request.Status))
                entity.Status = request.Status;
            
            if (!string.IsNullOrWhiteSpace(request.Priority))
                entity.Priority = request.Priority;
            
            entity.ModifiedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Updated knowledge: {Id}", request.Id);
            
            return new UpdateKnowledgeResponse
            {
                Success = true,
                Message = $"Knowledge {request.Id} updated successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating knowledge");
            return new UpdateKnowledgeResponse
            {
                Success = false,
                Error = $"Failed to update knowledge: {ex.Message}"
            };
        }
    }
    
    public async Task<DeleteKnowledgeResponse> DeleteKnowledgeAsync(string id)
    {
        try
        {
            var entity = await _context.Knowledge.FindAsync(id);
            
            if (entity == null)
            {
                return new DeleteKnowledgeResponse
                {
                    Success = false,
                    Error = $"Knowledge with ID {id} not found"
                };
            }
            
            _context.Knowledge.Remove(entity);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Deleted knowledge: {Id}", id);
            
            return new DeleteKnowledgeResponse
            {
                Success = true,
                Message = $"Knowledge {id} deleted successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting knowledge");
            return new DeleteKnowledgeResponse
            {
                Success = false,
                Error = $"Failed to delete knowledge: {ex.Message}"
            };
        }
    }
    
    public async Task<TimelineResponse> GetTimelineAsync(TimelineRequest request)
    {
        try
        {
            var requestedWorkspace = request.Workspace ?? _workspaceResolver.GetCurrentWorkspace();
            var resolvedWorkspace = await ResolveWorkspaceNameAsync(requestedWorkspace);
            var workspace = resolvedWorkspace ?? requestedWorkspace;
            var query = _context.Knowledge.Where(k => k.Workspace == workspace);
            
            // Apply time filter
            DateTime startDate;
            if (request.StartDate.HasValue)
            {
                startDate = request.StartDate.Value;
            }
            else if (request.HoursAgo.HasValue)
            {
                startDate = DateTime.UtcNow.AddHours(-request.HoursAgo.Value);
            }
            else if (request.DaysAgo.HasValue)
            {
                startDate = DateTime.UtcNow.AddDays(-request.DaysAgo.Value);
            }
            else
            {
                startDate = DateTime.UtcNow.AddDays(-7); // Default to last 7 days
            }
            
            DateTime endDate = request.EndDate ?? DateTime.UtcNow;
            
            query = query.Where(k => k.CreatedAt >= startDate && k.CreatedAt <= endDate);
            
            // Apply type filter if specified
            if (!string.IsNullOrWhiteSpace(request.Type))
            {
                query = query.Where(k => k.Type == request.Type);
            }
            
            // Order by creation date descending for consistent chronological sorting
            query = query.OrderByDescending(k => k.CreatedAt);
            
            // Apply limit
            var entities = await query
                .Take(request.MaxResults ?? 100)
                .ToListAsync();
            
            // Convert to timeline items
            var timeline = entities.Select(e => new TimelineItem
            {
                Id = e.Id,
                Type = e.Type,
                Summary = e.Content.Length > 100 ? e.Content.Substring(0, 100) + "..." : e.Content,
                CreatedAt = e.CreatedAt,
                ModifiedAt = e.ModifiedAt,
                Workspace = e.Workspace ?? "",
                Tags = e.Tags != null ? JsonSerializer.Deserialize<string[]>(e.Tags) : null,
                Status = e.Status,
                Priority = e.Priority,
                AccessCount = e.AccessCount
            }).ToList();
            
            return new TimelineResponse
            {
                Success = true,
                Timeline = timeline,
                TotalCount = timeline.Count,
                DateRange = new DateRange { From = startDate, To = endDate }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting timeline");
            return new TimelineResponse
            {
                Success = false,
                Timeline = new List<TimelineItem>(),
                Error = $"Failed to get timeline: {ex.Message}"
            };
        }
    }
    
    public async Task<WorkspaceStats> GetStatsAsync()
    {
        var workspace = _workspaceResolver.GetCurrentWorkspace();
        var totalItems = await _context.Knowledge.Where(k => k.Workspace == workspace).CountAsync();
        
        return new WorkspaceStats
        {
            TotalItems = totalItems,
            Workspace = workspace
        };
    }
    
    /// <summary>
    /// Enhanced search with temporal scoring and advanced filtering
    /// </summary>
    public async Task<SearchKnowledgeResponse> SearchEnhancedAsync(KnowledgeSearchParameters parameters)
    {
        try
        {
            var workspace = parameters.Workspace ?? _workspaceResolver.GetCurrentWorkspace();
            var query = _context.Knowledge.AsQueryable();
            
            // Apply workspace filter
            if (!string.IsNullOrWhiteSpace(workspace))
            {
                query = query.Where(k => k.Workspace == workspace);
            }
            
            // Apply type filter
            if (parameters.Types != null && parameters.Types.Any())
            {
                query = query.Where(k => parameters.Types.Contains(k.Type));
            }
            
            // Apply date range filter
            if (parameters.FromDate.HasValue)
            {
                query = query.Where(k => k.CreatedAt >= parameters.FromDate.Value);
            }
            if (parameters.ToDate.HasValue)
            {
                query = query.Where(k => k.CreatedAt <= parameters.ToDate.Value);
            }
            
            // Apply archived filter
            if (!parameters.IncludeArchived)
            {
                query = query.Where(k => k.ArchivedAt == null);
            }
            
            // Apply expired filter
            query = query.Where(k => k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow);
            
            // Apply tag filter
            if (parameters.Tags != null && parameters.Tags.Any())
            {
                foreach (var tag in parameters.Tags)
                {
                    var tagLower = tag.ToLower();
                    query = query.Where(k => k.Tags != null && k.Tags.ToLower().Contains(tagLower));
                }
            }
            
            // Apply status filter
            if (parameters.Statuses != null && parameters.Statuses.Any())
            {
                query = query.Where(k => parameters.Statuses.Contains(k.Status));
            }
            
            // Apply priority filter
            if (parameters.Priorities != null && parameters.Priorities.Any())
            {
                query = query.Where(k => parameters.Priorities.Contains(k.Priority));
            }
            
            // Apply text search
            if (!string.IsNullOrWhiteSpace(parameters.Query))
            {
                var searchTerm = parameters.Query.ToLower();
                query = query.Where(k => 
                    k.Content.ToLower().Contains(searchTerm) ||
                    (k.Tags != null && k.Tags.ToLower().Contains(searchTerm)) ||
                    (k.Type != null && k.Type.ToLower().Contains(searchTerm)));
            }
            
            // Get all matching items for scoring
            var entities = await query.ToListAsync();
            
            // Apply temporal scoring if enabled
            if (parameters.TemporalScoring != TemporalScoringMode.None)
            {
                var decayFunction = parameters.TemporalScoring switch
                {
                    TemporalScoringMode.Aggressive => TemporalDecayFunction.Aggressive,
                    TemporalScoringMode.Gentle => TemporalDecayFunction.Gentle,
                    _ => TemporalDecayFunction.Default
                };
                
                var now = DateTime.UtcNow;
                
                // Calculate scores for each item
                var scoredItems = entities.Select(e =>
                {
                    var ageInDays = (now - e.CreatedAt).TotalDays;
                    var temporalScore = decayFunction.Calculate(ageInDays);
                    
                    // Apply access count boost if enabled
                    var accessBoost = 1.0f;
                    if (parameters.BoostFrequent && e.AccessCount > 0)
                    {
                        // Logarithmic scaling for access count (max 20% boost)
                        accessBoost = 1.0f + (float)(Math.Log10(e.AccessCount + 1) * 0.05);
                    }
                    
                    // Apply recency boost if enabled
                    var recencyBoost = 1.0f;
                    if (parameters.BoostRecent && e.ModifiedAt != e.CreatedAt)
                    {
                        var modifiedAgeInDays = (now - e.ModifiedAt).TotalDays;
                        recencyBoost = 1.0f + (float)Math.Max(0, (7 - modifiedAgeInDays) / 7 * 0.2); // 20% boost for items modified in last week
                    }
                    
                    var finalScore = temporalScore * accessBoost * recencyBoost;
                    
                    return new { Entity = e, Score = finalScore };
                }).ToList();
                
                // Sort by score (highest first) then by ID for stability
                entities = scoredItems
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Entity.Id)
                    .Select(x => x.Entity)
                    .ToList();
            }
            else
            {
                // Apply standard sorting
                entities = parameters.OrderBy?.ToLower() switch
                {
                    "created" => parameters.OrderDescending 
                        ? entities.OrderByDescending(e => e.CreatedAt).ToList()
                        : entities.OrderBy(e => e.CreatedAt).ToList(),
                    "modified" => parameters.OrderDescending
                        ? entities.OrderByDescending(e => e.ModifiedAt).ToList()
                        : entities.OrderBy(e => e.ModifiedAt).ToList(),
                    "accessed" => parameters.OrderDescending
                        ? entities.OrderByDescending(e => e.AccessedAt ?? DateTime.MinValue).ToList()
                        : entities.OrderBy(e => e.AccessedAt ?? DateTime.MinValue).ToList(),
                    "accesscount" => parameters.OrderDescending
                        ? entities.OrderByDescending(e => e.AccessCount).ToList()
                        : entities.OrderBy(e => e.AccessCount).ToList(),
                    _ => entities.OrderByDescending(e => e.Id).ToList() // Default to chronological
                };
            }
            
            // Get total count before limiting
            var totalCount = entities.Count;
            
            // Apply limit
            entities = entities.Take(parameters.MaxResults).ToList();
            
            // Skip access tracking update in search to avoid entity tracking issues
            // This can be handled separately if needed
            
            // Convert to response models
            var items = entities.Select(e => new KnowledgeSearchItem
            {
                Id = e.Id,
                Type = e.Type,
                Content = e.Content,
                Tags = e.Tags != null ? JsonSerializer.Deserialize<string[]>(e.Tags) : null,
                Status = e.Status,
                Priority = e.Priority,
                CreatedAt = e.CreatedAt,
                ModifiedAt = e.ModifiedAt,
                AccessCount = e.AccessCount
            }).ToList();
            
            return new SearchKnowledgeResponse
            {
                Success = true,
                Items = items,
                TotalCount = totalCount,
                Message = $"Found {items.Count} matching knowledge items"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in enhanced search: {Message}", ex.ToString());
            return new SearchKnowledgeResponse
            {
                Success = false,
                Items = new List<KnowledgeSearchItem>(),
                Error = $"Failed to search knowledge: {ex.Message} - Inner: {ex.InnerException?.Message}"
            };
        }
    }
}