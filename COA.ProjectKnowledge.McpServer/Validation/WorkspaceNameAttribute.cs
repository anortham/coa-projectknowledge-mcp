using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace COA.ProjectKnowledge.McpServer.Validation;

/// <summary>
/// Validates that a workspace name follows naming conventions
/// </summary>
public class WorkspaceNameAttribute : ValidationAttribute
{
    private static readonly Regex ValidPattern = new Regex(
        @"^[a-zA-Z0-9][a-zA-Z0-9\-_\.]{0,98}[a-zA-Z0-9]$",
        RegexOptions.Compiled);
    
    private static readonly string[] ReservedNames = 
    {
        "default", "system", "admin", "root", "temp", "tmp", 
        "test", "debug", "null", "undefined", "none"
    };

    public WorkspaceNameAttribute() : base("Invalid workspace name")
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            // Null is valid - use [Required] separately if needed
            return ValidationResult.Success;
        }

        var workspace = value.ToString();
        
        if (string.IsNullOrWhiteSpace(workspace))
        {
            return new ValidationResult("Workspace name cannot be empty");
        }

        if (workspace.Length < 2)
        {
            return new ValidationResult("Workspace name must be at least 2 characters");
        }

        if (workspace.Length > 100)
        {
            return new ValidationResult("Workspace name cannot exceed 100 characters");
        }

        // Check reserved names
        if (ReservedNames.Contains(workspace.ToLowerInvariant()))
        {
            return new ValidationResult($"'{workspace}' is a reserved workspace name");
        }

        // Check naming pattern
        if (!ValidPattern.IsMatch(workspace))
        {
            return new ValidationResult(
                "Workspace name must start and end with alphanumeric characters, " +
                "and can contain hyphens, underscores, or dots in between");
        }

        // Check for path traversal attempts
        if (workspace.Contains("..") || workspace.Contains("//") || workspace.Contains("\\\\"))
        {
            return new ValidationResult("Workspace name contains invalid path sequences");
        }

        return ValidationResult.Success;
    }
}