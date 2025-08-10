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

public class GetChecklistTool : McpToolBase<GetChecklistParams, GetChecklistResult>
{
    private readonly ChecklistService _checklistService;
    
    public GetChecklistTool(ChecklistService checklistService)
    {
        _checklistService = checklistService;
    }
    
    public override string Name => ToolNames.ViewChecklist;
    public override string Description => "Get a checklist with its current status";
    public override ToolCategory Category => ToolCategory.Query;

    protected override async Task<GetChecklistResult> ExecuteInternalAsync(GetChecklistParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            var checklist = await _checklistService.GetChecklistAsync(parameters.ChecklistId);
            
            if (checklist == null)
            {
                return new GetChecklistResult
                {
                    Success = false,
                    Error = new ErrorInfo
                    {
                        Code = "CHECKLIST_NOT_FOUND",
                        Message = "Checklist not found",
                        Recovery = new RecoveryInfo
                        {
                            Steps = new[]
                            {
                                "Verify checklist ID exists",
                                "Use create_checklist to create a new checklist",
                                "Check if checklist was deleted"
                            }
                        }
                    }
                };
            }
            
            return new GetChecklistResult
            {
                Success = true,
                Checklist = new ChecklistInfo
                {
                    Id = checklist.Id,
                    Content = checklist.Content,
                    ParentChecklistId = checklist.ParentChecklistId,
                    Items = checklist.Items.Select(i => new ChecklistItemInfo
                    {
                        Id = i.Id,
                        Content = i.Content,
                        IsCompleted = i.IsCompleted,
                        CompletedAt = i.CompletedAt,
                        Order = i.Order
                    }).ToList(),
                    CreatedAt = checklist.CreatedAt,
                    ModifiedAt = checklist.ModifiedAt,
                    CompletedCount = checklist.Items.Count(i => i.IsCompleted),
                    TotalCount = checklist.Items.Count,
                    CompletionPercentage = checklist.CompletionPercentage,
                    Status = checklist.GetStatus()
                }
            };
        }
        catch (Exception ex)
        {
            return new GetChecklistResult
            {
                Success = false,
                Error = ErrorHelpers.CreateChecklistError($"Failed to get checklist: {ex.Message}", "get")
            };
        }
    }
}

public class GetChecklistParams
{
    [FrameworkAttributes.Required(ErrorMessage = "Checklist ID is required")]
    [FrameworkAttributes.StringLength(50, ErrorMessage = "Checklist ID cannot exceed 50 characters")]

    [ComponentModel.Description("ID of the checklist to retrieve")]
    public string ChecklistId { get; set; } = string.Empty;
}

public class GetChecklistResult : ToolResultBase
{
    public override string Operation => "get_checklist";
    public ChecklistInfo? Checklist { get; set; }
}

public class ChecklistInfo
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ParentChecklistId { get; set; }
    public List<ChecklistItemInfo> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
    public double CompletionPercentage { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ChecklistItemInfo
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int Order { get; set; }
}