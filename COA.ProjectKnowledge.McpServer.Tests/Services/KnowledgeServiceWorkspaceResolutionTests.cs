using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Data.Entities;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Tests.TestBase;
using NUnit.Framework;
using FluentAssertions;

namespace COA.ProjectKnowledge.McpServer.Tests.Services;

public class KnowledgeServiceWorkspaceResolutionTests : ProjectKnowledgeTestBase
{
    private KnowledgeService _knowledgeService = null!;
    private Mock<IWorkspaceResolver> _workspaceResolverMock = null!;
    private WorkspaceResolver _realWorkspaceResolver = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        
        // Create real WorkspaceResolver for normalization testing
        var configuration = new ConfigurationBuilder().Build();
        _realWorkspaceResolver = new WorkspaceResolver(configuration);
        
        // Setup mock to use real normalization logic
        _workspaceResolverMock = new Mock<IWorkspaceResolver>();
        _workspaceResolverMock.Setup(x => x.NormalizeWorkspaceName(It.IsAny<string>()))
            .Returns<string>(name => _realWorkspaceResolver.NormalizeWorkspaceName(name));
        
        // Replace the default workspace resolver with our mock
        services.AddSingleton(_workspaceResolverMock.Object);
    }

    protected override void OnSetUp()
    {
        base.OnSetUp();
        _knowledgeService = GetService<KnowledgeService>();
    }

    [Test]
    public async Task SearchKnowledgeAsync_ShouldFindKnowledgeWithNormalizedWorkspaceName()
    {
        // Arrange
        const string originalWorkspace = "COA ProjectKnowledge MCP";
        const string normalizedWorkspace = "coa-projectknowledge-mcp";
        
        // Add knowledge with original workspace name
        var knowledge = new KnowledgeEntity
        {
            Id = "test-id-1",
            Type = KnowledgeTypes.WorkNote,
            Content = "Test content for workspace resolution",
            Workspace = originalWorkspace,
            Tags = "[\"test\"]",
            Status = "active",
            Priority = "normal",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        
        var context = GetDbContext();
        context.Knowledge.Add(knowledge);
        await context.SaveChangesAsync();

        // Act - Search using normalized workspace name
        var request = new SearchKnowledgeRequest
        {
            Query = "test content",
            Workspace = normalizedWorkspace,
            MaxResults = 10
        };

        var result = await _knowledgeService.SearchKnowledgeAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be("test-id-1");
    }

    [Test]
    public async Task GetTimelineAsync_ShouldFindKnowledgeWithNormalizedWorkspaceName()
    {
        // Arrange
        const string originalWorkspace = "My Test Project";
        const string normalizedWorkspace = "my-test-project";
        
        // Add knowledge with original workspace name
        var knowledge = new KnowledgeEntity
        {
            Id = "timeline-test-1",
            Type = KnowledgeTypes.Checkpoint,
            Content = "Timeline test checkpoint",
            Workspace = originalWorkspace,
            Tags = "[\"timeline\"]",
            Status = "active",
            Priority = "normal",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        
        var context = GetDbContext();
        context.Knowledge.Add(knowledge);
        await context.SaveChangesAsync();

        // Act - Request timeline using normalized workspace name
        var request = new TimelineRequest
        {
            Workspace = normalizedWorkspace,
            DaysAgo = 1,
            MaxResults = 10
        };

        var result = await _knowledgeService.GetTimelineAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Timeline.Should().HaveCount(1);
        result.Timeline[0].Id.Should().Be("timeline-test-1");
    }

    [TestCase("COA ProjectKnowledge MCP", "coa-projectknowledge-mcp")]
    [TestCase("My Project Name", "my_project_name")]
    [TestCase("Test Project", "TEST-PROJECT")]
    [TestCase("Snake_Case_Project", "snake-case-project")]
    public async Task WorkspaceResolution_ShouldMatchDifferentFormats(string originalWorkspace, string searchWorkspace)
    {
        // Arrange
        var knowledge = new KnowledgeEntity
        {
            Id = $"test-{Guid.NewGuid()}",
            Type = KnowledgeTypes.WorkNote,
            Content = "Content for format test",
            Workspace = originalWorkspace,
            Tags = "[\"format-test\"]",
            Status = "active",
            Priority = "normal",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        
        var context = GetDbContext();
        context.Knowledge.Add(knowledge);
        await context.SaveChangesAsync();

        // Act
        var request = new SearchKnowledgeRequest
        {
            Query = "format test",
            Workspace = searchWorkspace,
            MaxResults = 10
        };

        var result = await _knowledgeService.SearchKnowledgeAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(knowledge.Id);
    }

    [Test]
    public async Task WorkspaceResolution_ShouldFallBackToExactMatchIfNoNormalizedMatch()
    {
        // Arrange
        const string exactWorkspace = "Exact-Match-Workspace";
        
        var knowledge = new KnowledgeEntity
        {
            Id = "exact-match-test",
            Type = KnowledgeTypes.WorkNote,
            Content = "Exact match test content",
            Workspace = exactWorkspace,
            Tags = "[\"exact-match\"]",
            Status = "active",
            Priority = "normal",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        
        var context = GetDbContext();
        context.Knowledge.Add(knowledge);
        await context.SaveChangesAsync();

        // Act - Search using exact workspace name
        var request = new SearchKnowledgeRequest
        {
            Query = "exact match",
            Workspace = exactWorkspace,
            MaxResults = 10
        };

        var result = await _knowledgeService.SearchKnowledgeAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be("exact-match-test");
    }

    [Test]
    public async Task WorkspaceResolution_ShouldReturnEmptyForNonMatchingWorkspace()
    {
        // Arrange
        const string originalWorkspace = "Real Workspace";
        const string searchWorkspace = "Non Existing Workspace";
        
        var knowledge = new KnowledgeEntity
        {
            Id = "no-match-test",
            Type = KnowledgeTypes.WorkNote,
            Content = "This should not be found",
            Workspace = originalWorkspace,
            Tags = "[\"no-match\"]",
            Status = "active",
            Priority = "normal",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        
        var context = GetDbContext();
        context.Knowledge.Add(knowledge);
        await context.SaveChangesAsync();

        // Act
        var request = new SearchKnowledgeRequest
        {
            Query = "should not be found",
            Workspace = searchWorkspace,
            MaxResults = 10
        };

        var result = await _knowledgeService.SearchKnowledgeAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Items.Should().BeEmpty();
    }
}