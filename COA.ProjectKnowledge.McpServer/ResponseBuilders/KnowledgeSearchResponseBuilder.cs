using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;
using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.TokenOptimization.Actions;
using COA.Mcp.Framework.Models;
using COA.ProjectKnowledge.McpServer.Models;
using Microsoft.Extensions.Logging;

namespace COA.ProjectKnowledge.McpServer.ResponseBuilders;

public class KnowledgeSearchResponseBuilder : BaseResponseBuilder<List<Knowledge>>
{
    private new readonly ILogger<KnowledgeSearchResponseBuilder> _logger;
    
    public KnowledgeSearchResponseBuilder(ILogger<KnowledgeSearchResponseBuilder> logger) : base(logger)
    {
        _logger = logger;
    }
    
    public override async Task<object> BuildResponseAsync(
        List<Knowledge> data,
        ResponseContext context)
    {
        var startTime = DateTime.UtcNow;
        var tokenBudget = CalculateTokenBudget(context);
        
        // Estimate tokens for full data
        var fullDataTokens = TokenEstimator.EstimateCollection(data);
        _logger.LogDebug("Full data tokens: {Tokens}, Budget: {Budget}", fullDataTokens, tokenBudget);
        
        // If data fits within budget, return full results
        if (fullDataTokens <= tokenBudget)
        {
            return new AIOptimizedResponse
            {
                Data = new AIResponseData
                {
                    Summary = $"Found {data.Count} knowledge items",
                    Results = data.Select(k => new
                    {
                        k.Id,
                        k.Type,
                        Content = k.Content.Length > 500 ? k.Content.Substring(0, 497) + "..." : k.Content,
                        k.CreatedAt,
                        k.Tags,
                        k.Status,
                        k.Priority
                    }).ToList(),
                    Count = data.Count
                },
                Insights = GenerateInsights(data, context.ResponseMode),
                Actions = GenerateActions(data, (int)(tokenBudget * 0.1)),
                Meta = CreateMetadata(startTime, false)
            };
        }
        
        // Need to reduce - allocate token budget
        var dataTokenBudget = (int)(tokenBudget * 0.7); // 70% for data
        var insightTokenBudget = (int)(tokenBudget * 0.15); // 15% for insights
        var actionTokenBudget = (int)(tokenBudget * 0.15); // 15% for actions
        
        // Reduce data to fit budget
        var reducedData = ReduceKnowledgeItems(data, dataTokenBudget);
        
        // Generate and reduce insights
        var insights = GenerateInsights(data, context.ResponseMode);
        var reducedInsights = ReduceInsights(insights, insightTokenBudget);
        
        // Generate and reduce actions
        var actions = GenerateActions(data, actionTokenBudget);
        var reducedActions = ReduceActions(actions, actionTokenBudget);
        
        var response = new AIOptimizedResponse
        {
            Data = new AIResponseData
            {
                Summary = $"Found {data.Count} knowledge items (showing {reducedData.Count})",
                Results = reducedData.Select(k => new
                {
                    k.Id,
                    k.Type,
                    Content = k.Content.Length > 500 ? k.Content.Substring(0, 497) + "..." : k.Content,
                    k.CreatedAt,
                    k.Tags,
                    k.Status,
                    k.Priority
                }).ToList(),
                Count = reducedData.Count,
                ExtensionData = new Dictionary<string, object>
                {
                    { "TotalCount", data.Count }
                }
            },
            Insights = reducedInsights,
            Actions = reducedActions,
            Meta = CreateMetadata(startTime, true)
        };
        
        // Update actual token estimate
        response.Meta.TokenInfo.Estimated = TokenEstimator.EstimateObject(response);
        
        return response;
    }
    
    private List<Knowledge> ReduceKnowledgeItems(List<Knowledge> items, int tokenBudget)
    {
        var result = new List<Knowledge>();
        var currentTokens = 0;
        
        // Prioritize by: Checkpoints > Checklists > ProjectInsights > TechnicalDebt > WorkNotes
        // Then by AccessCount and CreatedAt
        var prioritized = items
            .OrderBy(k => GetTypePriority(k.Type))
            .ThenByDescending(k => k.AccessCount)
            .ThenByDescending(k => k.CreatedAt)
            .ToList();
        
        foreach (var item in prioritized)
        {
            var itemTokens = TokenEstimator.EstimateObject(new
            {
                item.Id,
                item.Type,
                Content = item.Content.Length > 500 ? item.Content.Substring(0, 497) + "..." : item.Content,
                item.CreatedAt,
                item.Tags,
                item.Status,
                item.Priority
            });
            
            if (currentTokens + itemTokens <= tokenBudget)
            {
                result.Add(item);
                currentTokens += itemTokens;
            }
            else
            {
                break; // Budget exhausted
            }
        }
        
        return result;
    }
    
