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
public class CreateRelationshipToolTests : ProjectKnowledgeTestBase
{
    private CreateRelationshipTool _tool = null!;
    private RelationshipService _relationshipService = null!;
    private KnowledgeService _knowledgeService = null!;
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
        services.AddScoped<CreateRelationshipTool>();
    }

    protected override void OnSetUp()
    {
        base.OnSetUp();
        _relationshipService = GetRequiredService<RelationshipService>();
        _knowledgeService = GetRequiredService<KnowledgeService>();
        _tool = GetRequiredService<CreateRelationshipTool>();
    }

    [Test]
    public async Task ExecuteAsync_WithValidKnowledgeItems_CreatesRelationship()
    {
        // Arrange
        // First create two knowledge items to relate
        var item1 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Authentication uses JWT tokens"
        });

        var item2 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.TechnicalDebt,
            Content = "JWT token expiration needs configuration"
        });

        var parameters = new CreateRelationshipParams
        {
            FromId = item1.KnowledgeId!,
            ToId = item2.KnowledgeId!,
            RelationshipType = "relates_to",
            Description = "JWT implementation and its technical debt"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();

        // Verify relationship was created in database
        var relationships = DbContext.Relationships
            .Where(r => r.FromId == item1.KnowledgeId && r.ToId == item2.KnowledgeId)
            .ToList();
        relationships.Should().HaveCount(1);
        relationships.First().RelationshipType.Should().Be("relates_to");
        relationships.First().Description.Should().Be("JWT implementation and its technical debt");
    }

    [Test]
    public async Task ExecuteAsync_WithNonexistentFromId_ReturnsError()
    {
        // Arrange
        var item = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.WorkNote,
            Content = "Valid knowledge item"
        });

        var parameters = new CreateRelationshipParams
        {
            FromId = "non-existent-id",
            ToId = item.KnowledgeId!,
            RelationshipType = "relates_to"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("KNOWLEDGE_NOT_FOUND");
    }

    [Test]
    public async Task ExecuteAsync_WithNonexistentToId_ReturnsError()
    {
        // Arrange
        var item = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.WorkNote,
            Content = "Valid knowledge item"
        });

        var parameters = new CreateRelationshipParams
        {
            FromId = item.KnowledgeId!,
            ToId = "non-existent-id",
            RelationshipType = "relates_to"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("KNOWLEDGE_NOT_FOUND");
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyFromId_ReturnsError()
    {
        // Arrange
        var parameters = new CreateRelationshipParams
        {
            FromId = string.Empty,
            ToId = "some-id",
            RelationshipType = "relates_to"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyToId_ReturnsError()
    {
        // Arrange
        var parameters = new CreateRelationshipParams
        {
            FromId = "some-id",
            ToId = string.Empty,
            RelationshipType = "relates_to"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Test]
    public async Task ExecuteAsync_WithDifferentRelationshipTypes_CreatesCorrectType()
    {
        // Arrange
        var item1 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Parent insight"
        });

        var item2 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Child insight"
        });

        var parameters = new CreateRelationshipParams
        {
            FromId = item1.KnowledgeId!,
            ToId = item2.KnowledgeId!,
            RelationshipType = "parent_of",
            Description = "Parent-child relationship"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify correct relationship type
        var relationship = DbContext.Relationships
            .First(r => r.FromId == item1.KnowledgeId && r.ToId == item2.KnowledgeId);
        relationship.RelationshipType.Should().Be("parent_of");
    }

    [Test]
    public async Task ExecuteAsync_WithOptionalDescription_StoresDescription()
    {
        // Arrange
        var item1 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.TechnicalDebt,
            Content = "Performance issue"
        });

        var item2 = await _knowledgeService.StoreKnowledgeAsync(new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Performance optimization strategy"
        });

        var description = "This debt item is addressed by the optimization strategy";

        var parameters = new CreateRelationshipParams
        {
            FromId = item1.KnowledgeId!,
            ToId = item2.KnowledgeId!,
            RelationshipType = "addresses",
            Description = description
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify description is stored
        var relationship = DbContext.Relationships
            .First(r => r.FromId == item1.KnowledgeId && r.ToId == item2.KnowledgeId);
        relationship.Description.Should().Be(description);
    }
}