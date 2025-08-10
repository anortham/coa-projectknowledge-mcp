using COA.Mcp.Framework.Prompts;
using COA.Mcp.Protocol;

namespace COA.ProjectKnowledge.McpServer.Prompts;

/// <summary>
/// Interactive prompt to guide users through structured knowledge capture
/// </summary>
public class KnowledgeCapturePrompt : PromptBase
{
    public override string Name => "knowledge-capture";
    
    public override string Description => 
        "Interactive guide for capturing structured knowledge, insights, and decisions";

    public override List<PromptArgument> Arguments => new()
    {
        new PromptArgument
        {
            Name = "type",
            Description = "Type of knowledge to capture (ProjectInsight, TechnicalDebt, WorkNote)",
            Required = true
        },
        new PromptArgument
        {
            Name = "workspace",
            Description = "Project workspace (optional, defaults to current)",
            Required = false
        },
        new PromptArgument
        {
            Name = "context",
            Description = "Current context or subject matter",
            Required = false
        }
    };

    public override async Task<GetPromptResult> RenderAsync(
        Dictionary<string, object>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateArguments(arguments);
        if (!validation.IsValid)
        {
            return new GetPromptResult
            {
                Description = "Invalid arguments",
                Messages = new List<PromptMessage>
                {
                    CreateSystemMessage($"Error: {string.Join(", ", validation.Errors)}")
                }
            };
        }

        var type = GetRequiredArgument<string>(arguments, "type");
        var workspace = GetOptionalArgument<string>(arguments, "workspace", "current project");
        var context = GetOptionalArgument<string>(arguments, "context", "");

        var messages = new List<PromptMessage>();

        // System message based on type
        var systemPrompt = type.ToLower() switch
        {
            "projectinsight" => @"You are helping capture an important project insight or architectural decision.
Focus on:
- The insight or decision itself
- Why it matters
- Impact on the project
- Future considerations",
            
            "technicaldebt" => @"You are helping document technical debt or a known issue.
Focus on:
- What the issue is
- Current impact
- Proposed solution
- Priority and timeline",
            
            "worknote" => @"You are helping capture a work note or observation.
This can be informal but should be clear and actionable if needed.",
            
            _ => "You are helping capture knowledge for future reference."
        };

        messages.Add(CreateSystemMessage(systemPrompt));

        // User message with context
        var userPrompt = type.ToLower() switch
        {
            "projectinsight" => GenerateProjectInsightPrompt(context, workspace),
            "technicaldebt" => GenerateTechnicalDebtPrompt(context, workspace),
            "worknote" => GenerateWorkNotePrompt(context, workspace),
            _ => $"Help me capture knowledge about: {context}"
        };

        messages.Add(CreateUserMessage(userPrompt));

        // Assistant response template
        var assistantTemplate = type.ToLower() switch
        {
            "projectinsight" => @"I'll help you capture this project insight. Let me structure it properly:

## Insight
[Main insight or decision]

## Context
[Why this came up and relevant background]

## Impact
[How this affects the project]

## Recommendations
[Next steps or considerations]

Please provide the details, and I'll help format them appropriately.",

            "technicaldebt" => @"I'll help you document this technical debt item. Let me structure it:

## Issue Description
[What needs to be fixed or improved]

## Current Impact
[How it's affecting the system now]

## Proposed Solution
[How to address it]

## Priority & Timeline
[When it should be addressed]

Please describe the technical debt, and I'll help organize it properly.",

            "worknote" => @"I'll help you capture this work note. 

What specific observation, task, or information would you like to record?",

            _ => "Please describe what you'd like to capture."
        };

        messages.Add(CreateAssistantMessage(assistantTemplate));

        return new GetPromptResult
        {
            Description = $"Capture {type} for {workspace}",
            Messages = messages
        };
    }

    private string GenerateProjectInsightPrompt(string context, string workspace)
    {
        if (!string.IsNullOrEmpty(context))
        {
            return $@"I've discovered something important about {context} in {workspace}.

Help me document this insight properly, including:
- What the insight is
- Why it matters
- How it impacts our architecture or approach
- What we should consider going forward";
        }

        return $@"I need to document an important insight or architectural decision for {workspace}.

What aspect of the project would you like to capture an insight about?
- Architecture patterns
- Technology choices  
- Design decisions
- Performance findings
- Security considerations
- Other discoveries";
    }

    private string GenerateTechnicalDebtPrompt(string context, string workspace)
    {
        if (!string.IsNullOrEmpty(context))
        {
            return $@"I've identified technical debt related to {context} in {workspace}.

Help me document:
- The specific issue or shortcut taken
- Current negative impacts
- Recommended fix or refactoring
- Priority level (critical/high/medium/low)";
        }

        return $@"I need to document technical debt in {workspace}.

What type of technical debt are you seeing?
- Code that needs refactoring
- Missing tests or documentation
- Performance bottlenecks
- Security vulnerabilities
- Outdated dependencies
- Architectural issues";
    }

    private string GenerateWorkNotePrompt(string context, string workspace)
    {
        if (!string.IsNullOrEmpty(context))
        {
            return $@"I want to record a work note about {context} in {workspace}.

What would you like to note about this?";
        }

        return $@"I want to record a work note for {workspace}.

What would you like to capture?
- Current task or progress
- Blocker or issue encountered
- Question or uncertainty
- Idea for improvement
- Meeting note or decision
- Other observation";
    }
}