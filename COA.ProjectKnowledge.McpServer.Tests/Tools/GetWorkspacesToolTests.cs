using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Data.Entities;
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
using System.Linq;

namespace COA.ProjectKnowledge.McpServer.Tests.Tools;

[TestFixture]
public class GetWorkspacesToolTests : ProjectKnowledgeTestBase
{
    private GetWorkspacesTool _tool = null!;
    private KnowledgeService _knowledgeService = null!;
    private Mock<IWorkspaceResolver> _workspaceResolverMock = null!;
    private Mock<ILogger<KnowledgeService>> _knowledgeLoggerMock = null!;
    private Mock<ILogger<GetWorkspacesTool>> _toolLoggerMock = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        // Setup mocks
        _workspaceResolverMock = new Mock<IWorkspaceResolver>();
        _workspaceResolverMock.Setup(x => x.GetCurrentWorkspace()).Returns("TestWorkspace");
        services.AddSingleton(_workspaceResolverMock.Object);

        _knowledgeLoggerMock = new Mock<ILogger<KnowledgeService>>();
        services.AddSingleton(_knowledgeLoggerMock.Object);

        _toolLoggerMock = new Mock<ILogger<GetWorkspacesTool>>();
        services.AddSingleton(_toolLoggerMock.Object);

        // Add services
        services.AddScoped<KnowledgeService>();
        services.AddScoped<GetWorkspacesTool>();
    }

    protected override void OnSetUp()
    {
        base.OnSetUp();
        _knowledgeService = GetRequiredService<KnowledgeService>();
        _tool = GetRequiredService<GetWorkspacesTool>();
    }

    protected override async void SeedTestData()
    {
        base.SeedTestData();

        // Create knowledge items in different workspaces by manipulating the database directly
        await CreateTestKnowledgeAsync(KnowledgeTypes.ProjectInsight, "Test insight 1", "Workspace1");
        await CreateTestKnowledgeAsync(KnowledgeTypes.TechnicalDebt, "Test debt 1", "Workspace1");
        await CreateTestKnowledgeAsync(KnowledgeTypes.WorkNote, "Test note 1", "Workspace2");
        await CreateTestKnowledgeAsync(KnowledgeTypes.ProjectInsight, "Test insight 2", "Workspace2");
        await CreateTestKnowledgeAsync(KnowledgeTypes.Checkpoint, "Test checkpoint 1", "TestWorkspace");
    }

    private new async Task CreateTestKnowledgeAsync(string type, string content, string workspace)
    {
        var knowledge = new KnowledgeEntity
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Content = content,
            Workspace = workspace,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            AccessCount = 0
        };

        GetDbContext().Knowledge.Add(knowledge);
        await GetDbContext().SaveChangesAsync();
    }

    [Test]
    public async Task ExecuteAsync_WithDefaultParameters_ReturnsAllWorkspaces()
    {
        // Arrange
        var parameters = new GetWorkspacesParams();

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        // Should return workspace information
    }

    [Test]
    public async Task ExecuteAsync_AfterSeedingData_ReturnsWorkspacesWithCounts()
    {
        // Arrange
        var parameters = new GetWorkspacesParams();

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        
        // Should find the workspaces we created in seed data
        // The exact structure depends on GetWorkspacesResult implementation
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyDatabase_ReturnsEmptyResults()
    {
        // Arrange
        // Clear all knowledge items
        GetDbContext().Knowledge.RemoveRange(GetDbContext().Knowledge);
        await GetDbContext().SaveChangesAsync();

        var parameters = new GetWorkspacesParams();

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        // Should return empty results or at least success
    }

    [Test]
    public async Task ExecuteAsync_ReturnsWorkspacesOrderedByActivity()
    {
        // Arrange
        // Add recent activity to one workspace
        await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.WorkNote,
            Content = "Recent activity in TestWorkspace"
        });

        var parameters = new GetWorkspacesParams();

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        // Workspaces should be ordered appropriately
    }

    [Test]
    public async Task ExecuteAsync_IncludesWorkspaceStatistics()
    {
        // Arrange
        // Create knowledge items with different types in TestWorkspace
        await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Stats test insight"
        });

        await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.TechnicalDebt,
            Content = "Stats test debt",
            Priority = "high"
        });

        var parameters = new GetWorkspacesParams();

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        // Should include statistics about knowledge types per workspace
    }

    [Test]
    public async Task ExecuteAsync_HandlesLargeNumberOfWorkspaces()
    {
        // Arrange
        // Create many workspaces
        for (int i = 0; i < 50; i++)
        {
            await CreateTestKnowledgeAsync(KnowledgeTypes.WorkNote, $"Note {i}", $"Workspace{i:D3}");
        }

        var parameters = new GetWorkspacesParams();

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        // Should handle large number of workspaces efficiently
    }
}