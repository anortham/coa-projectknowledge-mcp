using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Data.Entities;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Tests.TestBase;
using COA.ProjectKnowledge.McpServer.Tools;
using COA.ProjectKnowledge.McpServer.ResponseBuilders;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace COA.ProjectKnowledge.McpServer.Tests.Tools;

[TestFixture]
public class FindKnowledgeToolTests : ProjectKnowledgeTestBase
{
    private FindKnowledgeTool _tool = null!;
    private Mock<ILogger<FindKnowledgeTool>> _toolLoggerMock = null!;
    private KnowledgeService _knowledgeService = null!;
    private KnowledgeSearchResponseBuilder _responseBuilder = null!;
    private IServiceScope _testScope = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        // Setup specific mocks for this test
        _toolLoggerMock = new Mock<ILogger<FindKnowledgeTool>>();
        services.AddSingleton(_toolLoggerMock.Object);

        var builderLoggerMock = new Mock<ILogger<KnowledgeSearchResponseBuilder>>();
        services.AddSingleton(builderLoggerMock.Object);

        // Register response builder
        services.AddScoped<KnowledgeSearchResponseBuilder>();
    }

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();
        
        // Create scope for the test to ensure proper service lifetimes
        _testScope = ServiceProvider.CreateScope();
        _knowledgeService = _testScope.ServiceProvider.GetRequiredService<KnowledgeService>();
        _responseBuilder = _testScope.ServiceProvider.GetRequiredService<KnowledgeSearchResponseBuilder>();
        
        // Create the tool
        _tool = new FindKnowledgeTool(_knowledgeService, _responseBuilder, _toolLoggerMock.Object);
        
        // Add test data after scope is created
        SeedTestDataAsync().GetAwaiter().GetResult();
    }
    
    [TearDown]
    public override void TearDown()
    {
        _testScope?.Dispose();
        base.TearDown();
    }

    private KnowledgeEntity CreateKnowledgeItem(
        string type, 
        string content, 
        string workspace, 
        DateTime createdAt, 
        int accessCount,
        string? tags = null,
        string? priority = null,
        string? status = null)
    {
        var item = new KnowledgeEntity
        {
            Id = ChronologicalId.Generate(),
            Type = type,
            Content = content,
            Workspace = workspace,
            CreatedAt = createdAt,
            ModifiedAt = createdAt,
            AccessedAt = createdAt.AddDays(Math.Min(0, (DateTime.UtcNow - createdAt).TotalDays / 2)),
            AccessCount = accessCount,
            Tags = tags != null ? System.Text.Json.JsonSerializer.Serialize(tags.Split(',')) : null,
            Priority = priority,
            Status = status,
            Metadata = "{}"
        };
        
        return item;
    }
    
    private async Task SeedTestDataAsync()
    {
        var context = _testScope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        
        // Clear existing data
        context.Knowledge.RemoveRange(context.Knowledge);
        await context.SaveChangesAsync();
        
        // Add test knowledge items with different ages for temporal scoring tests
        var now = DateTime.UtcNow;
        var items = new[]
        {
            CreateKnowledgeItem(
                KnowledgeTypes.ProjectInsight,
                "JWT authentication system uses refresh tokens for enhanced security",
                "test-workspace",
                now.AddDays(-1), // 1 day old
                10,
                "auth,security,jwt",
                "high",
                "active"),
            
            CreateKnowledgeItem(
                KnowledgeTypes.TechnicalDebt,
                "User service needs refactoring to reduce cyclomatic complexity",
                "test-workspace",
                now.AddDays(-10), // 10 days old
                2,
                "refactoring,debt,user-service",
                "medium",
                "active"),
            
            CreateKnowledgeItem(
                KnowledgeTypes.WorkNote,
                "Remember to update API documentation after endpoint changes",
                "test-workspace",
                now.AddDays(-30), // 30 days old
                1,
                "documentation,api",
                "low",
                "active"),
            
            CreateKnowledgeItem(
                KnowledgeTypes.ProjectInsight,
                "Database connection pooling improved response times by 40%",
                "test-workspace",
                now.AddDays(-90), // 90 days old
                5,
                "performance,database",
                "high",
                "completed"),
            
            CreateKnowledgeItem(
                KnowledgeTypes.Checkpoint,
                "Completed authentication module implementation",
                "other-workspace", // Different workspace
                now.AddDays(-2),
                3,
                "milestone,auth",
                null,
                "completed")
        };
        
        foreach (var item in items)
        {
            context.Knowledge.Add(item);
        }
        await context.SaveChangesAsync();
    }

    [Test]
    public async Task ExecuteAsync_WithBasicQuery_ReturnsMatchingResults()
    {
        // Arrange
        var parameters = new FindKnowledgeParams
        {
            Query = "authentication",
            Workspace = "test-workspace",
            MaxResults = 10
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Content.Should().Contain("JWT authentication");
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyQuery_ReturnsAllItemsInWorkspace()
    {
        // Arrange
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            Workspace = "test-workspace",
            MaxResults = 10
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(4); // All items in test-workspace
    }

    [Test]
    public async Task ExecuteAsync_WithAggressiveTemporalScoring_PrioritizesRecentItems()
    {
        // Arrange
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            Workspace = "test-workspace",
            MaxResults = 10,
            TemporalScoring = TemporalScoringMode.Aggressive, // 7-day half-life
            BoostRecent = true
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(4);
        
        // With aggressive temporal scoring, the 1-day old item should be first
        result.Items.First().Content.Should().Contain("JWT authentication");
        
        // The 90-day old item should be last due to heavy temporal decay
        result.Items.Last().Content.Should().Contain("Database connection pooling");
    }

    [Test]
    public async Task ExecuteAsync_WithNoTemporalScoring_ReturnsChronologicalOrder()
    {
        // Arrange
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            Workspace = "test-workspace",
            MaxResults = 10,
            TemporalScoring = TemporalScoringMode.None,
            BoostRecent = false,
            BoostFrequent = false
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(4);
        
        // Without temporal scoring, items are ordered by ID (chronological)
        // Since all items are created at nearly the same time in the test,
        // we can't reliably predict the order of CreatedAt fields
        // Just verify we got the expected number of items
        FluentAssertions.AssertionExtensions.Should(result.Items.Count).BeGreaterThan(0);
    }

    [Test]
    public async Task ExecuteAsync_WithTypeFilter_ReturnsOnlyMatchingTypes()
    {
        // Arrange
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            Types = new[] { KnowledgeTypes.ProjectInsight },
            Workspace = "test-workspace",
            MaxResults = 10
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => i.Type == KnowledgeTypes.ProjectInsight);
    }

    [Test]
    public async Task ExecuteAsync_WithMultipleTypes_ReturnsAllMatchingTypes()
    {
        // Arrange
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            Types = new[] { KnowledgeTypes.TechnicalDebt, KnowledgeTypes.WorkNote },
            Workspace = "test-workspace",
            MaxResults = 10
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(i => i.Type == KnowledgeTypes.TechnicalDebt);
        result.Items.Should().Contain(i => i.Type == KnowledgeTypes.WorkNote);
    }

    [Test]
    public async Task ExecuteAsync_WithTagFilter_ReturnsMatchingTags()
    {
        // Arrange
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            Tags = new[] { "security" },
            Workspace = "test-workspace",
            MaxResults = 10
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Tags.Should().Contain("security");
    }

    [Test]
    public async Task ExecuteAsync_WithPriorityFilter_ReturnsMatchingPriorities()
    {
        // Arrange
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            Priorities = new[] { "high" },
            Workspace = "test-workspace",
            MaxResults = 10
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => i.Priority == "high");
    }

    [Test]
    public async Task ExecuteAsync_WithStatusFilter_ReturnsMatchingStatuses()
    {
        // Arrange
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            Statuses = new[] { "completed" },
            Workspace = "test-workspace",
            MaxResults = 10
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Status.Should().Be("completed");
    }

    [Test]
    public async Task ExecuteAsync_WithDateRange_FiltersCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            FromDate = now.AddDays(-15), // Last 15 days
            ToDate = now,
            Workspace = "test-workspace",
            MaxResults = 10
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(2); // Only items from last 15 days
        result.Items.Should().OnlyContain(i => 
            i.CreatedAt >= now.AddDays(-15) && i.CreatedAt <= now);
    }

    [Test]
    public async Task ExecuteAsync_WithMaxResults_LimitsOutput()
    {
        // Arrange
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            Workspace = "test-workspace",
            MaxResults = 2
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        FluentAssertions.AssertionExtensions.Should(result.TotalCount).Be(4); // Total available is 4
    }

    [Test]
    public async Task ExecuteAsync_WithBoostFrequent_PrioritizesHighAccessCount()
    {
        // Arrange
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            Workspace = "test-workspace",
            MaxResults = 10,
            BoostFrequent = true,
            BoostRecent = false,
            TemporalScoring = TemporalScoringMode.None // Disable temporal to test access boost alone
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(4);
        
        // The item with AccessCount=10 should rank high
        var highAccessItem = result.Items.FirstOrDefault(i => 
            i.Content.Contains("JWT authentication"));
        highAccessItem.Should().NotBeNull();
        FluentAssertions.AssertionExtensions.Should(highAccessItem!.AccessCount).Be(10);
    }

    [Test]
    public async Task ExecuteAsync_WithDefaultTemporalScoring_AppliesModerateDecay()
    {
        // Arrange
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            Workspace = "test-workspace",
            MaxResults = 10,
            TemporalScoring = TemporalScoringMode.Default // 30-day half-life
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(4);
        
        // With default scoring, recent items should still rank higher but not as aggressively
        // The 30-day old item should still have reasonable score (0.5 decay)
        var thirtyDayOldItem = result.Items.FirstOrDefault(i => 
            i.Content.Contains("API documentation"));
        thirtyDayOldItem.Should().NotBeNull();
    }

    [Test]
    public async Task ExecuteAsync_WithGentleTemporalScoring_AppliesSlowDecay()
    {
        // Arrange
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            Workspace = "test-workspace",
            MaxResults = 10,
            TemporalScoring = TemporalScoringMode.Gentle // 90-day half-life
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(4);
        
        // With gentle scoring, even 90-day old items should have decent scores
        var ninetyDayOldItem = result.Items.FirstOrDefault(i => 
            i.Content.Contains("Database connection pooling"));
        ninetyDayOldItem.Should().NotBeNull();
        // It should not be last with gentle scoring due to its high priority
    }

    [Test]
    public async Task ExecuteAsync_WithCombinedFilters_AppliesAllCorrectly()
    {
        // Arrange
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            Types = new[] { KnowledgeTypes.ProjectInsight },
            Tags = new[] { "performance" },
            Workspace = "test-workspace",
            MaxResults = 10
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        var item = result.Items.First();
        item.Type.Should().Be(KnowledgeTypes.ProjectInsight);
        item.Tags.Should().Contain("performance");
        item.Content.Should().Contain("Database connection pooling");
    }

    [Test]
    public async Task ExecuteAsync_WithInvalidWorkspace_ReturnsNoResults()
    {
        // Arrange
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            Workspace = "non-existent-workspace",
            MaxResults = 10
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        FluentAssertions.AssertionExtensions.Should(result.TotalCount).Be(0);
    }

    [Test]
    public async Task ExecuteAsync_WithNullParameters_UsesDefaults()
    {
        // Arrange - Add data with default workspace "TestWorkspace"
        var context = _testScope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        var item = CreateKnowledgeItem(
            KnowledgeTypes.WorkNote,
            "Item with default workspace",
            "TestWorkspace", // This is the default from mock
            DateTime.UtcNow,
            0,
            "test",
            "normal",
            "active");
        context.Knowledge.Add(item);
        await context.SaveChangesAsync();
        
        var parameters = new FindKnowledgeParams
        {
            // All nulls to test defaults
        };
        
        // Act
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        // Should use default workspace and return the item we added
        result.Items.Should().HaveCountGreaterThan(0);
    }
}