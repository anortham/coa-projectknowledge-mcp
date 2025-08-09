using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class UpdateChecklistItemTool : McpToolBase<UpdateChecklistItemParams, UpdateChecklistItemResult>
{
    private readonly ChecklistService _checklistService;
    
    public UpdateChecklistItemTool(ChecklistService checklistService)
    {
        _checklistService = checklistService;
    }
    
    public override string Name => "update_checklist_item";
    public override string Description => "Update the status of a checklist item";
    public override ToolCategory Category => ToolCategory.Resources;

    protected override async Task<UpdateChecklistItemResult> ExecuteInternalAsync(UpdateChecklistItemParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _checklistService.UpdateChecklistItemAsync(
                parameters.ChecklistId,
                parameters.ItemId,
                parameters.IsCompleted);
            
            if (!updated)
            {
                return new UpdateChecklistItemResult
                {
                    Success = false,
                    Error = new ErrorInfo
                    {
                        Code = "ITEM_NOT_FOUND",
                        Message = "Checklist or item not found"
                    }
                };
            }
            
            // Get the updated checklist to calculate completion percentage
            var checklist = await _checklistService.GetChecklistAsync(parameters.ChecklistId);
            
            return new UpdateChecklistItemResult
            {
                Success = true,
                CompletionPercentage = checklist?.CompletionPercentage ?? 0,
                Message = $"Item marked as {(parameters.IsCompleted ? "completed" : "pending")}. Checklist is {checklist?.CompletionPercentage:F0}% complete."
            };
        }
        catch (Exception ex)
        {
            return new UpdateChecklistItemResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "UPDATE_FAILED",
                    Message = $"Failed to update checklist item: {ex.Message}"
                }
            };
        }
    }
}

public class UpdateChecklistItemParams
{
    [Description("ID of the checklist")]
    public string ChecklistId { get; set; } = string.Empty;
    
    [Description("ID of the item to update")]
    public string ItemId { get; set; } = string.Empty;
    
    [Description("Whether the item is completed")]
    public bool IsCompleted { get; set; }
}

public class UpdateChecklistItemResult : ToolResultBase
{
    public override string Operation => "update_checklist_item";
    public double CompletionPercentage { get; set; }
}