using COA.Mcp.Framework.TokenOptimization.ResponseBuilders;
using COA.Mcp.Framework.TokenOptimization.Models;
using Microsoft.Extensions.Logging;

namespace COA.ProjectKnowledge.McpServer.ResponseBuilders;

/// <summary>
/// Base class for ProjectKnowledge response builders with common functionality
/// </summary>
public abstract class ProjectKnowledgeResponseBuilder<TData, TResult> : BaseResponseBuilder<TData, TResult>
    where TResult : new()
{
    protected ProjectKnowledgeResponseBuilder(ILogger logger) : base(logger)
    {
    }

    /// <summary>
    /// Get priority for knowledge types to ensure consistent ordering
    /// </summary>
    protected int GetTypePriority(string type) => type switch
    {
        "TechnicalDebt" => 1,
        "ProjectInsight" => 2,
        "WorkNote" => 3,
        _ => 99
    };

    /// <summary>
    /// Format a timestamp for user display
    /// </summary>
    protected string FormatTimestamp(DateTime timestamp)
    {
        var localTime = timestamp.ToLocalTime();
        var now = DateTime.Now;
        var timeSpan = now - localTime;

        if (timeSpan.TotalMinutes < 1)
            return "just now";
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes}m ago";
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours}h ago";
        if (timeSpan.TotalDays < 7)
            return $"{(int)timeSpan.TotalDays}d ago";

        return localTime.ToString("MMM d, yyyy");
    }

    /// <summary>
    /// Truncate content for display with ellipsis
    /// </summary>
    protected string TruncateContent(string content, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        if (content.Length <= maxLength)
            return content;

        return content[..maxLength] + "...";
    }
}