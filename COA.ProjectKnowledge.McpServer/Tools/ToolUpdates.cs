// This file contains the updated error handling for all tools
// Copy these implementations to replace the existing error handling in each tool

// For CreateCheckpointTool.cs - Add to using statements:
// using COA.ProjectKnowledge.McpServer.Helpers;

// Replace the catch block with:
/*
catch (Exception ex)
{
    return new CreateCheckpointResult
    {
        Success = false,
        Error = ErrorHelpers.CreateCheckpointError($"Failed to create checkpoint: {ex.Message}", "create")
    };
}
*/

// For GetCheckpointTool.cs - Add to using statements:
// using COA.ProjectKnowledge.McpServer.Helpers;

// Replace error handling with:
/*
if (checkpoint == null)
{
    return new GetCheckpointResult
    {
        Success = false,
        Error = ErrorHelpers.CreateCheckpointError($"Checkpoint {parameters.CheckpointId ?? "latest"} not found", "get")
    };
}

// In catch block:
Error = ErrorHelpers.CreateCheckpointError($"Failed to get checkpoint: {ex.Message}", "get")
*/

// For ListCheckpointsTool.cs - Add to using statements:
// using COA.ProjectKnowledge.McpServer.Helpers;

// Replace catch block with:
/*
Error = ErrorHelpers.CreateCheckpointError($"Failed to list checkpoints: {ex.Message}", "list")
*/

// For CreateChecklistTool.cs - Add to using statements:
// using COA.ProjectKnowledge.McpServer.Helpers;

// Replace error handling with:
/*
if (!response.Success)
{
    Error = ErrorHelpers.CreateChecklistError(response.Error ?? "Failed to create checklist", "create")
}

// In catch block:
Error = ErrorHelpers.CreateChecklistError($"Failed to create checklist: {ex.Message}", "create")
*/

// For UpdateChecklistItemTool.cs - Add to using statements:
// using COA.ProjectKnowledge.McpServer.Helpers;

// Replace error handling with:
/*
if (!response.Success)
{
    Error = ErrorHelpers.CreateChecklistError(response.Error ?? "Failed to update checklist item", "update")
}

// In catch block:
Error = ErrorHelpers.CreateChecklistError($"Failed to update checklist item: {ex.Message}", "update")
*/

// For GetChecklistTool.cs - Add to using statements:
// using COA.ProjectKnowledge.McpServer.Helpers;

// Replace error handling with:
/*
if (!response.Success)
{
    Error = ErrorHelpers.CreateChecklistError(response.Error ?? "Failed to get checklist", "get")
}

// In catch block:
Error = ErrorHelpers.CreateChecklistError($"Failed to get checklist: {ex.Message}", "get")
*/

// For CreateRelationshipTool.cs - Add to using statements:
// using COA.ProjectKnowledge.McpServer.Helpers;

// Replace error handling with:
/*
if (!response.Success)
{
    Error = ErrorHelpers.CreateRelationshipError(response.Error ?? "Failed to create relationship", "create")
}

// In catch block:
Error = ErrorHelpers.CreateRelationshipError($"Failed to create relationship: {ex.Message}", "create")
*/

// For GetRelationshipsTool.cs - Add to using statements:
// using COA.ProjectKnowledge.McpServer.Helpers;

// Replace error handling with:
/*
if (!response.Success)
{
    Error = ErrorHelpers.CreateRelationshipError(response.Error ?? "Failed to get relationships", "get")
}

// In catch block:
Error = ErrorHelpers.CreateRelationshipError($"Failed to get relationships: {ex.Message}", "get")
*/

// For GetTimelineTool.cs - Add to using statements:
// using COA.ProjectKnowledge.McpServer.Helpers;
// using COA.ProjectKnowledge.McpServer.Resources;

// Add ResourceProvider dependency:
/*
private readonly KnowledgeResourceProvider _resourceProvider;

public GetTimelineTool(
    KnowledgeService knowledgeService,
    KnowledgeResourceProvider resourceProvider)
{
    _knowledgeService = knowledgeService;
    _resourceProvider = resourceProvider;
}
*/

// Replace error handling with:
/*
// In the success path, check for large results:
if (response.Items.Count > 50)
{
    var timelineId = $"{DateTime.UtcNow:yyyyMMddHHmmss}-timeline";
    var resourceUri = _resourceProvider.StoreAsResource(
        "timeline",
        timelineId,
        response.Items,
        $"Timeline with {response.Items.Count} items");
    
    return new GetTimelineResult
    {
        Success = true,
        Items = response.Items.Take(20).ToList(), // Preview
        TotalCount = response.Items.Count,
        ResourceUri = resourceUri,
        Message = $"Timeline with {response.Items.Count} items. Full data at: {resourceUri}",
        Meta = new ToolExecutionMetadata
        {
            Mode = "resource",
            Truncated = true
        }
    };
}

// In error cases:
Error = ErrorHelpers.CreateTimelineError(response.Error ?? "Failed to get timeline")

// In catch block:
Error = ErrorHelpers.CreateTimelineError($"Failed to get timeline: {ex.Message}")
*/

// For GetWorkspacesTool.cs - Add to using statements:
// using COA.ProjectKnowledge.McpServer.Helpers;

// Replace error handling with:
/*
// In catch block:
Error = ErrorHelpers.CreateWorkspaceError($"Failed to get workspaces: {ex.Message}")
*/