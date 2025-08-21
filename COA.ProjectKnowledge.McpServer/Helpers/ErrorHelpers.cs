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
                    "Verify the knowledge type is valid (TechnicalDebt, ProjectInsight, WorkNote)",
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