    private int GetTypePriority(string type)
    {
        return type switch
        {
            "Checkpoint" => 1,
            "Checklist" => 2,
            "ProjectInsight" => 3,
            "TechnicalDebt" => 4,
            "WorkNote" => 5,
            _ => 6
        };
    }
    
    protected override List<string> GenerateInsights(List<Knowledge> data, string responseMode)
    {
        var insights = new List<string>();
        
        if (data.Count == 0)
        {
            insights.Add("No knowledge items found - try broadening your search criteria");
            return insights;
        }
        
        // Type distribution insight
        var typeGroups = data.GroupBy(k => k.Type).OrderByDescending(g => g.Count()).ToList();
        if (typeGroups.Count > 1)
        {
            var topTypes = string.Join(", ", typeGroups.Take(3).Select(g => $"{g.Key} ({g.Count()})"));
            insights.Add($"Knowledge distribution: {topTypes}");
        }
        
        // Recent activity insight
        var recentItems = data.Where(k => k.CreatedAt > DateTime.UtcNow.AddDays(-7)).Count();
        if (recentItems > 0)
        {
            insights.Add($"{recentItems} items created in the last 7 days");
        }
        
        // Workspace distribution
        var workspaces = data.Where(k => !string.IsNullOrEmpty(k.Workspace))
            .GroupBy(k => k.Workspace)
            .OrderByDescending(g => g.Count())
            .ToList();
        if (workspaces.Count > 1)
        {
            insights.Add($"Knowledge spans {workspaces.Count} workspaces");
        }
        
        // High-priority items
        var highPriority = data.Count(k => k.Priority == "high");
        if (highPriority > 0)
        {
            insights.Add($"{highPriority} high-priority items require attention");
        }
        
        // Response mode specific
        if (responseMode == "summary")
        {
            insights.Add("Showing summary view - full results available via resource URI");
        }
        
        return insights;
    }
    
    protected override List<AIAction> GenerateActions(List<Knowledge> data, int tokenBudget)
    {
        var actions = new List<AIAction>();
        
        if (data.Count == 0)
        {
            actions.Add(new AIAction
            {
                Action = "store_knowledge",
                Description = "Create new knowledge entry",
                Category = "create",
                Priority = 10,
                Parameters = new Dictionary<string, object>
                {
                    { "type", "WorkNote" }
                }
            });
        }
        else
        {
            // If many results, suggest filtering
            if (data.Count > 20)
            {
                actions.Add(new AIAction
                {
                    Action = "find_knowledge",
                    Description = "Refine search with more specific criteria",
                    Category = "filter",
                    Priority = 9,
                    Parameters = new Dictionary<string, object>
                    {
                        { "query", "type:ProjectInsight" }
                    }
                });
            }
            
            // If checkpoints exist, suggest loading
            var checkpoints = data.Where(k => k.Type == "Checkpoint").Take(1).FirstOrDefault();
            if (checkpoints != null)
            {
                actions.Add(new AIAction
                {
                    Action = "load_checkpoint",
                    Description = "Resume from latest checkpoint",
                    Category = "navigate",
                    Priority = 8,
                    Parameters = new Dictionary<string, object>
                    {
                        { "checkpointId", checkpoints.Id }
                    }
                });
            }
            
            // If checklists exist, suggest viewing
            var checklists = data.Where(k => k.Type == "Checklist").Take(1).FirstOrDefault();
            if (checklists != null)
            {
                actions.Add(new AIAction
                {
                    Action = "view_checklist",
                    Description = "View checklist progress",
                    Category = "view",
                    Priority = 7,
                    Parameters = new Dictionary<string, object>
                    {
                        { "checklistId", checklists.Id }
                    }
                });
            }
            
            // Suggest exporting if many items
            if (data.Count > 10)
            {
                actions.Add(new AIAction
                {
                    Action = "export_knowledge",
                    Description = "Export results to Obsidian markdown",
                    Category = "export",
                    Priority = 6
                });
            }
        }
        
        // Keep only actions that fit in budget
        var result = new List<AIAction>();
        var currentTokens = 0;
        
        foreach (var action in actions.OrderByDescending(a => a.Priority))
        {
            var actionTokens = TokenEstimator.EstimateObject(action);
            if (currentTokens + actionTokens <= tokenBudget)
            {
                result.Add(action);
                currentTokens += actionTokens;
            }
        }
        
        return result;
    }
}