using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Framework.TokenOptimization.Models;
using COA.Mcp.Framework.TokenOptimization.Actions;
using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;
using COA.Mcp.Framework.Models;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Tools;
using Microsoft.Extensions.Logging;

namespace COA.ProjectKnowledge.McpServer.ResponseBuilders;

public class KnowledgeSearchResponseBuilder : ProjectKnowledgeResponseBuilder<List<Knowledge>, List<KnowledgeItem>>
{
    public KnowledgeSearchResponseBuilder(ILogger<KnowledgeSearchResponseBuilder> logger) : base(logger)
    {
    }
    
    public override Task<List<KnowledgeItem>> BuildResponseAsync(
        List<Knowledge> data,
        ResponseContext context)
    {
        var tokenBudget = CalculateTokenBudget(context);
        
        // Convert Knowledge to KnowledgeItem directly
        var allItems = data.Select(k => new KnowledgeItem
        {
            Id = k.Id,
            Type = k.Type,
            Content = TruncateContent(k.Content, 500),
            CreatedAt = k.CreatedAt,
            ModifiedAt = k.ModifiedAt,
            AccessCount = k.AccessCount,
            Tags = k.Tags,
            Status = k.Status,
            Priority = k.Priority
        }).ToList();
        
        // Estimate tokens for full data
        var fullDataTokens = TokenEstimator.EstimateCollection(allItems);
        _logger?.LogDebug("Full data tokens: {Tokens}, Budget: {Budget}", fullDataTokens, tokenBudget);
        
        // If data fits within budget, return all results
        if (fullDataTokens <= tokenBudget)
        {
            return Task.FromResult(allItems);
        }
        
        // Need to reduce data to fit budget
        var reducedItems = ReduceKnowledgeItems(allItems, tokenBudget);
        
        return Task.FromResult(reducedItems);
    }
    
    private List<KnowledgeItem> ReduceKnowledgeItems(List<KnowledgeItem> items, int tokenBudget)
    {
        var result = new List<KnowledgeItem>();
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
        
        return actions;
    }
}