using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Framework.TokenOptimization.Caching;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Constants;
using COA.ProjectKnowledge.McpServer.Helpers;
using COA.ProjectKnowledge.McpServer.Resources;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;

// Use framework attributes with aliases to avoid conflicts
using FrameworkAttributes = COA.Mcp.Framework.Attributes;
using ComponentModel = System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class GetTimelineTool : McpToolBase<GetTimelineParams, GetTimelineResult>
{
    private readonly KnowledgeService _knowledgeService;
    private readonly KnowledgeResourceProvider _resourceProvider;
    private readonly ITokenEstimator _tokenEstimator;
    private readonly IResponseCacheService _cacheService;
    private readonly ExecutionContextService _contextService;
    private readonly ILogger<GetTimelineTool> _logger;
    
    public GetTimelineTool(
        KnowledgeService knowledgeService,
        KnowledgeResourceProvider resourceProvider,
        ITokenEstimator tokenEstimator,
        IResponseCacheService cacheService,
        ExecutionContextService contextService,
        ILogger<GetTimelineTool> logger)
    {
        _knowledgeService = knowledgeService;
        _resourceProvider = resourceProvider;
        _tokenEstimator = tokenEstimator;
        _cacheService = cacheService;
        _contextService = contextService;
        _logger = logger;
    }
    
    public override string Name => ToolNames.ShowActivity;
    public override string Description => ToolDescriptions.ShowActivity;
    public override ToolCategory Category => ToolCategory.Query;

    protected override async Task<GetTimelineResult> ExecuteInternalAsync(GetTimelineParams parameters, CancellationToken cancellationToken)
    {
        // Create execution context for tracking
        var customData = new Dictionary<string, object?>
        {
            ["DaysAgo"] = parameters.DaysAgo,
            ["HoursAgo"] = parameters.HoursAgo,
            ["Type"] = parameters.Type,
            ["Workspace"] = parameters.Workspace ?? "default",
            ["MaxResults"] = parameters.MaxResults ?? 500
        };
        
        return await _contextService.RunWithContextAsync(
            Name,
            async (context) => await ExecuteWithContextAsync(parameters, context, cancellationToken),
            customData: customData);
    }
    
    private async Task<GetTimelineResult> ExecuteWithContextAsync(
        GetTimelineParams parameters, 
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Generate cache key for this timeline request
        var cacheKey = GenerateCacheKey(parameters);
        
        // Check cache first
        if (_cacheService != null)
        {
            var cachedResult = await _cacheService.GetAsync<GetTimelineResult>(cacheKey);
            if (cachedResult != null)
            {
                _logger.LogDebug("Returning cached timeline for key: {CacheKey}", cacheKey);
                context.CustomData["CacheHit"] = true;
                return cachedResult;
            }
        }
        
        try
        {
            // Determine date range
            DateTime startDate, endDate;
            if (parameters.DaysAgo.HasValue)
            {
                endDate = DateTime.UtcNow;
                startDate = endDate.AddDays(-parameters.DaysAgo.Value);
            }
            else if (parameters.HoursAgo.HasValue)
            {
                endDate = DateTime.UtcNow;
                startDate = endDate.AddHours(-parameters.HoursAgo.Value);
            }
            else
            {
                startDate = parameters.StartDate ?? DateTime.UtcNow.AddDays(-7);
                endDate = parameters.EndDate ?? DateTime.UtcNow;
            }
            
            // Build query based on parameters
            var query = "*"; // Start with all items
            
            if (!string.IsNullOrEmpty(parameters.Type))
            {
                query = $"type:{parameters.Type}";
            }
            
            // Use the timeline API directly
            var request = new TimelineRequest
            {
                StartDate = startDate,
                EndDate = endDate,
                Type = parameters.Type,
                Workspace = parameters.Workspace,
                MaxResults = parameters.MaxResults ?? 500
            };
            
            var response = await _knowledgeService.GetTimelineAsync(request);
            
            if (!response.Success)
            {
                return new GetTimelineResult
                {
                    Success = false,
                    Timeline = new List<TimelineEntry>(),
                    Error = ErrorHelpers.CreateTimelineError(response.Error ?? "Failed to get timeline")
                };
            }
            
            var sortedItems = response.Timeline;
            
            // Create timeline entries - map TimelineItem to TimelineEntry
            var timeline = sortedItems.Select(k => new TimelineEntry
            {
                Id = k.Id,
                Type = k.Type,
                Summary = k.Summary,
                CreatedAt = k.CreatedAt,
                ModifiedAt = k.ModifiedAt,
                Workspace = k.Workspace,
                Tags = k.Tags,
                Status = k.Status,
                Priority = k.Priority,
                AccessCount = k.AccessCount
            }).ToList();
            
            // Group by time period
            var groups = GroupByTimePeriod(timeline, parameters.MaxPerGroup ?? 10);
            
            // Build formatted timeline
            var formattedTimeline = BuildFormattedTimeline(groups, timeline.Count, startDate, endDate);
            
            var result = new GetTimelineResult
            {
                Success = true,
                Timeline = timeline,
                FormattedTimeline = formattedTimeline,
                Groups = groups.Select(g => new TimelineGroup
                {
                    Period = g.Key,
                    Count = g.Value.Count,
                    Types = g.Value.GroupBy(t => t.Type).ToDictionary(t => t.Key, t => t.Count())
                }).ToList(),
                TotalCount = timeline.Count,
                DateRange = new DateRange { From = startDate, To = endDate },
                Message = $"Found {timeline.Count} items from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}"
            };
            
            // Check if result is too large and needs resource storage
            var estimatedTokens = _tokenEstimator.EstimateObject(result);
            context.CustomData["EstimatedTokens"] = estimatedTokens;
            
            if (estimatedTokens > 10000 && timeline.Count > 50)
            {
                // Store full results as resource
                var timelineId = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}";
                var resourceUri = _resourceProvider.StoreAsResource(
                    "timeline",
                    timelineId,
                    timeline,
                    $"Full timeline results ({timeline.Count} items)");
                
                // Return truncated result with resource URI
                result.Timeline = timeline.Take(50).ToList();
                result.ResourceUri = resourceUri;
                result.Meta = new ToolExecutionMetadata
                {
                    Truncated = true,
                    Tokens = _tokenEstimator.EstimateObject(result)
                };
                
                _logger.LogInformation("Timeline truncated from {Full} to {Truncated} items, full data at {Uri}",
                    timeline.Count, result.Timeline.Count, resourceUri);
            }
            
            // Cache the result for 5 minutes
            if (_cacheService != null)
            {
                try
                {
                    await _cacheService.SetAsync(cacheKey, result, new CacheEntryOptions());
                    context.CustomData["CacheSet"] = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache timeline for key: {CacheKey}", cacheKey);
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get timeline");
            context.CustomData["Error"] = true;
            
            return new GetTimelineResult
            {
                Success = false,
                Timeline = new List<TimelineEntry>(),
                Error = ErrorHelpers.CreateTimelineError($"Failed to get timeline: {ex.Message}")
            };
        }
    }
    
    private string GenerateCacheKey(GetTimelineParams parameters)
    {
        // Create a deterministic cache key based on parameters
        var keyData = new
        {
            Tool = Name,
            DaysAgo = parameters.DaysAgo,
            HoursAgo = parameters.HoursAgo,
            StartDate = parameters.StartDate?.ToString("yyyyMMddHHmmss"),
            EndDate = parameters.EndDate?.ToString("yyyyMMddHHmmss"),
            Type = parameters.Type,
            Workspace = parameters.Workspace,
            MaxPerGroup = parameters.MaxPerGroup,
            MaxResults = parameters.MaxResults
        };
        
        var json = JsonSerializer.Serialize(keyData);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return $"timeline_{Convert.ToBase64String(hash).Replace("/", "_").Replace("+", "-").Substring(0, 16)}";
    }
    
    private string GetSummary(Knowledge item)
    {
        if (item is Checkpoint checkpoint)
        {
            return $"Checkpoint #{checkpoint.SequenceNumber} - Session: {checkpoint.SessionId}";
        }
        
        if (item is Checklist checklist)
        {
            var completed = checklist.Items?.Count(i => i.IsCompleted) ?? 0;
            var total = checklist.Items?.Count ?? 0;
            return $"{item.Content.Split('\n').FirstOrDefault()?.Trim() ?? "Checklist"} ({completed}/{total})";
        }
        
        // For other types, use first line of content
        var firstLine = item.Content.Split('\n').FirstOrDefault()?.Trim() ?? "";
        if (firstLine.Length > 100)
        {
            firstLine = firstLine.Substring(0, 97) + "...";
        }
        
        return firstLine;
    }
    
    private Dictionary<string, List<TimelineEntry>> GroupByTimePeriod(List<TimelineEntry> timeline, int maxPerGroup)
    {
        var now = DateTime.Now;
        var groups = new Dictionary<string, List<TimelineEntry>>();
        
        foreach (var entry in timeline)
        {
            var period = GetTimePeriod(entry.CreatedAt, now);
            if (!groups.ContainsKey(period))
            {
                groups[period] = new List<TimelineEntry>();
            }
            
            if (groups[period].Count < maxPerGroup)
            {
                groups[period].Add(entry);
            }
        }
        
        return groups;
    }
    
    private string GetTimePeriod(DateTime date, DateTime now)
    {
        // Convert to local time for user-friendly grouping
        var localDate = date.Kind == DateTimeKind.Utc ? date.ToLocalTime() : date;
        var diff = now - localDate;
        
        if (diff.TotalHours < 1) return "Last Hour";
        if (diff.TotalHours < 24) return "Today";
        if (diff.TotalHours < 48) return "Yesterday";
        if (diff.TotalDays < 7) return "This Week";
        if (diff.TotalDays < 14) return "Last Week";
        if (diff.TotalDays < 30) return "This Month";
        if (diff.TotalDays < 60) return "Last Month";
        return "Older";
    }
    
    private string BuildFormattedTimeline(Dictionary<string, List<TimelineEntry>> groups, int totalFound, DateTime startDate, DateTime endDate)
    {
        var sb = new StringBuilder();
        var days = (int)(endDate - startDate).TotalDays;
        
        sb.AppendLine($"# Knowledge Timeline - {(days > 0 ? $"Last {days} Days" : "Today")}" );
        sb.AppendLine($"*{totalFound} total items found*");
        sb.AppendLine($"*Period: {startDate:yyyy-MM-dd HH:mm} to {endDate:yyyy-MM-dd HH:mm} Local*");
        sb.AppendLine();
        
        // Define period order
        var periodOrder = new[] 
        { 
            "Last Hour", "Today", "Yesterday", "This Week", 
            "Last Week", "This Month", "Last Month", "Older" 
        };
        
        foreach (var period in periodOrder)
        {
            if (!groups.ContainsKey(period) || groups[period].Count == 0) continue;
            
            var entries = groups[period];
            sb.AppendLine($"## {period} ({entries.Count} items)");
            sb.AppendLine();
            
            foreach (var entry in entries)
            {
                sb.AppendLine(FormatTimelineEntry(entry));
            }
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    private string FormatTimelineEntry(TimelineEntry entry)
    {
        var sb = new StringBuilder();
        
        // Header with type and time
        var timeAgo = GetFriendlyTimeAgo(entry.CreatedAt);
        sb.AppendLine($"### [{entry.Type}] {timeAgo}");
        
        // Summary (first 150 chars)
        var summaryPreview = entry.Summary.Length > 150 
            ? entry.Summary.Substring(0, 147) + "..." 
            : entry.Summary;
        sb.AppendLine(summaryPreview);
        
        // Key fields
        var fields = new List<string>();
        if (!string.IsNullOrEmpty(entry.Status)) fields.Add($"Status: {entry.Status}");
        if (!string.IsNullOrEmpty(entry.Priority)) fields.Add($"Priority: {entry.Priority}");
        if (entry.AccessCount > 0) fields.Add($"Accessed: {entry.AccessCount}x");
        
        if (fields.Any())
        {
            sb.AppendLine($"*{string.Join(" | ", fields)}*");
        }
        
        // Tags
        if (entry.Tags?.Any() == true)
        {
            sb.AppendLine($"Tags: {string.Join(", ", entry.Tags)}");
        }
        
        // Knowledge ID for reference
        sb.AppendLine($"`{entry.Id}`");
        sb.AppendLine();
        
        return sb.ToString();
    }
    
    private string GetFriendlyTimeAgo(DateTime date)
    {
        // Always convert to local time for user display - users want to see their local time
        var localDate = date.Kind == DateTimeKind.Utc ? date.ToLocalTime() : date;
        var diff = DateTime.Now - localDate;
        
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} minutes ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hours ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";
        if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)} weeks ago";
        return localDate.ToString("MMM dd, yyyy HH:mm");
    }
}

