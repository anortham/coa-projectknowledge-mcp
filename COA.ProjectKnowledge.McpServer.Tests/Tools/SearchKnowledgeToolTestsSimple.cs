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
using COA.Mcp.Framework.TokenOptimization.Caching;

namespace COA.ProjectKnowledge.McpServer.Tests.Tools;

[TestFixture]
public class SearchKnowledgeToolTestsSimple : ProjectKnowledgeTestBase
{
    private SearchKnowledgeTool _tool = null!;
    private Mock<IWorkspaceResolver> _workspaceResolverMock = null!;
    private Mock<ILogger<SearchKnowledgeTool>> _toolLoggerMock = null!;
    private Mock<IResponseCacheService> _cacheServiceMock = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        // Setup specific mocks for this test
        _toolLoggerMock = new Mock<ILogger<SearchKnowledgeTool>>();
        services.AddSingleton(_toolLoggerMock.Object);

        var knowledgeLoggerMock = new Mock<ILogger<KnowledgeService>>();
        services.AddSingleton(knowledgeLoggerMock.Object);

        var checkpointLoggerMock = new Mock<ILogger<CheckpointService>>();
        services.AddSingleton(checkpointLoggerMock.Object);

        var resourceProviderLoggerMock = new Mock<ILogger<COA.ProjectKnowledge.McpServer.Resources.KnowledgeResourceProvider>>();
        services.AddSingleton(resourceProviderLoggerMock.Object);

        var builderLoggerMock = new Mock<ILogger<COA.ProjectKnowledge.McpServer.ResponseBuilders.KnowledgeSearchResponseBuilder>>();
        services.AddSingleton(builderLoggerMock.Object);

        _cacheServiceMock = new Mock<IResponseCacheService>();
        services.AddSingleton(_cacheServiceMock.Object);

        var resourceCacheMock = new Mock<COA.Mcp.Framework.Interfaces.IResourceCache>();
        services.AddSingleton(resourceCacheMock.Object);

        var tokenEstimatorMock = new Mock<COA.Mcp.Framework.TokenOptimization.ITokenEstimator>();
        services.AddSingleton(tokenEstimatorMock.Object);
        
        // Add notification service mock
        var notificationServiceMock = new Mock<IRealTimeNotificationService>();
        services.AddSingleton(notificationServiceMock.Object);

        // Add real services
        services.AddScoped<KnowledgeService>();
        services.AddScoped<CheckpointService>();
        services.AddScoped<COA.ProjectKnowledge.McpServer.Resources.KnowledgeResourceProvider>();
        services.AddScoped<COA.ProjectKnowledge.McpServer.ResponseBuilders.KnowledgeSearchResponseBuilder>();
        services.AddScoped<SearchKnowledgeTool>();
    }

    protected override void OnSetUp()
    {
        base.OnSetUp();
        _tool = GetRequiredService<SearchKnowledgeTool>();
    }

    [Test]
    public async Task ExecuteAsync_WithValidQuery_ReturnsSuccess()
    {
        // Arrange
        var parameters = new SearchKnowledgeParams
        {
            Query = "test",
            MaxResults = 10
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyQuery_ReturnsSuccess()
    {
        // Arrange
        var parameters = new SearchKnowledgeParams
        {
            Query = string.Empty,
            MaxResults = 10
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        // Empty query is handled gracefully - returns success with no results
        result.Success.Should().BeTrue();
    }

    [Test]
    public async Task ExecuteAsync_WithTypeFilter_ReturnsSuccess()
    {
        // Arrange
        var parameters = new SearchKnowledgeParams
        {
            Query = "type:ProjectInsight test",
            MaxResults = 10
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        // Note: May return no results, but should not throw exception
    }
}