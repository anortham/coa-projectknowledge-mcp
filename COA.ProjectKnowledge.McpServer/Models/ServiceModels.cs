namespace COA.ProjectKnowledge.McpServer.Models;

// Knowledge Service Models
public class StoreKnowledgeRequest
{
    public string Type { get; set; } = KnowledgeTypes.WorkNote;
    public string Content { get; set; } = string.Empty;
    public string[]? Tags { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public CodeSnippet[]? CodeSnippets { get; set; }
    public string[]? RelatedTo { get; set; }
    public string? Workspace { get; set; }
}

public class StoreKnowledgeResponse
{
    public bool Success { get; set; }
    public string? KnowledgeId { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class SearchKnowledgeRequest
{
    public string Query { get; set; } = string.Empty;
    public string? Workspace { get; set; }
    public int? MaxResults { get; set; }
}

public class CrossWorkspaceSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string[]? Workspaces { get; set; } // If null, search ALL workspaces
    public int? MaxResults { get; set; }
}

public class ExternalContribution
{
    public string Type { get; set; } = KnowledgeTypes.WorkNote;
    public string Content { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
}

public class SearchKnowledgeResponse
{
    public bool Success { get; set; }
    public List<KnowledgeSearchItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class KnowledgeSearchItem
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string[]? Tags { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public int AccessCount { get; set; }
}

public class CrossWorkspaceSearchItem : KnowledgeSearchItem
{
    public string Workspace { get; set; } = string.Empty;
}

public class TimelineRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? Workspace { get; set; }
    public string[]? Types { get; set; }
    public int MaxResults { get; set; } = 50;
    
    // Additional properties for GetTimelineTool compatibility
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? DaysAgo { get; set; }
    public double? HoursAgo { get; set; }
    public string? Type { get; set; }
}

public class TimelineResponse
{
    public bool Success { get; set; }
    public List<TimelineItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    
    // Additional properties for GetTimelineTool compatibility
    public List<TimelineItem> Timeline { get; set; } = new();
    public DateRange? DateRange { get; set; }
}

public class TimelineItem
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string[]? Tags { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public int AccessCount { get; set; }
    public string? Workspace { get; set; }
    
    // Additional property for GetTimelineTool compatibility
    public string Summary { get; set; } = string.Empty;
}

public class GetKnowledgeResponse
{
    public bool Success { get; set; }
    public Knowledge? Knowledge { get; set; }
    public string? Error { get; set; }
}

public class UpdateKnowledgeRequest
{
    public string Id { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string[]? Tags { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
}

public class UpdateKnowledgeResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class DeleteKnowledgeResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}


public class DateRange
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}


// Code snippet parameter model (for tools)
public class CodeSnippetParameter
{
    public string FilePath { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int? StartLine { get; set; }
    public int? EndLine { get; set; }
}