public class GetTimelineParams
{
    [ComponentModel.Description("Get items from the last N days (default: 7)")]
    public int? DaysAgo { get; set; }
    
    [ComponentModel.Description("Get items from the last N hours (alternative to days)")]
    public double? HoursAgo { get; set; }
    
    [ComponentModel.Description("Start date for timeline (optional, overrides days/hours)")]
    public DateTime? StartDate { get; set; }
    
    [ComponentModel.Description("End date for timeline (optional, overrides days/hours)")]
    public DateTime? EndDate { get; set; }
    
    [ComponentModel.Description("Filter by knowledge type (optional)")]
    public string? Type { get; set; }
    
    [ComponentModel.Description("Workspace to query (optional, defaults to current)")]
    public string? Workspace { get; set; }
    
    [ComponentModel.Description("Maximum items per time period group (default: 10)")]
    public int? MaxPerGroup { get; set; }
    
    [ComponentModel.Description("Maximum total results to retrieve (default: 500)")]
    public int? MaxResults { get; set; }
}

public class GetTimelineResult : ToolResultBase
{
    public override string Operation => ToolNames.ShowActivity;
    public List<TimelineEntry> Timeline { get; set; } = new();
    public string FormattedTimeline { get; set; } = string.Empty;
    public List<TimelineGroup> Groups { get; set; } = new();
    public int TotalCount { get; set; }
    public DateRange? DateRange { get; set; }
}

public class TimelineEntry
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Workspace { get; set; } = string.Empty;
    public string[]? Tags { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public int AccessCount { get; set; }
}

public class TimelineGroup
{
    public string Period { get; set; } = string.Empty;
    public int Count { get; set; }
    public Dictionary<string, int> Types { get; set; } = new();
}

public class DateRange
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}