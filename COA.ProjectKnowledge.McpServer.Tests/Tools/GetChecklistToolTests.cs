using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Tests.TestBase;
using COA.ProjectKnowledge.McpServer.Tools;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Threading.Tasks;

namespace COA.ProjectKnowledge.McpServer.Tests.Tools;

[TestFixture]
public class GetChecklistToolTests : ProjectKnowledgeTestBase
{
    private GetChecklistTool _tool = null!;
    private ChecklistService _checklistService = null!;
    private CreateChecklistTool _createChecklistTool = null!;
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
        services.AddScoped<GetChecklistTool>();
        services.AddScoped<CreateChecklistTool>();
    }

    protected override void OnSetUp()
    {
        base.OnSetUp();
        _checklistService = GetRequiredService<ChecklistService>();
        _tool = GetRequiredService<GetChecklistTool>();
        _createChecklistTool = GetRequiredService<CreateChecklistTool>();
    }

    [Test]
    public async Task ExecuteAsync_WithValidChecklistId_ReturnsChecklist()
    {
        // Arrange
        // First create a checklist to retrieve
        var createResult = await _createChecklistTool.ExecuteAsync(new CreateChecklistParams
        {
            Content = "Test Checklist for Retrieval",
            Items = new[] { "Task 1", "Task 2", "Task 3" }
        });

        var parameters = new GetChecklistParams
        {
            ChecklistId = createResult.ChecklistId!
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Checklist.Should().NotBeNull();
        result.Checklist!.Id.Should().Be(createResult.ChecklistId);
        result.Checklist.Content.Should().Be("Test Checklist for Retrieval");
        result.Checklist.Items.Should().HaveCount(3);
        FluentAssertions.AssertionExtensions.Should(result.Checklist.TotalCount).Be(3);
        FluentAssertions.AssertionExtensions.Should(result.Checklist.CompletedCount).Be(0);
        result.Checklist.CompletionPercentage.Should().Be(0);
    }

    [Test]
    public async Task ExecuteAsync_WithNonexistentChecklistId_ReturnsError()
    {
        // Arrange
        var parameters = new GetChecklistParams
        {
            ChecklistId = "non-existent-checklist-id"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("CHECKLIST_NOT_FOUND");
        result.Checklist.Should().BeNull();
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyChecklistId_ReturnsError()
    {
        // Arrange
        var parameters = new GetChecklistParams
        {
            ChecklistId = string.Empty
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Test]
    public async Task ExecuteAsync_WithPartiallyCompletedChecklist_ReturnsCorrectStatus()
    {
        // Arrange
        // Create checklist
        var createResult = await _createChecklistTool.ExecuteAsync(new CreateChecklistParams
        {
            Content = "Partially Completed Checklist",
            Items = new[] { "Task 1", "Task 2", "Task 3", "Task 4" }
        });

        var parameters = new GetChecklistParams
        {
            ChecklistId = createResult.ChecklistId!
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Checklist.Should().NotBeNull();
        result.Checklist!.Items.Should().HaveCount(4);
        result.Checklist.Items.Should().AllSatisfy(item => item.IsCompleted.Should().BeFalse());
        result.Checklist.Status.Should().Be("active"); // Should be active with no completed items
    }

    [Test]
    public async Task ExecuteAsync_WithChecklistItems_ReturnsItemDetails()
    {
        // Arrange
        var createResult = await _createChecklistTool.ExecuteAsync(new CreateChecklistParams
        {
            Content = "Detailed Checklist",
            Items = new[] { "First task", "Second task" }
        });

        var parameters = new GetChecklistParams
        {
            ChecklistId = createResult.ChecklistId!
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Checklist.Should().NotBeNull();
        result.Checklist!.Items.Should().HaveCount(2);
        
        // Verify item details
        var firstItem = result.Checklist.Items.First();
        firstItem.Id.Should().NotBeNullOrEmpty();
        firstItem.Content.Should().Be("First task");
        firstItem.IsCompleted.Should().BeFalse();
        firstItem.CompletedAt.Should().BeNull();
        
        var secondItem = result.Checklist.Items.Last();
        secondItem.Content.Should().Be("Second task");
        secondItem.IsCompleted.Should().BeFalse();
    }
}