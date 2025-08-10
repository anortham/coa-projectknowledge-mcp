using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Exceptions;
using COA.Mcp.Framework.Interfaces;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Constants;
using COA.ProjectKnowledge.McpServer.Helpers;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
// Use framework attributes with aliases to avoid conflicts
using FrameworkAttributes = COA.Mcp.Framework.Attributes;
using ComponentModel = System.ComponentModel;

namespace COA.ProjectKnowledge.McpServer.Tools;

public class CreateChecklistTool : McpToolBase<CreateChecklistParams, CreateChecklistResult>
{
    private readonly ChecklistService _checklistService;
    private readonly ILogger<CreateChecklistTool> _logger;
    
    public CreateChecklistTool(ChecklistService checklistService, ILogger<CreateChecklistTool> logger)
    {
        _checklistService = checklistService;
        _logger = logger;
    }
    
    public override string Name => ToolNames.CreateChecklist;
    public override string Description => ToolDescriptions.CreateChecklist;
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
        catch (ArgumentException ex)
        {
            throw new ParameterValidationException(
                ValidationResult.Failure("parameters", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create checklist");
            throw new ToolExecutionException(
                Name,
                $"Failed to create checklist: {ex.Message}",
                ex);
        }
    }
}

public class CreateChecklistParams
{
    [FrameworkAttributes.Required(ErrorMessage = "Content is required")]
    [FrameworkAttributes.StringLength(5000, ErrorMessage = "Content cannot exceed 5000 characters")]
    [ComponentModel.Description("Content/description of the checklist")]
    public string Content { get; set; } = string.Empty;
    
    [ComponentModel.Description("Array of checklist item contents")]
    public string[]? Items { get; set; }
    
    [FrameworkAttributes.StringLength(50, ErrorMessage = "Parent checklist ID cannot exceed 50 characters")]
    [ComponentModel.Description("Parent checklist ID for nested checklists (optional)")]
    public string? ParentChecklistId { get; set; }
}

public class CreateChecklistResult : ToolResultBase
{
    public override string Operation => ToolNames.CreateChecklist;
    public string? ChecklistId { get; set; }
    public int ItemCount { get; set; }
    public double CompletionPercentage { get; set; }
}