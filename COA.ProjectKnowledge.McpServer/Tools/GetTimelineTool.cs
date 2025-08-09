using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Constants;
using System.ComponentModel;
using System.Text;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class GetTimelineTool : McpToolBase<GetTimelineParams, GetTimelineResult>
{
    private readonly KnowledgeService _knowledgeService;
    
    public GetTimelineTool(KnowledgeService knowledgeService)
    {
        _knowledgeService = knowledgeService;
    }
    
    public override string Name => ToolNames.ShowActivity;
    public override string Description => ToolDescriptions.ShowActivity;
    public override ToolCategory Category => ToolCategory.Query;

    protected override async Task<GetTimelineResult> ExecuteInternalAsync(GetTimelineParams parameters, CancellationToken cancellationToken)
    {
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
                    Error = new ErrorInfo { Code = "TIMELINE_FAILED", Message = response.Error ?? "Failed to get timeline" }
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
            
            return new GetTimelineResult
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
        }
        catch (Exception ex)
        {
            return new GetTimelineResult
            {
                Success = false,
                Timeline = new List<TimelineEntry>(),
                Error = new ErrorInfo
                {
                    Code = "TIMELINE_FAILED",
                    Message = $"Failed to get timeline: {ex.Message}"
                }
            };
        }
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
    [Description("Get items from the last N days (default: 7)")]
    public int? DaysAgo { get; set; }
    
    [Description("Get items from the last N hours (alternative to days)")]
    public double? HoursAgo { get; set; }
    
    [Description("Start date for timeline (optional, overrides days/hours)")]
    public DateTime? StartDate { get; set; }
    
    [Description("End date for timeline (optional, overrides days/hours)")]
    public DateTime? EndDate { get; set; }
    
    [Description("Filter by knowledge type (optional)")]
    public string? Type { get; set; }
    
    [Description("Workspace to query (optional, defaults to current)")]
    public string? Workspace { get; set; }
    
    [Description("Maximum items per time period group (default: 10)")]
    public int? MaxPerGroup { get; set; }
    
    [Description("Maximum total results to retrieve (default: 500)")]
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