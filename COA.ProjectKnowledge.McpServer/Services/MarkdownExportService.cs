using COA.ProjectKnowledge.McpServer.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace COA.ProjectKnowledge.McpServer.Services;

/// <summary>
/// Service for exporting knowledge to Obsidian-compatible markdown files
/// </summary>
public class MarkdownExportService
{
    private readonly KnowledgeService _knowledgeService;
    private readonly RelationshipService _relationshipService;
    private readonly IPathResolutionService _pathResolution;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MarkdownExportService> _logger;
    
    public MarkdownExportService(
        KnowledgeService knowledgeService,
        RelationshipService relationshipService,
        IPathResolutionService pathResolution,
        IConfiguration configuration,
        ILogger<MarkdownExportService> logger)
    {
        _knowledgeService = knowledgeService;
        _relationshipService = relationshipService;
        _pathResolution = pathResolution;
        _configuration = configuration;
        _logger = logger;
    }
    
    /// <summary>
    /// Export all knowledge to markdown files with Obsidian-style links
    /// </summary>
    public async Task<ExportResult> ExportToMarkdownAsync(
        string? outputPath = null, 
        string? workspace = null,
        ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        var result = new ExportResult { StartedAt = DateTime.UtcNow };
        
        try
        {
            // Determine output directory
            outputPath ??= Path.Combine(_pathResolution.GetExportsPath(), 
                $"export_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
            _pathResolution.EnsureDirectoryExists(outputPath);
            
            // Get all knowledge items
            var query = options.FilterByType != null 
                ? $"type:{options.FilterByType}"
                : "*";
            var items = await _knowledgeService.SearchAsync(query, workspace, maxResults: 1000);
            
            _logger.LogInformation("Exporting {Count} knowledge items to {Path}", 
                items.Count, outputPath);
            
            // Create folder structure by type
            var typeGroups = items.GroupBy(k => k.Type);
            foreach (var group in typeGroups)
            {
                var typePath = Path.Combine(outputPath, SanitizeFolderName(group.Key));
                _pathResolution.EnsureDirectoryExists(typePath);
                
                foreach (var item in group)
                {
                    var success = await ExportItemAsync(item, typePath, items, options);
                    if (success)
                        result.ExportedCount++;
                    else
                        result.FailedCount++;
                }
            }
            
            // Create index file
            if (options.CreateIndex)
            {
                await CreateIndexFileAsync(outputPath, items);
            }
            
            // Create graph view file for relationships
            if (options.IncludeRelationships)
            {
                await CreateGraphFileAsync(outputPath, items);
            }
            
            result.Success = true;
            result.OutputPath = outputPath;
            result.CompletedAt = DateTime.UtcNow;
            result.Message = $"Exported {result.ExportedCount} items to {outputPath}";
            
            _logger.LogInformation("Export completed: {Exported} succeeded, {Failed} failed", 
                result.ExportedCount, result.FailedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed");
            result.Success = false;
            result.Message = $"Export failed: {ex.Message}";
        }
        
        return result;
    }
    
    private async Task<bool> ExportItemAsync(
        Knowledge item, 
        string typePath, 
        List<Knowledge> allItems,
        ExportOptions options)
    {
        try
        {
            var fileName = GenerateFileName(item);
            var filePath = Path.Combine(typePath, fileName);
            
            var content = new StringBuilder();
            
            // Front matter (YAML metadata)
            content.AppendLine("---");
            content.AppendLine($"id: {item.Id}");
            content.AppendLine($"type: {item.Type}");
            content.AppendLine($"created: {item.CreatedAt:yyyy-MM-dd HH:mm}");
            content.AppendLine($"modified: {item.ModifiedAt:yyyy-MM-dd HH:mm}");
            content.AppendLine($"workspace: {item.Workspace}");
            
            if (item.Tags?.Any() == true)
            {
                content.AppendLine($"tags: [{string.Join(", ", item.Tags)}]");
            }
            
            if (!string.IsNullOrEmpty(item.Status))
            {
                content.AppendLine($"status: {item.Status}");
            }
            
            if (!string.IsNullOrEmpty(item.Priority))
            {
                content.AppendLine($"priority: {item.Priority}");
            }
            
            content.AppendLine("---");
            content.AppendLine();
            
            // Title
            content.AppendLine($"# {GetItemTitle(item)}");
            content.AppendLine();
            
            // Main content with wiki links
            var linkedContent = await AddWikiLinksAsync(item.Content, allItems);
            content.AppendLine(linkedContent);
            content.AppendLine();
            
            // Type-specific content
            if (item is Checkpoint checkpoint)
            {
                content.AppendLine("## Session Info");
                content.AppendLine($"- **Session**: {checkpoint.SessionId}");
                content.AppendLine($"- **Sequence**: #{checkpoint.SequenceNumber}");
                
                if (checkpoint.ActiveFiles?.Any() == true)
                {
                    content.AppendLine();
                    content.AppendLine("## Active Files");
                    foreach (var file in checkpoint.ActiveFiles)
                    {
                        content.AppendLine($"- `{file}`");
                    }
                }
            }
            else if (item is Checklist checklist && checklist.Items?.Any() == true)
            {
                content.AppendLine("## Checklist Items");
                var completed = checklist.Items.Count(i => i.IsCompleted);
                content.AppendLine($"Progress: {completed}/{checklist.Items.Count} ({completed * 100 / checklist.Items.Count}%)");
                content.AppendLine();
                
                foreach (var checkItem in checklist.Items.OrderBy(i => i.Order))
                {
                    var checkbox = checkItem.IsCompleted ? "[x]" : "[ ]";
                    content.AppendLine($"- {checkbox} {checkItem.Content}");
                    if (checkItem.IsCompleted && checkItem.CompletedAt.HasValue)
                    {
                        content.AppendLine($"  - Completed: {checkItem.CompletedAt.Value:yyyy-MM-dd HH:mm}");
                    }
                }
            }
            
            // Code snippets
            if (item.CodeSnippets?.Any() == true)
            {
                content.AppendLine("## Code Snippets");
                foreach (var snippet in item.CodeSnippets)
                {
                    content.AppendLine();
                    content.AppendLine($"### {Path.GetFileName(snippet.FilePath)} (lines {snippet.StartLine}-{snippet.EndLine})");
                    content.AppendLine($"```{snippet.Language}");
                    content.AppendLine(snippet.Code);
                    content.AppendLine("```");
                }
            }
            
            // Relationships
            if (options.IncludeRelationships)
            {
                var relationships = await _relationshipService.GetRelationshipsAsync(item.Id);
                if (relationships.Any())
                {
                    content.AppendLine();
                    content.AppendLine("## Related Knowledge");
                    
                    var outgoing = relationships.Where(r => r.FromId == item.Id);
                    if (outgoing.Any())
                    {
                        content.AppendLine("### Links to:");
                        foreach (var rel in outgoing)
                        {
                            var target = allItems.FirstOrDefault(i => i.Id == rel.ToId);
                            if (target != null)
                            {
                                var targetTitle = GetItemTitle(target);
                                content.AppendLine($"- [[{targetTitle}]] ({rel.RelationshipType})");
                            }
                        }
                    }
                    
                    var incoming = relationships.Where(r => r.ToId == item.Id);
                    if (incoming.Any())
                    {
                        content.AppendLine("### Linked from:");
                        foreach (var rel in incoming)
                        {
                            var source = allItems.FirstOrDefault(i => i.Id == rel.FromId);
                            if (source != null)
                            {
                                var sourceTitle = GetItemTitle(source);
                                content.AppendLine($"- [[{sourceTitle}]] ({rel.RelationshipType})");
                            }
                        }
                    }
                }
            }
            
            // Metadata section
            if (item.Metadata?.Any() == true)
            {
                content.AppendLine();
                content.AppendLine("## Metadata");
                content.AppendLine("```json");
                content.AppendLine(System.Text.Json.JsonSerializer.Serialize(item.Metadata, 
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                content.AppendLine("```");
            }
            
            await File.WriteAllTextAsync(filePath, content.ToString());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export item {Id}", item.Id);
            return false;
        }
    }
    
    private Task<string> AddWikiLinksAsync(string content, List<Knowledge> allItems)
    {
        // Auto-link references to other knowledge items
        foreach (var item in allItems)
        {
            var title = GetItemTitle(item);
            var pattern = $@"\b{Regex.Escape(item.Id)}\b";
            content = Regex.Replace(content, pattern, $"[[{title}]]");
        }
        
        return Task.FromResult(content);
    }
    
    private async Task CreateIndexFileAsync(string outputPath, List<Knowledge> items)
    {
        var indexPath = Path.Combine(outputPath, "README.md");
        var content = new StringBuilder();
        
        content.AppendLine("# Knowledge Export");
        content.AppendLine($"Exported: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        content.AppendLine($"Total items: {items.Count}");
        content.AppendLine();
        
        // Group by type
        var typeGroups = items.GroupBy(k => k.Type).OrderBy(g => g.Key);
        
        content.AppendLine("## Contents");
        foreach (var group in typeGroups)
        {
            content.AppendLine();
            content.AppendLine($"### {group.Key} ({group.Count()})");
            
            foreach (var item in group.OrderByDescending(i => i.ModifiedAt).Take(10))
            {
                var title = GetItemTitle(item);
                var relativePath = $"{SanitizeFolderName(item.Type)}/{GenerateFileName(item)}";
                content.AppendLine($"- [[{title}]] - {item.ModifiedAt:yyyy-MM-dd}");
            }
            
            if (group.Count() > 10)
            {
                content.AppendLine($"- ...and {group.Count() - 10} more");
            }
        }
        
        // Statistics
        content.AppendLine();
        content.AppendLine("## Statistics");
        content.AppendLine($"- Oldest entry: {items.Min(i => i.CreatedAt):yyyy-MM-dd}");
        content.AppendLine($"- Newest entry: {items.Max(i => i.CreatedAt):yyyy-MM-dd}");
        content.AppendLine($"- Most accessed: {items.OrderByDescending(i => i.AccessCount).First().Id}");
        
        await File.WriteAllTextAsync(indexPath, content.ToString());
    }
    
    private async Task CreateGraphFileAsync(string outputPath, List<Knowledge> items)
    {
        var graphPath = Path.Combine(outputPath, "GRAPH.md");
        var content = new StringBuilder();
        
        content.AppendLine("# Knowledge Graph");
        content.AppendLine("This file provides an overview of relationships between knowledge items.");
        content.AppendLine();
        
        var relationships = new List<Relationship>();
        foreach (var item in items)
        {
            var itemRels = await _relationshipService.GetRelationshipsAsync(item.Id);
            relationships.AddRange(itemRels);
        }
        
        relationships = relationships.DistinctBy(r => $"{r.FromId}-{r.ToId}-{r.RelationshipType}").ToList();
        
        content.AppendLine($"## Relationship Summary ({relationships.Count} total)");
        
        var typeGroups = relationships.GroupBy(r => r.RelationshipType);
        foreach (var group in typeGroups)
        {
            content.AppendLine($"- {group.Key}: {group.Count()}");
        }
        
        content.AppendLine();
        content.AppendLine("## Connection Map");
        content.AppendLine("```mermaid");
        content.AppendLine("graph TD");
        
        foreach (var rel in relationships.Take(50)) // Limit for readability
        {
            var from = items.FirstOrDefault(i => i.Id == rel.FromId);
            var to = items.FirstOrDefault(i => i.Id == rel.ToId);
            
            if (from != null && to != null)
            {
                var fromTitle = GetItemTitle(from).Replace("\"", "'");
                var toTitle = GetItemTitle(to).Replace("\"", "'");
                content.AppendLine($"    \"{fromTitle}\" -->|{rel.RelationshipType}| \"{toTitle}\"");
            }
        }
        
        content.AppendLine("```");
        
        await File.WriteAllTextAsync(graphPath, content.ToString());
    }
    
    private string GetItemTitle(Knowledge item)
    {
        // Extract a meaningful title from the content
        if (item is Checkpoint checkpoint)
        {
            return $"Checkpoint {checkpoint.SessionId} #{checkpoint.SequenceNumber}";
        }
        
        if (item is Checklist checklist)
        {
            var firstLine = item.Content.Split('\n').FirstOrDefault()?.Trim() ?? item.Id;
            return $"Checklist - {firstLine}";
        }
        
        // For other types, use first line or ID
        var title = item.Content.Split('\n').FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 50)
        {
            title = $"{item.Type} - {item.Id.Substring(0, 8)}";
        }
        
        return SanitizeFileName(title);
    }
    
    private string GenerateFileName(Knowledge item)
    {
        var title = GetItemTitle(item);
        return $"{title}.md";
    }
    
    private string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", name.Split(invalid));
        sanitized = Regex.Replace(sanitized, @"[^\w\s\-\.]", "");
        sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();
        return sanitized;
    }
    
    private string SanitizeFolderName(string name)
    {
        return SanitizeFileName(name).Replace(" ", "_");
    }
}

// Export models
public class ExportOptions
{
    public bool IncludeRelationships { get; set; } = true;
    public bool CreateIndex { get; set; } = true;
    public string? FilterByType { get; set; }
    public bool IncludeArchived { get; set; } = false;
}

public class ExportResult
{
    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public int ExportedCount { get; set; }
    public int FailedCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}