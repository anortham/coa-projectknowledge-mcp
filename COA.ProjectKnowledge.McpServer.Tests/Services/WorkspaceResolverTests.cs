using Microsoft.Extensions.Configuration;
using COA.ProjectKnowledge.McpServer.Services;
using NUnit.Framework;
using FluentAssertions;

namespace COA.ProjectKnowledge.McpServer.Tests.Services;

public class WorkspaceResolverTests
{
    private readonly WorkspaceResolver _workspaceResolver;

    public WorkspaceResolverTests()
    {
        var configuration = new ConfigurationBuilder().Build();
        _workspaceResolver = new WorkspaceResolver(configuration);
    }

    [TestCase("COA ProjectKnowledge MCP", "coa-projectknowledge-mcp")]
    [TestCase("My Project Name", "my-project-name")]
    [TestCase("snake_case_project", "snake-case-project")]
    [TestCase("Mixed_Case Project", "mixed-case-project")]
    [TestCase("UPPERCASE PROJECT", "uppercase-project")]
    [TestCase("lowercase project", "lowercase-project")]
    [TestCase("Project-With-Hyphens", "project-with-hyphens")]
    [TestCase("Project_With_Underscores", "project-with-underscores")]
    [TestCase("Mixed-Case_And Spaces", "mixed-case-and-spaces")]
    public void NormalizeWorkspaceName_ShouldConvertToLowercaseWithHyphens(string input, string expected)
    {
        // Act
        var result = _workspaceResolver.NormalizeWorkspaceName(input);

        // Assert
        result.Should().Be(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("\t")]
    public void NormalizeWorkspaceName_ShouldHandleNullAndWhitespace(string input)
    {
        // Act
        var result = _workspaceResolver.NormalizeWorkspaceName(input);

        // Assert
        result.Should().Be(input);
    }

    [Test]
    public void NormalizeWorkspaceName_ShouldBeDeterministic()
    {
        // Arrange
        var input = "COA ProjectKnowledge MCP";

        // Act
        var result1 = _workspaceResolver.NormalizeWorkspaceName(input);
        var result2 = _workspaceResolver.NormalizeWorkspaceName(input);

        // Assert
        result1.Should().Be(result2);
    }

    [TestCase("COA ProjectKnowledge MCP", "coa-projectknowledge-mcp", true)]
    [TestCase("My Project", "my-project", true)]
    [TestCase("Different Project", "another-project", false)]
    [TestCase("Project Name", "project_name", true)] // Should normalize both and match
    [TestCase("Test Project", "TEST_PROJECT", true)] // Should normalize both and match
    public void NormalizeWorkspaceName_ShouldAllowMatching(string workspace1, string workspace2, bool shouldMatch)
    {
        // Act
        var normalized1 = _workspaceResolver.NormalizeWorkspaceName(workspace1);
        var normalized2 = _workspaceResolver.NormalizeWorkspaceName(workspace2);
        var matches = normalized1 == normalized2;

        // Assert
        matches.Should().Be(shouldMatch);
    }

    [Test]
    public void NormalizeWorkspaceName_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var input = "Project@Name#With$Special%Characters";

        // Act
        var result = _workspaceResolver.NormalizeWorkspaceName(input);

        // Assert
        result.Should().Be("project@name#with$special%characters");
        result.Should().NotContain(" ");
        result.Should().NotContain("_");
    }
}