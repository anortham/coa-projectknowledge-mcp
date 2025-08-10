using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace COA.ProjectKnowledge.McpServer.Validation;

/// <summary>
/// Validates an array of tags for proper formatting
/// </summary>
public class TagsAttribute : ValidationAttribute
{
    private static readonly Regex ValidTagPattern = new Regex(
        @"^[a-zA-Z0-9][a-zA-Z0-9\-_]{0,48}[a-zA-Z0-9]$|^[a-zA-Z0-9]$",
        RegexOptions.Compiled);
    
    public int MaxTags { get; set; } = 20;
    public int MaxTagLength { get; set; } = 50;

    public TagsAttribute() : base("Invalid tags")
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        if (value is not string[] tags)
        {
            return new ValidationResult("Tags must be a string array");
        }

        if (tags.Length > MaxTags)
        {
            return new ValidationResult($"Cannot have more than {MaxTags} tags");
        }

        var invalidTags = new List<string>();
        var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                invalidTags.Add("(empty)");
                continue;
            }

            if (tag.Length > MaxTagLength)
            {
                invalidTags.Add($"{tag} (too long)");
                continue;
            }

            if (!ValidTagPattern.IsMatch(tag))
            {
                invalidTags.Add($"{tag} (invalid format)");
                continue;
            }

            if (!seenTags.Add(tag))
            {
                invalidTags.Add($"{tag} (duplicate)");
            }
        }

        if (invalidTags.Any())
        {
            return new ValidationResult(
                $"Invalid tags: {string.Join(", ", invalidTags)}. " +
                "Tags must be alphanumeric with hyphens or underscores, 1-50 characters");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Normalize tags by removing duplicates and invalid entries
    /// </summary>
    public static string[] Normalize(string[]? tags)
    {
        if (tags == null || tags.Length == 0)
        {
            return Array.Empty<string>();
        }

        return tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Where(t => t.Length <= 50)
            .Where(t => ValidTagPattern.IsMatch(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Distinct()
            .Take(20)
            .ToArray();
    }
}