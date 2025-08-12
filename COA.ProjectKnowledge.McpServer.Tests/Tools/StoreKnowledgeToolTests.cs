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
using System.Linq;
using System.Threading.Tasks;

namespace COA.ProjectKnowledge.McpServer.Tests.Tools;

[TestFixture]
public class StoreKnowledgeToolTests : ProjectKnowledgeTestBase
{
    private KnowledgeService _knowledgeService = null!;
    private Mock<IWorkspaceResolver> _workspaceResolverMock = null!;
    private Mock<ILogger<KnowledgeService>> _loggerMock = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        // Setup mocks
        _workspaceResolverMock = new Mock<IWorkspaceResolver>();
        _workspaceResolverMock.Setup(x => x.GetCurrentWorkspace()).Returns("TestWorkspace");
        services.AddSingleton(_workspaceResolverMock.Object);

        _loggerMock = new Mock<ILogger<KnowledgeService>>();
        services.AddSingleton(_loggerMock.Object);

        // Add services
        services.AddScoped<KnowledgeService>();
    }

    protected override void OnSetUp()
    {
        base.OnSetUp();
        _knowledgeService = GetRequiredService<KnowledgeService>();
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

        // Act - Use the service directly since tool uses old service
        var result = await _knowledgeService.StoreKnowledgeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.KnowledgeId.Should().NotBeNullOrEmpty();

        // Verify in database
        var knowledge = await GetDbContext().Knowledge.FindAsync(result.KnowledgeId);
        knowledge.Should().NotBeNull();
        knowledge!.Content.Should().Be("Test insight content");
    }

    [Test]
    public async Task StoreKnowledge_WithCodeSnippets_StoresCodeMetadata()
    {
        // Arrange
        var request = new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.TechnicalDebt,
            Content = "Refactoring needed in authentication module",
            CodeSnippets = new[]
            {
                new CodeSnippet
                {
                    FilePath = "Auth/LoginService.cs",
                    Language = "csharp",
                    Code = "// TODO: Replace with async method",
                    StartLine = 42,
                    EndLine = 45
                }
            },
            Priority = "medium"
        };

        // Act
        var result = await _knowledgeService.StoreKnowledgeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.KnowledgeId.Should().NotBeNullOrEmpty();
        
        // Verify knowledge was created with code snippets
        var knowledge = await GetDbContext().Knowledge.FindAsync(result.KnowledgeId);
        knowledge.Should().NotBeNull();
        knowledge!.Metadata.Should().Contain("CodeSnippets");
    }

    [Test]
    public async Task StoreKnowledge_WithRelatedItems_CreatesRelationships()
    {
        // Arrange
        // First create a knowledge item to relate to
        var firstItem = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "First knowledge item"
        });

        var request = new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Related knowledge item",
            RelatedTo = new[] { firstItem.KnowledgeId! }
        };

        // Act
        var result = await _knowledgeService.StoreKnowledgeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        // Verify relationship was created
        var relationships = GetDbContext().Relationships
            .Where(r => r.FromId == result.KnowledgeId || r.ToId == result.KnowledgeId)
            .ToList();
        relationships.Should().HaveCount(1);
    }

    [Test]
    public async Task StoreKnowledge_WithInvalidType_ReturnsError()
    {
        // Arrange
        var request = new StoreKnowledgeRequest
        {
            Type = "InvalidType",
            Content = "Test content"
        };

        // Act
        var result = await _knowledgeService.StoreKnowledgeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        result.Error.Should().Contain("Invalid knowledge type");
    }

    [Test]
    public async Task StoreKnowledge_WithEmptyContent_HandlesGracefully()
    {
        // Arrange
        var request = new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.WorkNote,
            Content = "" // Empty content should still be allowed
        };

        // Act
        var result = await _knowledgeService.StoreKnowledgeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Empty content is valid
    }

    [Test]
    public async Task SearchKnowledge_WithQuery_ReturnsMatchingItems()
    {
        // Arrange
        await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Authentication uses JWT tokens"
        });
        
        await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.TechnicalDebt,
            Content = "Legacy authentication needs refactoring"
        });
        
        await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.WorkNote,
            Content = "Unrelated work note"
        });

        var searchRequest = new SearchKnowledgeRequest
        {
            Query = "authentication",
            MaxResults = 10
        };

        // Act
        var result = await _knowledgeService.SearchKnowledgeAsync(searchRequest);

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
        await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Insight 1"
        });
        
        await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Insight 2"
        });
        
        await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.TechnicalDebt,
            Content = "Debt 1"
        });

        var searchRequest = new SearchKnowledgeRequest
        {
            Query = "type:ProjectInsight",
            MaxResults = 10
        };

        // Act
        var result = await _knowledgeService.SearchKnowledgeAsync(searchRequest);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Items.Should().AllSatisfy(item => 
            item.Type.Should().Be(KnowledgeTypes.ProjectInsight));
    }
}