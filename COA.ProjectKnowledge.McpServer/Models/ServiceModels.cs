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

// Timeline Models
public class TimelineRequest
{
    public string? Workspace { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? DaysAgo { get; set; }
    public double? HoursAgo { get; set; }
    public string? Type { get; set; }
    public int? MaxResults { get; set; }
}

public class TimelineResponse
{
    public bool Success { get; set; }
    public List<TimelineItem> Timeline { get; set; } = new();
    public int TotalCount { get; set; }
    public DateRange? DateRange { get; set; }
    public string? Error { get; set; }
}

public class TimelineItem
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string Workspace { get; set; } = string.Empty;
    public string[]? Tags { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public int AccessCount { get; set; }
}

public class DateRange
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

// Checkpoint Service Models
public class CreateCheckpointRequest
{
    public string Content { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string[]? ActiveFiles { get; set; }
}

public class CreateCheckpointResponse
{
    public bool Success { get; set; }
    public Checkpoint? Checkpoint { get; set; }
    public string? Error { get; set; }
}

public class GetCheckpointRequest
{
    public string? CheckpointId { get; set; }
    public string? SessionId { get; set; }
}

public class GetCheckpointResponse
{
    public bool Success { get; set; }
    public Checkpoint? Checkpoint { get; set; }
    public string? Error { get; set; }
}

public class ListCheckpointsRequest
{
    public string SessionId { get; set; } = string.Empty;
    public int? MaxResults { get; set; }
}

public class ListCheckpointsResponse
{
    public bool Success { get; set; }
    public List<Checkpoint> Checkpoints { get; set; } = new();
    public string? Error { get; set; }
}

// Checklist Service Models
public class CreateChecklistRequest
{
    public string Content { get; set; } = string.Empty;
    public string[] Items { get; set; } = Array.Empty<string>();
    public string? ParentChecklistId { get; set; }
}

public class CreateChecklistResponse
{
    public bool Success { get; set; }
    public Checklist? Checklist { get; set; }
    public string? Error { get; set; }
}

public class GetChecklistRequest
{
    public string ChecklistId { get; set; } = string.Empty;
}

public class GetChecklistResponse
{
    public bool Success { get; set; }
    public Checklist? Checklist { get; set; }
    public string? Error { get; set; }
}

public class UpdateChecklistItemRequest
{
    public string ChecklistId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public class UpdateChecklistItemResponse
{
    public bool Success { get; set; }
    public Checklist? Checklist { get; set; }
    public string? Error { get; set; }
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