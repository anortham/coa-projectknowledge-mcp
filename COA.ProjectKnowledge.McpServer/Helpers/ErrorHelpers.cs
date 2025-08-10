using COA.Mcp.Framework.Models;
using COA.ProjectKnowledge.McpServer.Constants;

namespace COA.ProjectKnowledge.McpServer.Helpers;

/// <summary>
/// Helper class for creating consistent error responses with recovery information
/// </summary>
public static class ErrorHelpers
{
    public static ErrorInfo CreateStoreError(string message)
    {
        return new ErrorInfo
        {
            Code = "STORE_FAILED",
            Message = message,
            Recovery = new RecoveryInfo
            {
                Steps = new[]
                {
                    "Verify the knowledge type is valid (Checkpoint, Checklist, TechnicalDebt, ProjectInsight, WorkNote)",
                    "Ensure content is not empty",
                    "Check if database is accessible",
                    "Verify workspace permissions"
                },
                SuggestedActions = new List<SuggestedAction>
                {
                    new SuggestedAction
                    {
                        Tool = ToolNames.FindKnowledge,
                        Description = "Search for similar existing knowledge",
                        Parameters = new { query = "recent" }
                    }
                }
            }
        };
    }

    public static ErrorInfo CreateSearchError(string message)
    {
        return new ErrorInfo
        {
            Code = "SEARCH_FAILED",
            Message = message,
            Recovery = new RecoveryInfo
            {
                Steps = new[]
                {
                    "Try a simpler search query",
                    "Remove special characters from search",
                    "Check if workspace name is correct",
                    "Verify database connectivity"
                },
                SuggestedActions = new List<SuggestedAction>
                {
                    new SuggestedAction
                    {
                        Tool = ToolNames.DiscoverProjects,
                        Description = "List available workspaces",
                        Parameters = new { }
                    },
                    new SuggestedAction
                    {
                        Tool = ToolNames.ShowActivity,
                        Description = "View recent activity",
                        Parameters = new { hoursAgo = 24 }
                    }
                }
            }
        };
    }

    public static ErrorInfo CreateCheckpointError(string message, string operation)
    {
        return new ErrorInfo
        {
            Code = "CHECKPOINT_FAILED",
            Message = message,
            Recovery = new RecoveryInfo
            {
                Steps = operation switch
                {
                    "create" => new[]
                    {
                        "Ensure content is provided",
                        "Verify session ID format if specified",
                        "Check database connectivity",
                        "Ensure sufficient disk space"
                    },
                    "get" => new[]
                    {
                        "Verify checkpoint ID exists",
                        "Check if session ID is correct",
                        "Use list_checkpoints to find valid IDs",
                        "Ensure checkpoint hasn't been deleted"
                    },
                    _ => new[]
                    {
                        "Check checkpoint operation parameters",
                        "Verify database connectivity",
                        "Review checkpoint permissions"
                    }
                },
                SuggestedActions = new List<SuggestedAction>
                {
                    new SuggestedAction
                    {
                        Tool = ToolNames.ListCheckpoints,
                        Description = "List available checkpoints",
                        Parameters = new { maxResults = 10 }
                    }
                }
            }
        };
    }

    public static ErrorInfo CreateChecklistError(string message, string operation)
    {
        return new ErrorInfo
        {
            Code = "CHECKLIST_FAILED",
            Message = message,
            Recovery = new RecoveryInfo
            {
                Steps = operation switch
                {
                    "create" => new[]
                    {
                        "Provide checklist content/description",
                        "Include at least one checklist item",
                        "Verify parent checklist ID if nesting",
                        "Check database connectivity"
                    },
                    "update" => new[]
                    {
                        "Verify checklist ID exists",
                        "Check item ID is valid",
                        "Ensure checklist hasn't been deleted",
                        "Verify update permissions"
                    },
                    "get" => new[]
                    {
                        "Verify checklist ID exists",
                        "Use find_knowledge to search for checklists",
                        "Check if checklist is in current workspace"
                    },
                    _ => new[]
                    {
                        "Check checklist parameters",
                        "Verify database connectivity"
                    }
                },
                SuggestedActions = new List<SuggestedAction>
                {
                    new SuggestedAction
                    {
                        Tool = ToolNames.FindKnowledge,
                        Description = "Search for checklists",
                        Parameters = new { query = "type:Checklist" }
                    }
                }
            }
        };
    }

    public static ErrorInfo CreateRelationshipError(string message, string operation)
    {
        return new ErrorInfo
        {
            Code = "RELATIONSHIP_FAILED",
            Message = message,
            Recovery = new RecoveryInfo
            {
                Steps = operation switch
                {
                    "create" => new[]
                    {
                        "Verify both knowledge IDs exist",
                        "Check relationship type is valid",
                        "Ensure IDs are different",
                        "Verify relationship doesn't already exist"
                    },
                    "get" => new[]
                    {
                        "Verify knowledge ID exists",
                        "Check direction parameter (from, to, or both)",
                        "Ensure knowledge item has relationships"
                    },
                    _ => new[]
                    {
                        "Check relationship parameters",
                        "Verify database connectivity"
                    }
                },
                SuggestedActions = new List<SuggestedAction>
                {
                    new SuggestedAction
                    {
                        Tool = ToolNames.FindKnowledge,
                        Description = "Find knowledge items to link",
                        Parameters = new { query = "recent", maxResults = 10 }
                    }
                }
            }
        };
    }

    public static ErrorInfo CreateTimelineError(string message)
    {
        return new ErrorInfo
        {
            Code = "TIMELINE_FAILED",
            Message = message,
            Recovery = new RecoveryInfo
            {
                Steps = new[]
                {
                    "Check date parameters are valid",
                    "Ensure time range is reasonable",
                    "Verify workspace exists if specified",
                    "Check database connectivity"
                },
                SuggestedActions = new List<SuggestedAction>
                {
                    new SuggestedAction
                    {
                        Tool = ToolNames.ShowActivity,
                        Description = "Try with default parameters",
                        Parameters = new { daysAgo = 7 }
                    },
                    new SuggestedAction
                    {
                        Tool = ToolNames.DiscoverProjects,
                        Description = "List available workspaces",
                        Parameters = new { }
                    }
                }
            }
        };
    }

    public static ErrorInfo CreateWorkspaceError(string message)
    {
        return new ErrorInfo
        {
            Code = "WORKSPACE_FAILED",
            Message = message,
            Recovery = new RecoveryInfo
            {
                Steps = new[]
                {
                    "Check database connectivity",
                    "Ensure at least one knowledge item exists",
                    "Verify workspace detection is working",
                    "Try storing knowledge first"
                },
                SuggestedActions = new List<SuggestedAction>
                {
                    new SuggestedAction
                    {
                        Tool = ToolNames.StoreKnowledge,
                        Description = "Store initial knowledge",
                        Parameters = new { 
                            type = "WorkNote",
                            content = "Initial workspace setup"
                        }
                    }
                }
            }
        };
    }
}