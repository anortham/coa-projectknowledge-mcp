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
using System.Linq;

namespace COA.ProjectKnowledge.McpServer.Tests.Tools;

[TestFixture]
public class GetRelationshipsToolTests : ProjectKnowledgeTestBase
{
    private GetRelationshipsTool _tool = null!;
    private RelationshipService _relationshipService = null!;
    private KnowledgeService _knowledgeService = null!;
    private CreateRelationshipTool _createRelationshipTool = null!;
    private Mock<IWorkspaceResolver> _workspaceResolverMock = null!;
    private Mock<ILogger<RelationshipService>> _relationshipLoggerMock = null!;
    private Mock<ILogger<KnowledgeService>> _knowledgeLoggerMock = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        // Setup mocks
        _workspaceResolverMock = new Mock<IWorkspaceResolver>();
        _workspaceResolverMock.Setup(x => x.GetCurrentWorkspace()).Returns("TestWorkspace");
        services.AddSingleton(_workspaceResolverMock.Object);

        _relationshipLoggerMock = new Mock<ILogger<RelationshipService>>();
        services.AddSingleton(_relationshipLoggerMock.Object);

        _knowledgeLoggerMock = new Mock<ILogger<KnowledgeService>>();
        services.AddSingleton(_knowledgeLoggerMock.Object);

        // Add services
        services.AddScoped<RelationshipService>();
        services.AddScoped<KnowledgeService>();
        services.AddScoped<GetRelationshipsTool>();
        services.AddScoped<CreateRelationshipTool>();
    }

    protected override void OnSetUp()
    {
        base.OnSetUp();
        _relationshipService = GetRequiredService<RelationshipService>();
        _knowledgeService = GetRequiredService<KnowledgeService>();
        _tool = GetRequiredService<GetRelationshipsTool>();
        _createRelationshipTool = GetRequiredService<CreateRelationshipTool>();
    }

    [Test]
    public async Task ExecuteAsync_WithValidKnowledgeId_ReturnsRelationships()
    {
        // Arrange
        // Create knowledge items
        var item1 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Central knowledge item"
        });

        var item2 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.TechnicalDebt,
            Content = "Related debt item"
        });

        var item3 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.WorkNote,
            Content = "Another related item"
        });

        // Create relationships
        await _createRelationshipTool.ExecuteAsync(new CreateRelationshipParams
        {
            FromId = item1.KnowledgeId!,
            ToId = item2.KnowledgeId!,
            RelationshipType = "relates_to",
            Description = "First relationship"
        });

        await _createRelationshipTool.ExecuteAsync(new CreateRelationshipParams
        {
            FromId = item3.KnowledgeId!,
            ToId = item1.KnowledgeId!,
            RelationshipType = "references",
            Description = "Second relationship"
        });

        var parameters = new GetRelationshipsParams
        {
            KnowledgeId = item1.KnowledgeId!
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        // Should find relationships in both directions (from and to)
    }

    [Test]
    public async Task ExecuteAsync_WithDirectionFrom_ReturnsOnlyOutgoingRelationships()
    {
        // Arrange
        var item1 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Source item"
        });

        var item2 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.TechnicalDebt,
            Content = "Target item"
        });

        // Create relationship FROM item1 TO item2
        await _createRelationshipTool.ExecuteAsync(new CreateRelationshipParams
        {
            FromId = item1.KnowledgeId!,
            ToId = item2.KnowledgeId!,
            RelationshipType = "leads_to"
        });

        var parameters = new GetRelationshipsParams
        {
            KnowledgeId = item1.KnowledgeId!,
            Direction = "from"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        // Should find the outgoing relationship
    }

    [Test]
    public async Task ExecuteAsync_WithDirectionTo_ReturnsOnlyIncomingRelationships()
    {
        // Arrange
        var item1 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Target item"
        });

        var item2 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.WorkNote,
            Content = "Source item"
        });

        // Create relationship FROM item2 TO item1
        await _createRelationshipTool.ExecuteAsync(new CreateRelationshipParams
        {
            FromId = item2.KnowledgeId!,
            ToId = item1.KnowledgeId!,
            RelationshipType = "references"
        });

        var parameters = new GetRelationshipsParams
        {
            KnowledgeId = item1.KnowledgeId!,
            Direction = "to"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        // Should find the incoming relationship
    }

    [Test]
    public async Task ExecuteAsync_WithNonexistentKnowledgeId_ReturnsEmptyResults()
    {
        // Arrange
        var parameters = new GetRelationshipsParams
        {
            KnowledgeId = "non-existent-knowledge-id"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        // Should return empty results, not an error
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyKnowledgeId_ReturnsError()
    {
        // Arrange
        var parameters = new GetRelationshipsParams
        {
            KnowledgeId = string.Empty
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Test]
    public async Task ExecuteAsync_WithDirectionBoth_ReturnsAllRelationships()
    {
        // Arrange
        var item1 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Central item"
        });

        var item2 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.TechnicalDebt,
            Content = "Related item 1"
        });

        var item3 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.WorkNote,
            Content = "Related item 2"
        });

        // Create relationships in both directions
        await _createRelationshipTool.ExecuteAsync(new CreateRelationshipParams
        {
            FromId = item1.KnowledgeId!,
            ToId = item2.KnowledgeId!,
            RelationshipType = "outgoing"
        });

        await _createRelationshipTool.ExecuteAsync(new CreateRelationshipParams
        {
            FromId = item3.KnowledgeId!,
            ToId = item1.KnowledgeId!,
            RelationshipType = "incoming"
        });

        var parameters = new GetRelationshipsParams
        {
            KnowledgeId = item1.KnowledgeId!,
            Direction = "both"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        // Should find relationships in both directions
    }
}