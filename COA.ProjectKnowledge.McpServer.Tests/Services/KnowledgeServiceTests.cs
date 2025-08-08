using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Tests.TestBase;
using COA.ProjectKnowledge.McpServer.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace COA.ProjectKnowledge.McpServer.Tests.Services;

[TestFixture]
public class KnowledgeServiceTests : ProjectKnowledgeTestBase
{
    private KnowledgeServiceEF _service = null!;
    private Mock<ILogger<KnowledgeServiceEF>> _loggerMock = null!;
    private Mock<IWorkspaceResolver> _workspaceResolverMock = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        _loggerMock = new Mock<ILogger<KnowledgeServiceEF>>();
        services.AddSingleton(_loggerMock.Object);

        _workspaceResolverMock = new Mock<IWorkspaceResolver>();
        _workspaceResolverMock.Setup(x => x.GetCurrentWorkspace()).Returns("TestWorkspace");
        services.AddSingleton(_workspaceResolverMock.Object);

        services.AddScoped<KnowledgeServiceEF>();
    }

    protected override void OnSetUp()
    {
        base.OnSetUp();
        _service = GetRequiredService<KnowledgeServiceEF>();
    }

    [Test]
    public async Task StoreKnowledge_WithValidData_CreatesKnowledgeItem()
    {
        // Arrange
        var request = new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Test insight content",
            Tags = new[] { "test", "insight" },
            Status = "active",
            Priority = "high"
        };

        // Act
        var result = await _service.StoreKnowledgeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.KnowledgeId.Should().NotBeNullOrEmpty();

        AssertKnowledgeExists(result.KnowledgeId!);
    }

    [Test]
    public async Task StoreKnowledge_WithCodeSnippets_StoresCodeMetadata()
    {
        // Arrange
        var request = new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.WorkNote,
            Content = "Code analysis note",
            CodeSnippets = new[]
            {
                new CodeSnippet
                {
                    FilePath = "test.cs",
                    Language = "csharp",
                    Code = "public class Test { }",
                    StartLine = 1,
                    EndLine = 1
                }
            }
        };

        // Act
        var result = await _service.StoreKnowledgeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var knowledge = DbContext.Knowledge.Find(result.KnowledgeId);
        knowledge.Should().NotBeNull();
        knowledge!.Metadata.Should().Contain("CodeSnippets");
    }

    [Test]
    public async Task SearchKnowledge_WithQuery_ReturnsMatchingItems()
    {
        // Arrange
        await CreateTestKnowledgeAsync(KnowledgeTypes.ProjectInsight, "Authentication uses JWT tokens");
        await CreateTestKnowledgeAsync(KnowledgeTypes.TechnicalDebt, "Legacy authentication needs refactoring");
        await CreateTestKnowledgeAsync(KnowledgeTypes.WorkNote, "Unrelated work note");

        var request = new SearchKnowledgeRequest
        {
            Query = "authentication",
            MaxResults = 10
        };

        // Act
        var result = await _service.SearchKnowledgeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(2);
        result.Items.Should().AllSatisfy(item => 
            item.Content.ToLower().Should().Contain("authentication"));
    }

    [Test]
    public async Task SearchKnowledge_WithTypeFilter_ReturnsOnlySpecificType()
    {
        // Arrange
        await CreateTestKnowledgeAsync(KnowledgeTypes.ProjectInsight, "Insight 1");
        await CreateTestKnowledgeAsync(KnowledgeTypes.ProjectInsight, "Insight 2");
        await CreateTestKnowledgeAsync(KnowledgeTypes.TechnicalDebt, "Debt 1");

        var request = new SearchKnowledgeRequest
        {
            Query = "type:ProjectInsight",
            MaxResults = 10
        };

        // Act
        var result = await _service.SearchKnowledgeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Items.Should().AllSatisfy(item => 
            item.Type.Should().Be(KnowledgeTypes.ProjectInsight));
    }

    [Test]
    public async Task GetKnowledge_WithValidId_ReturnsKnowledgeItem()
    {
        // Arrange
        var knowledge = await CreateTestKnowledgeAsync(
            KnowledgeTypes.WorkNote, 
            "Test content", 
            "test-id-123");

        // Act
        var result = await _service.GetKnowledgeAsync("test-id-123");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Knowledge.Should().NotBeNull();
        result.Knowledge!.Id.Should().Be("test-id-123");
        result.Knowledge.Content.Should().Be("Test content");
    }

    [Test]
    public async Task GetKnowledge_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _service.GetKnowledgeAsync("non-existent-id");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Knowledge.Should().BeNull();
        result.Error.Should().Contain("not found");
    }

    [Test]
    public async Task UpdateKnowledge_WithValidId_UpdatesContent()
    {
        // Arrange
        var knowledge = await CreateTestKnowledgeAsync(
            KnowledgeTypes.WorkNote,
            "Original content",
            "update-test-id");

        var request = new UpdateKnowledgeRequest
        {
            Id = "update-test-id",
            Content = "Updated content",
            Status = "completed"
        };

        // Act
        var result = await _service.UpdateKnowledgeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var updated = DbContext.Knowledge.Find("update-test-id");
        updated.Should().NotBeNull();
        updated!.Content.Should().Be("Updated content");
        updated.Status.Should().Be("completed");
        updated.ModifiedAt.Should().BeAfter(knowledge.CreatedAt);
    }

    [Test]
    public async Task DeleteKnowledge_WithValidId_RemovesItem()
    {
        // Arrange
        await CreateTestKnowledgeAsync(
            KnowledgeTypes.WorkNote,
            "To be deleted",
            "delete-test-id");

        // Act
        var result = await _service.DeleteKnowledgeAsync("delete-test-id");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        AssertKnowledgeNotExists("delete-test-id");
    }

    [Test]
    public async Task GetTimeline_ReturnsChronologicalItems()
    {
        // Arrange
        var now = DateTime.UtcNow;
        await CreateTestKnowledgeAsync(KnowledgeTypes.ProjectInsight, "Recent insight");
        await Task.Delay(100); // Ensure different timestamps
        await CreateTestKnowledgeAsync(KnowledgeTypes.WorkNote, "Recent note");

        var request = new TimelineRequest
        {
            HoursAgo = 1,
            MaxResults = 10
        };

        // Act
        var result = await _service.GetTimelineAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Timeline.Should().HaveCountGreaterOrEqualTo(2);
        result.Timeline.Should().BeInDescendingOrder(item => item.CreatedAt);
    }

    [Test]
    public async Task StoreKnowledge_WithRelatedItems_CreatesRelationships()
    {
        // Arrange
        var existingId = (await CreateTestKnowledgeAsync()).Id;
        
        var request = new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Related insight",
            RelatedTo = new[] { existingId }
        };

        // Act
        var result = await _service.StoreKnowledgeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var relationships = DbContext.Relationships
            .Where(r => r.FromId == result.KnowledgeId || r.ToId == result.KnowledgeId)
            .ToList();

        relationships.Should().HaveCount(1);
        relationships.First().RelationshipType.Should().Be("relates_to");
    }

    [Test]
    public async Task SearchKnowledge_WithMaxResults_LimitsReturnedItems()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await CreateTestKnowledgeAsync(KnowledgeTypes.WorkNote, $"Note {i}");
        }

        var request = new SearchKnowledgeRequest
        {
            Query = "Note",
            MaxResults = 5
        };

        // Act
        var result = await _service.SearchKnowledgeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(10);
    }

    [Test]
    public void GenerateChronologicalId_ProducesValidFormat()
    {
        // Act
        var id = _service.GenerateChronologicalId();

        // Assert
        id.Should().NotBeNullOrEmpty();
        // Format is actually longer than expected - it's based on DateTime ticks
        id.Should().MatchRegex(@"^[0-9A-F]+-[0-9A-F]{6}$");
        id.Should().Contain("-");
    }

    [Test]
    public async Task StoreKnowledge_TracksAccessCount()
    {
        // Arrange
        var request = new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Access tracking test"
        };

        var storeResult = await _service.StoreKnowledgeAsync(request);

        // Act - Access the knowledge multiple times
        await _service.GetKnowledgeAsync(storeResult.KnowledgeId!);
        await _service.GetKnowledgeAsync(storeResult.KnowledgeId!);
        var result = await _service.GetKnowledgeAsync(storeResult.KnowledgeId!);

        // Assert
        result.Knowledge!.AccessCount.Should().Be(3);
    }
}