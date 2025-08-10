using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Constants;
using COA.ProjectKnowledge.McpServer.Helpers;
using System.ComponentModel;
using Microsoft.Extensions.Logging;

// Use framework attributes with aliases to avoid conflicts
using FrameworkAttributes = COA.Mcp.Framework.Attributes;
using ComponentModel = System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class UpdateChecklistItemTool : McpToolBase<UpdateChecklistItemParams, UpdateChecklistItemResult>
{
    private readonly ChecklistService _checklistService;
    private readonly ExecutionContextService _contextService;
    private readonly ILogger<UpdateChecklistItemTool> _logger;
    
    public UpdateChecklistItemTool(
        ChecklistService checklistService,
        ExecutionContextService contextService,
        ILogger<UpdateChecklistItemTool> logger)
    {
        _checklistService = checklistService;
        _contextService = contextService;
        _logger = logger;
    }
    
    public override string Name => ToolNames.UpdateTask;
    public override string Description => "Update the status of a checklist item";
    public override ToolCategory Category => ToolCategory.Resources;

    protected override async Task<UpdateChecklistItemResult> ExecuteInternalAsync(UpdateChecklistItemParams parameters, CancellationToken cancellationToken)
    {
        // Create execution context for tracking
        var customData = new Dictionary<string, object?>
        {
            ["ChecklistId"] = parameters.ChecklistId,
            ["ItemId"] = parameters.ItemId,
            ["IsCompleted"] = parameters.IsCompleted
        };
        
        return await _contextService.RunWithContextAsync(
            Name,
            async (context) => await ExecuteWithContextAsync(parameters, context, cancellationToken),
            customData: customData);
    }
    
    private async Task<UpdateChecklistItemResult> ExecuteWithContextAsync(
        UpdateChecklistItemParams parameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
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
                    Error = ErrorHelpers.CreateChecklistError("Checklist or item not found", "update")
                };
            }
            
            // Get the updated checklist to calculate completion percentage
            var checklist = await _checklistService.GetChecklistAsync(parameters.ChecklistId);
            
            context.CustomData["CompletionPercentage"] = checklist?.CompletionPercentage ?? 0;
            
            return new UpdateChecklistItemResult
            {
                Success = true,
                CompletionPercentage = checklist?.CompletionPercentage ?? 0,
                Message = $"Item marked as {(parameters.IsCompleted ? "completed" : "pending")}. Checklist is {checklist?.CompletionPercentage:F0}% complete."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update checklist item");
            context.CustomData["Error"] = true;
            
            return new UpdateChecklistItemResult
            {
                Success = false,
                Error = ErrorHelpers.CreateChecklistError($"Failed to update checklist item: {ex.Message}", "update")
            };
        }
    }
}

public class UpdateChecklistItemParams
{
    [FrameworkAttributes.Required(ErrorMessage = "Checklist ID is required")]
    [FrameworkAttributes.StringLength(50, ErrorMessage = "Checklist ID cannot exceed 50 characters")]

    [ComponentModel.Description("ID of the checklist")]
    public string ChecklistId { get; set; } = string.Empty;
    
    [FrameworkAttributes.Required(ErrorMessage = "Item ID is required")]
    [FrameworkAttributes.StringLength(50, ErrorMessage = "Item ID cannot exceed 50 characters")]

    
    [ComponentModel.Description("ID of the item to update")]
    public string ItemId { get; set; } = string.Empty;
    
    [FrameworkAttributes.Required(ErrorMessage = "IsCompleted is required")]

    
    [ComponentModel.Description("Whether the item is completed")]
    public bool IsCompleted { get; set; }
}

public class UpdateChecklistItemResult : ToolResultBase
{
    public override string Operation => "update_checklist_item";
    public double CompletionPercentage { get; set; }
}