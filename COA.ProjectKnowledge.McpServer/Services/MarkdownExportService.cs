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
            var searchRequest = new SearchKnowledgeRequest
            {
                Query = query,
                Workspace = workspace,
                MaxResults = 1000
            };
            var searchResponse = await _knowledgeService.SearchKnowledgeAsync(searchRequest);
            var items = searchResponse.Items?.Select(item =>
            {
                var knowledge = new Knowledge
                {
                    Id = item.Id,
                    Type = item.Type,
                    Content = item.Content,
                    CreatedAt = item.CreatedAt,
                    ModifiedAt = item.ModifiedAt,
                    AccessCount = item.AccessCount
                };
                
                // Set metadata for computed properties
                if (item.Tags != null)
                    knowledge.SetMetadata("tags", item.Tags);
                if (!string.IsNullOrEmpty(item.Status))
                    knowledge.SetMetadata("status", item.Status);
                if (!string.IsNullOrEmpty(item.Priority))
                    knowledge.SetMetadata("priority", item.Priority);
                
                return knowledge;
            }).ToList() ?? new List<Knowledge>();
            
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
            
            // Create viewing guide
            await CreateViewingGuideAsync(outputPath, options.Format);
            
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
            
            // Main content with links based on format
            var linkedContent = await AddLinksAsync(item.Content, allItems, options.Format);
            content.AppendLine(linkedContent);
            content.AppendLine();
            
            // Type-specific content - checkpoints and checklists moved to Goldfish
            
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
                                var link = GenerateLink(target, options.Format);
                                content.AppendLine($"- {link} ({rel.RelationshipType})");
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
                                var link = GenerateLink(source, options.Format);
                                content.AppendLine($"- {link} ({rel.RelationshipType})");
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
    
    private Task<string> AddLinksAsync(string content, List<Knowledge> allItems, ExportFormat format)
    {
        // Auto-link references to other knowledge items based on format
        foreach (var item in allItems)
        {
            var pattern = $@"\b{Regex.Escape(item.Id)}\b";
            var link = GenerateLink(item, format);
            content = Regex.Replace(content, pattern, link);
        }
        
        return Task.FromResult(content);
    }
    
    private string GenerateLink(Knowledge item, ExportFormat format)
    {
        var title = GetItemTitle(item);
        var fileName = GenerateFileName(item);
        var typePath = SanitizeFolderName(item.Type).ToLower();
        
        return format switch
        {
            ExportFormat.Obsidian => $"[[{title}]]",
            ExportFormat.Universal or ExportFormat.Joplin => $"[{title}](./{typePath}/{fileName})",
            _ => $"[{title}](./{typePath}/{fileName})"
        };
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
        if (items.Count > 0)
        {
            content.AppendLine($"- Oldest entry: {items.Min(i => i.CreatedAt):yyyy-MM-dd}");
            content.AppendLine($"- Newest entry: {items.Max(i => i.CreatedAt):yyyy-MM-dd}");
            content.AppendLine($"- Most accessed: {items.OrderByDescending(i => i.AccessCount).First().Id}");
        }
        else
        {
            content.AppendLine("- No knowledge items to analyze");
        }
        
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
        // Checkpoint and Checklist types moved to Goldfish
        
        // For other types, use first line or ID
        var title = item.Content.Split('\n').FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 50)
        {
            title = $"{item.Type} - {item.Id.Substring(0, 8)}";
        }
        
        return SanitizeFileName(title);
    }
    
    private async Task CreateViewingGuideAsync(string outputPath, ExportFormat format)
    {
        var guidePath = Path.Combine(outputPath, "VIEWING_GUIDE.md");
        var content = new StringBuilder();
        
        content.AppendLine("# Viewing This Documentation");
        content.AppendLine();
        content.AppendLine($"This export was created in **{format}** format on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.");
        content.AppendLine();
        
        content.AppendLine("## Option 1: Joplin (Recommended - Free & Open Source)");
        content.AppendLine("1. Download Joplin from https://joplinapp.org");
        content.AppendLine("2. Go to File → Import → MD - Markdown (Directory)");
        content.AppendLine("3. Select this folder");
        content.AppendLine("4. All notes will be imported with structure preserved");
        content.AppendLine();
        
        content.AppendLine("## Option 2: Obsidian (Power Users)");
        content.AppendLine("1. Download Obsidian from https://obsidian.md");
        content.AppendLine("2. Create new vault or open existing");
        if (format == ExportFormat.Obsidian)
        {
            content.AppendLine("3. Copy this folder into your vault");
            content.AppendLine("4. WikiLinks will work automatically");
        }
        else
        {
            content.AppendLine("3. Copy this folder into your vault");
            content.AppendLine("4. Standard markdown links will work");
        }
        content.AppendLine();
        
        content.AppendLine("## Option 3: VS Code");
        content.AppendLine("1. Open this folder in VS Code");
        content.AppendLine("2. Install 'Markdown Preview Enhanced' extension");
        content.AppendLine("3. Use Ctrl+K V (Cmd+K V on Mac) to preview any file");
        content.AppendLine("4. Mermaid diagrams will render in preview");
        content.AppendLine();
        
        content.AppendLine("## Option 4: GitHub/Azure DevOps");
        content.AppendLine("1. Upload folder to repository");
        content.AppendLine("2. Markdown files will render automatically");
        content.AppendLine("3. Navigate using file browser");
        content.AppendLine("4. Mermaid diagrams are supported");
        content.AppendLine();
        
        content.AppendLine("## Option 5: Web Browser");
        content.AppendLine("1. Use a markdown viewer browser extension");
        content.AppendLine("2. Or use online viewers like dillinger.io or stackedit.io");
        content.AppendLine("3. Open individual .md files");
        content.AppendLine();
        
        content.AppendLine("## File Structure");
        content.AppendLine("- `README.md` - Main index with statistics");
        content.AppendLine("- `GRAPH.md` - Relationship visualization (Mermaid)");
        content.AppendLine("- `VIEWING_GUIDE.md` - This file");
        content.AppendLine("- Folders by knowledge type containing individual items");
        content.AppendLine();
        
        content.AppendLine("## Features");
        content.AppendLine("- ✅ Cross-references between documents");
        content.AppendLine("- ✅ Mermaid diagrams for relationships");
        content.AppendLine("- ✅ YAML front matter with metadata");
        content.AppendLine("- ✅ Organized by knowledge type");
        content.AppendLine("- ✅ Compatible with major markdown tools");
        
        await File.WriteAllTextAsync(guidePath, content.ToString());
    }
    
    private string GenerateFileName(Knowledge item)
    {
        var title = GetItemTitle(item);
        var sanitized = SanitizeFileName(title);
        // Convert spaces to hyphens for better cross-platform compatibility
        sanitized = sanitized.Replace(" ", "-").ToLower();
        return $"{sanitized}.md";
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
public enum ExportFormat
{
    Universal,   // Default - works with Joplin, Obsidian, VS Code, GitHub, etc.
    Obsidian,    // WikiLinks and Obsidian-specific features
    Joplin       // Joplin-compatible format (currently same as Universal)
}

public class ExportOptions
{
    public bool IncludeRelationships { get; set; } = true;
    public bool CreateIndex { get; set; } = true;
    public string? FilterByType { get; set; }
    public bool IncludeArchived { get; set; } = false;
    public ExportFormat Format { get; set; } = ExportFormat.Universal;
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