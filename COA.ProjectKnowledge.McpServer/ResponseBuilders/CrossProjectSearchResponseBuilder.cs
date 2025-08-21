using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.TokenOptimization.Actions;
using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;
using COA.Mcp.Framework.Models;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Tools;
using Microsoft.Extensions.Logging;

namespace COA.ProjectKnowledge.McpServer.ResponseBuilders;

public class CrossProjectSearchResponseBuilder : ProjectKnowledgeResponseBuilder<List<CrossWorkspaceSearchItem>, List<CrossProjectKnowledgeItem>>
{
    public CrossProjectSearchResponseBuilder(ILogger<CrossProjectSearchResponseBuilder> logger) : base(logger)
    {
    }
    
    public override Task<List<CrossProjectKnowledgeItem>> BuildResponseAsync(
        List<CrossWorkspaceSearchItem> data,
        ResponseContext context)
    {
        var tokenBudget = CalculateTokenBudget(context);
        
        // Convert CrossWorkspaceSearchItem to CrossProjectKnowledgeItem directly
        var allItems = data.Select(item => new CrossProjectKnowledgeItem
        {
            Id = item.Id,
            Type = item.Type,
            Content = item.Content.Length > 500 ? item.Content.Substring(0, 497) + "..." : item.Content,
            Workspace = item.Workspace,
            CreatedAt = item.CreatedAt,
            ModifiedAt = item.ModifiedAt,
            AccessCount = item.AccessCount,
            Tags = item.Tags,
            Status = item.Status,
            Priority = item.Priority
        }).ToList();
        
        // Estimate tokens for full data
        var fullDataTokens = TokenEstimator.EstimateCollection(allItems);
        _logger?.LogDebug("Full cross-project data tokens: {Tokens}, Budget: {Budget}", fullDataTokens, tokenBudget);
        
        // If data fits within budget, return all results
        if (fullDataTokens <= tokenBudget)
        {
            return Task.FromResult(allItems);
        }
        
        // Need to reduce data to fit budget
        var reducedItems = ReduceCrossProjectItems(allItems, tokenBudget);
        
        return Task.FromResult(reducedItems);
    }
    
    private List<CrossProjectKnowledgeItem> ReduceCrossProjectItems(List<CrossProjectKnowledgeItem> items, int tokenBudget)
    {
        var result = new List<CrossProjectKnowledgeItem>();
        var currentTokens = 0;
        
        // Prioritize by: ProjectInsights > TechnicalDebt > WorkNotes
        // Then by AccessCount and CreatedAt
        // Also prioritize diverse workspaces to show cross-project coverage
        var workspaceGroups = items.GroupBy(k => k.Workspace).ToList();
        var prioritized = new List<CrossProjectKnowledgeItem>();
        
        // Round-robin through workspaces to ensure diverse coverage
        var maxItemsPerWorkspace = Math.Max(1, items.Count / Math.Max(1, workspaceGroups.Count));
        
        foreach (var workspaceGroup in workspaceGroups.OrderBy(g => g.Key))
        {
            var workspaceItems = workspaceGroup
                .OrderBy(k => GetTypePriority(k.Type))
                .ThenByDescending(k => k.AccessCount)
                .ThenByDescending(k => k.CreatedAt)
                .Take(maxItemsPerWorkspace)
                .ToList();
            
            prioritized.AddRange(workspaceItems);
        }
        
        // Add any remaining items if we have space
        var remaining = items.Except(prioritized)
            .OrderBy(k => GetTypePriority(k.Type))
            .ThenByDescending(k => k.AccessCount)
            .ThenByDescending(k => k.CreatedAt);
        
        prioritized.AddRange(remaining);
        
        foreach (var item in prioritized)
        {
            var itemTokens = TokenEstimator.EstimateObject(item);
            
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
    
    protected override List<string> GenerateInsights(List<CrossWorkspaceSearchItem> data, string responseMode)
    {
        var insights = new List<string>();
        
        if (data.Count == 0)
        {
            insights.Add("No cross-project knowledge found - try broadening your search criteria");
            return insights;
        }
        
        // Workspace distribution insight
        var workspaceGroups = data.GroupBy(k => k.Workspace).OrderByDescending(g => g.Count()).ToList();
        if (workspaceGroups.Count > 1)
        {
            var topWorkspaces = string.Join(", ", workspaceGroups.Take(3).Select(g => $"{g.Key} ({g.Count()})"));
            insights.Add($"Knowledge spans {workspaceGroups.Count} workspaces: {topWorkspaces}");
        }
        
        // Type distribution insight
        var typeGroups = data.GroupBy(k => k.Type).OrderByDescending(g => g.Count()).ToList();
        if (typeGroups.Count > 1)
        {
            var topTypes = string.Join(", ", typeGroups.Take(2).Select(g => $"{g.Key} ({g.Count()})"));
            insights.Add($"Most common types: {topTypes}");
        }
        
        return insights;
    }
    
    protected override List<AIAction> GenerateActions(List<CrossWorkspaceSearchItem> data, int tokenBudget)
    {
        var actions = new List<AIAction>();
        
        if (data.Count == 0)
        {
            actions.Add(new AIAction
            {
                Action = "store_knowledge",
                Description = "Create new cross-project knowledge entry",
                Category = "create",
                Priority = 10,
                Parameters = new Dictionary<string, object>
                {
                    { "type", "ProjectInsight" }
                }
            });
        }
        
        return actions;
    }
}