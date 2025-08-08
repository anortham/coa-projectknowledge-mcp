using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class CreateChecklistTool : McpToolBase<CreateChecklistParams, CreateChecklistResult>
{
    private readonly ChecklistService _checklistService;
    
    public CreateChecklistTool(ChecklistService checklistService)
    {
        _checklistService = checklistService;
    }
    
    public override string Name => "create_checklist";
    public override string Description => "Create a new checklist with items to track";
    public override ToolCategory Category => ToolCategory.Resources;

    protected override async Task<CreateChecklistResult> ExecuteInternalAsync(CreateChecklistParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            var checklist = await _checklistService.CreateChecklistAsync(
                parameters.Content,
                parameters.Items?.ToList() ?? new List<string>(),
                parameters.ParentChecklistId);
            
            return new CreateChecklistResult
            {
                Success = true,
                ChecklistId = checklist.Id,
                ItemCount = checklist.Items.Count,
                CompletionPercentage = checklist.CompletionPercentage,
                Message = $"Created checklist with {checklist.Items.Count} items"
            };
        }
        catch (Exception ex)
        {
            return new CreateChecklistResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "CHECKLIST_CREATE_FAILED",
                    Message = $"Failed to create checklist: {ex.Message}"
                }
            };
        }
    }
}

public class CreateChecklistParams
{
    [Description("Content/description of the checklist")]
    public string Content { get; set; } = string.Empty;
    
    [Description("Array of checklist item contents")]
    public string[]? Items { get; set; }
    
    [Description("Parent checklist ID for nested checklists (optional)")]
    public string? ParentChecklistId { get; set; }
}

public class CreateChecklistResult : ToolResultBase
{
    public override string Operation => "create_checklist";
    public string? ChecklistId { get; set; }
    public int ItemCount { get; set; }
    public double CompletionPercentage { get; set; }
}