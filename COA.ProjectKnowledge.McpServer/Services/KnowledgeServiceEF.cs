using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Data.Entities;
using COA.ProjectKnowledge.McpServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Services;

public class KnowledgeServiceEF
{
    private readonly KnowledgeDbContext _context;
    private readonly IWorkspaceResolver _workspaceResolver;
    private readonly ILogger<KnowledgeServiceEF> _logger;
    
    public KnowledgeServiceEF(
        KnowledgeDbContext context,
        IWorkspaceResolver workspaceResolver,
        ILogger<KnowledgeServiceEF> logger)
    {
        _context = context;
        _workspaceResolver = workspaceResolver;
        _logger = logger;
    }
    
    public string GenerateChronologicalId()
    {
        var timestamp = DateTime.UtcNow;
        var ticks = timestamp.Ticks;
        var hexTicks = ticks.ToString("X11");
        var random = Random.Shared.Next(0x100000, 0xFFFFFF).ToString("X6");
        return $"{hexTicks}-{random}";
    }
    
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
            
            var id = GenerateChronologicalId();
            var workspace = _workspaceResolver.GetCurrentWorkspace();
            
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
                    foreach (var meta in request.Metadata)
                    {
                        var parts = meta.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            metadata[parts[0]] = parts[1];
                        }
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
            var workspace = request.Workspace ?? _workspaceResolver.GetCurrentWorkspace();
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
            
            // Order by creation date descending
            query = query.OrderByDescending(k => k.CreatedAt);
            
            // Get total count before limiting
            var totalCount = await query.CountAsync();
            
            // Apply limit
            var entities = await query
                .Take(request.MaxResults ?? 50)
                .ToListAsync();
            
            // Update access tracking
            foreach (var entity in entities)
            {
                entity.AccessedAt = DateTime.UtcNow;
                entity.AccessCount++;
            }
            
            if (entities.Any())
            {
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
            
            // Update access tracking
            entity.AccessedAt = DateTime.UtcNow;
            entity.AccessCount++;
            await _context.SaveChangesAsync();
            
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
                IsArchived = entity.ArchivedAt.HasValue
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
            var workspace = request.Workspace ?? _workspaceResolver.GetCurrentWorkspace();
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
            
            // Order by creation date descending
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
}