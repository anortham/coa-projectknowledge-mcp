using System.ComponentModel.DataAnnotations;
using COA.ProjectKnowledge.McpServer.Constants;

namespace COA.ProjectKnowledge.McpServer.Validation;

/// <summary>
/// Validates that a knowledge type is one of the allowed types
/// </summary>
public class KnowledgeTypeAttribute : ValidationAttribute
{
    private static readonly HashSet<string> ValidTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        KnowledgeTypes.Checkpoint,
        KnowledgeTypes.Checklist,
        KnowledgeTypes.TechnicalDebt,
        KnowledgeTypes.ProjectInsight,
        KnowledgeTypes.WorkNote
    };

    public KnowledgeTypeAttribute() : base("Invalid knowledge type")
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            // Null is valid - will default to WorkNote
            return ValidationResult.Success;
        }

        var knowledgeType = value.ToString();
        
        if (string.IsNullOrWhiteSpace(knowledgeType))
        {
            // Empty defaults to WorkNote
            return ValidationResult.Success;
        }

        if (!ValidTypes.Contains(knowledgeType))
        {
            var validTypesList = string.Join(", ", ValidTypes);
            return new ValidationResult(
                $"'{knowledgeType}' is not a valid knowledge type. " +
                $"Valid types are: {validTypesList}");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Get the normalized knowledge type (proper casing)
    /// </summary>
    public static string? Normalize(string? knowledgeType)
    {
        if (string.IsNullOrWhiteSpace(knowledgeType))
        {
            return KnowledgeTypes.WorkNote;
        }

        var match = ValidTypes.FirstOrDefault(t => 
            t.Equals(knowledgeType, StringComparison.OrdinalIgnoreCase));
            
        return match ?? KnowledgeTypes.WorkNote;
    }
}