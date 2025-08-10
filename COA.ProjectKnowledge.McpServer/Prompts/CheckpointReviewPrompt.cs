using COA.Mcp.Framework.Prompts;
using COA.Mcp.Protocol;

namespace COA.ProjectKnowledge.McpServer.Prompts;

/// <summary>
/// Interactive prompt to guide users through checkpoint review and session restoration
/// </summary>
public class CheckpointReviewPrompt : PromptBase
{
    public override string Name => "checkpoint-review";
    
    public override string Description => 
        "Guide for reviewing and restoring previous work sessions from checkpoints";

    public override List<PromptArgument> Arguments => new()
    {
        new PromptArgument
        {
            Name = "sessionId",
            Description = "Session ID to review",
            Required = true
        },
        new PromptArgument
        {
            Name = "checkpointId",
            Description = "Specific checkpoint ID (optional, uses latest if not provided)",
            Required = false
        },
        new PromptArgument
        {
            Name = "action",
            Description = "Action to take: review, restore, or compare",
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

        var sessionId = GetRequiredArgument<string>(arguments, "sessionId");
        var checkpointId = GetOptionalArgument<string>(arguments, "checkpointId", "latest");
        var action = GetOptionalArgument<string>(arguments, "action", "review");

        var messages = new List<PromptMessage>();

        // System message
        messages.Add(CreateSystemMessage(@"You are helping review and potentially restore a previous work session.
Your role is to:
1. Summarize what was accomplished in the session
2. Identify incomplete tasks or next steps
3. Help restore the working context
4. Provide actionable recommendations"));

        // User message based on action
        var userPrompt = action.ToLower() switch
        {
            "restore" => $@"I need to restore my work session '{sessionId}' from checkpoint {checkpointId}.

Please:
1. Show me what I was working on
2. List the active files and their status
3. Identify any incomplete tasks
4. Help me resume where I left off",

            "compare" => $@"I want to compare different checkpoints in session '{sessionId}'.

Please:
1. Show the progression of work across checkpoints
2. Highlight major changes or milestones
3. Identify any regression or lost work
4. Recommend which checkpoint to restore",

            _ => $@"I want to review session '{sessionId}' checkpoint {checkpointId}.

Please analyze:
1. What was accomplished
2. What remains to be done
3. Any issues or blockers noted
4. Recommendations for moving forward"
        };

        messages.Add(CreateUserMessage(userPrompt));

        // Assistant response
        var assistantResponse = @"I'll help you review this checkpoint and understand your previous work session.

Let me analyze the checkpoint to provide:

## Session Summary
I'll summarize what you were working on and the current state.

## Completed Tasks
A list of what was accomplished in this session.

## Active Files
The files you were working with and their current state.

## Incomplete Tasks
Any tasks that were started but not finished.

## Next Steps
Clear recommendations for resuming your work.

Would you like me to restore this session and set up your workspace?";

        messages.Add(CreateAssistantMessage(assistantResponse));

        return new GetPromptResult
        {
            Description = $"{action} checkpoint for session {sessionId}",
            Messages = messages
        };
    }
}