using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Tools;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Tests.TestBase;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace COA.ProjectKnowledge.McpServer.Tests.Tools;

[TestFixture]
public class CreateChecklistToolTests : ProjectKnowledgeTestBase
{
    private CreateChecklistTool _tool = null!;
    private ChecklistService _checklistService = null!;
    private Mock<IWorkspaceResolver> _workspaceResolverMock = null!;
    private Mock<ILogger<ChecklistService>> _loggerMock = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        // Setup mocks
        _workspaceResolverMock = new Mock<IWorkspaceResolver>();
        _workspaceResolverMock.Setup(x => x.GetCurrentWorkspace()).Returns("TestWorkspace");
        services.AddSingleton(_workspaceResolverMock.Object);

        _loggerMock = new Mock<ILogger<ChecklistService>>();
        services.AddSingleton(_loggerMock.Object);

        // Add services
        services.AddScoped<ChecklistService>();
        services.AddScoped<CreateChecklistTool>();
    }

    protected override void OnSetUp()
    {
        base.OnSetUp();
        _checklistService = GetRequiredService<ChecklistService>();
        _tool = GetRequiredService<CreateChecklistTool>();
    }

    [Test]
    public async Task ExecuteAsync_WithValidRequest_CreatesChecklist()
    {
        // Arrange
        var parameters = new CreateChecklistParams
        {
            Content = "User Authentication Feature Implementation",
            Items = new[]
            {
                "Implement JWT token generation",
                "Add password hashing",
                "Create login endpoint",
                "Add user registration",
                "Write unit tests"
            }
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ChecklistId.Should().NotBeNullOrEmpty();
        FluentAssertions.AssertionExtensions.Should(result.ItemCount).Be(5);
        result.CompletionPercentage.Should().Be(0); // New items should be incomplete

        // Verify in database
        var checklist = await GetDbContext().Knowledge.FindAsync(result.ChecklistId);
        checklist.Should().NotBeNull();
        checklist!.Type.Should().Be(KnowledgeTypes.Checklist);
        checklist.Content.Should().Be("User Authentication Feature Implementation");
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyItemsList_CreatesChecklistWithoutItems()
    {
        // Arrange
        var parameters = new CreateChecklistParams
        {
            Content = "Empty checklist for future use",
            Items = Array.Empty<string>()
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ChecklistId.Should().NotBeNullOrEmpty();
        FluentAssertions.AssertionExtensions.Should(result.ItemCount).Be(0);

        // Verify in database
        var checklist = await GetDbContext().Knowledge.FindAsync(result.ChecklistId);
        checklist.Should().NotBeNull();
        checklist!.Type.Should().Be(KnowledgeTypes.Checklist);
    }

    [Test]
    public async Task ExecuteAsync_WithParentChecklist_CreatesNestedChecklist()
    {
        // Arrange
        // Create parent checklist first
        var parentResult = await _tool.ExecuteAsync(new CreateChecklistParams
        {
            Content = "Parent Checklist",
            Items = new[] { "Parent Task 1", "Parent Task 2" }
        });

        var parameters = new CreateChecklistParams
        {
            Content = "Nested Checklist",
            Items = new[] { "Nested Task 1", "Nested Task 2" },
            ParentChecklistId = parentResult.ChecklistId
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ChecklistId.Should().NotBeNullOrEmpty();

        // Verify parent relationship is established
        var nestedChecklist = await GetDbContext().Knowledge.FindAsync(result.ChecklistId);
        nestedChecklist.Should().NotBeNull();
        nestedChecklist!.Metadata.Should().Contain("parentChecklistId");
    }

    [Test]
    public async Task ExecuteAsync_WithInvalidParentId_ReturnsError()
    {
        // Arrange
        var parameters = new CreateChecklistParams
        {
            Content = "Checklist with invalid parent",
            Items = new[] { "Task 1", "Task 2" },
            ParentChecklistId = "non-existent-parent-id"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("PARENT_CHECKLIST_NOT_FOUND");
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyContent_ReturnsError()
    {
        // Arrange
        var parameters = new CreateChecklistParams
        {
            Content = string.Empty,
            Items = new[] { "Task 1", "Task 2" }
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("CHECKLIST_VALIDATION_ERROR");
    }

    [Test]
    public async Task ExecuteAsync_WithLongItemsList_HandlesCorrectly()
    {
        // Arrange
        var items = Enumerable.Range(1, 25)
            .Select(i => $"Task item number {i}")
            .ToArray();

        var parameters = new CreateChecklistParams
        {
            Content = "Large Checklist",
            Items = items
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        FluentAssertions.AssertionExtensions.Should(result.ItemCount).Be(25);
        result.CompletionPercentage.Should().Be(0); // All items should be incomplete
    }

    [Test]
    public async Task ExecuteAsync_WithSpecialCharactersInItems_PreservesContent()
    {
        // Arrange
        var parameters = new CreateChecklistParams
        {
            Content = "Special Characters Test",
            Items = new[]
            {
                "Task with \"quotes\" and 'apostrophes'",
                "Task with <HTML> & symbols",
                "Task with 100% completion rate",
                "Task with C# code: var x = new List<string>();"
            }
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        FluentAssertions.AssertionExtensions.Should(result.ItemCount).Be(4);
        result.CompletionPercentage.Should().Be(0);

        // Verify checklist created successfully with special characters in content
        var checklist = await GetDbContext().Knowledge.FindAsync(result.ChecklistId);
        checklist.Should().NotBeNull();
    }
}