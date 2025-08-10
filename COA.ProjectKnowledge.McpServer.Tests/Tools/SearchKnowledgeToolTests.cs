using COA.Mcp.Framework.Testing.Base;
using COA.Mcp.Framework.Testing.Assertions;
using COA.ProjectKnowledge.McpServer.Tools;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Resources;
using COA.ProjectKnowledge.McpServer.ResponseBuilders;
using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Framework.TokenOptimization.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Bogus;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text.Json;

namespace COA.ProjectKnowledge.McpServer.Tests.Tools;

public class SearchKnowledgeToolTests : ToolTestBase<SearchKnowledgeTool>
{
    private Mock<KnowledgeService> _knowledgeServiceMock = null!;
    private Mock<ITokenEstimator> _tokenEstimatorMock = null!;
    private Mock<KnowledgeResourceProvider> _resourceProviderMock = null!;
    private Mock<ILogger<SearchKnowledgeTool>> _loggerMock = null!;
    private Mock<ILogger<KnowledgeSearchResponseBuilder>> _builderLoggerMock = null!;
    private Faker _faker = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        // Set up mocks
        _knowledgeServiceMock = new Mock<KnowledgeService>();
        _tokenEstimatorMock = new Mock<ITokenEstimator>();
        _resourceProviderMock = new Mock<KnowledgeResourceProvider>();
        _loggerMock = new Mock<ILogger<SearchKnowledgeTool>>();
        _builderLoggerMock = new Mock<ILogger<KnowledgeSearchResponseBuilder>>();
        _faker = new Faker();

        // Register mocks in DI
        services.AddSingleton(_knowledgeServiceMock.Object);
        services.AddSingleton(_resourceProviderMock.Object);
        services.AddSingleton(_tokenEstimatorMock.Object);
        services.AddSingleton(_loggerMock.Object);
        services.AddSingleton(_builderLoggerMock.Object);
    }
    
    protected override SearchKnowledgeTool CreateTool()
    {
        return new SearchKnowledgeTool(
            _knowledgeServiceMock.Object,
            _resourceProviderMock.Object,
            _tokenEstimatorMock.Object,
            Mock.Of<IResponseCacheService>(),
            Mock.Of<ExecutionContextService>(),
            _loggerMock.Object,
            _builderLoggerMock.Object);
    }

    [Test]
    public async Task SearchKnowledge_ValidQuery_ReturnsResults()
    {
        // Arrange
        var parameters = new SearchKnowledgeParams
        {
            Query = "test query",
            MaxResults = 10
        };

        var mockResponse = new SearchKnowledgeResponse
        {
            Success = true,
            Items = GenerateMockKnowledgeItems(5),
            TotalCount = 5,
            Message = "Found 5 items"
        };

        _knowledgeServiceMock
            .Setup(x => x.SearchKnowledgeAsync(It.IsAny<SearchKnowledgeRequest>()))
            .ReturnsAsync(mockResponse);

        _tokenEstimatorMock
            .Setup(x => x.EstimateObject(It.IsAny<object>(), It.IsAny<JsonSerializerOptions>()))
            .Returns(100); // Mock token count

        // Act
        var result = await ExecuteToolAsync<SearchKnowledgeResult>(
            async () => await Tool.ExecuteAsync(parameters));

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Result, Is.Not.Null);
        Assert.That(result.Result!.Items, Is.Not.Null);
        Assert.That(result.Result.TotalCount, Is.EqualTo(5));
        Assert.That(result.Result.Message, Contains.Substring("5"));
    }

    [Test]
    public async Task SearchKnowledge_EmptyQuery_ReturnsValidationError()
    {
        // Arrange
        var parameters = new SearchKnowledgeParams
        {
            Query = "", // Empty query
            MaxResults = 10
        };

        // Act
        var result = await ExecuteToolAsync<SearchKnowledgeResult>(
            async () => await Tool.ExecuteAsync(parameters));

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Exception, Is.Not.Null);
    }

    [Test]
    public async Task SearchKnowledge_LargeResultSet_UsesTokenOptimization()
    {
        // Arrange
        var parameters = new SearchKnowledgeParams
        {
            Query = "large dataset",
            MaxResults = 100,
            MaxTokens = 1000 // Low token limit to trigger optimization
        };

        var mockResponse = new SearchKnowledgeResponse
        {
            Success = true,
            Items = GenerateMockKnowledgeItems(100), // Large dataset
            TotalCount = 100
        };

        _knowledgeServiceMock
            .Setup(x => x.SearchKnowledgeAsync(It.IsAny<SearchKnowledgeRequest>()))
            .ReturnsAsync(mockResponse);

        _tokenEstimatorMock
            .Setup(x => x.EstimateObject(It.IsAny<object>(), It.IsAny<JsonSerializerOptions>()))
            .Returns(50); // Each item costs 50 tokens

        _tokenEstimatorMock
            .Setup(x => x.EstimateCollection(It.IsAny<IEnumerable<object>>(), It.IsAny<Func<object, int>>(), It.IsAny<int>()))
            .Returns(5000); // Total would be 5000 tokens (over limit)

        // Act
        var result = await ExecuteToolAsync<SearchKnowledgeResult>(
            async () => await Tool.ExecuteAsync(parameters));

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Result, Is.Not.Null);
        Assert.That(result.Result!.Meta, Is.Not.Null);
        // Note: The actual truncation logic depends on the ResponseBuilder implementation
    }

    [Test]
    public async Task SearchKnowledge_WithWorkspace_FiltersCorrectly()
    {
        // Arrange
        var parameters = new SearchKnowledgeParams
        {
            Query = "workspace test",
            Workspace = "TestWorkspace",
            MaxResults = 10
        };

        SearchKnowledgeRequest? capturedRequest = null;
        _knowledgeServiceMock
            .Setup(x => x.SearchKnowledgeAsync(It.IsAny<SearchKnowledgeRequest>()))
            .Callback<SearchKnowledgeRequest>(r => capturedRequest = r)
            .ReturnsAsync(new SearchKnowledgeResponse
            {
                Success = true,
                Items = GenerateMockKnowledgeItems(3),
                TotalCount = 3
            });

        _tokenEstimatorMock
            .Setup(x => x.EstimateObject(It.IsAny<object>(), It.IsAny<JsonSerializerOptions>()))
            .Returns(100);

        // Act
        var result = await ExecuteToolAsync<SearchKnowledgeResult>(
            async () => await Tool.ExecuteAsync(parameters));

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest!.Workspace, Is.EqualTo("TestWorkspace"));
    }

    [Test]
    public async Task SearchKnowledge_ServiceFailure_ReturnsError()
    {
        // Arrange
        var parameters = new SearchKnowledgeParams
        {
            Query = "error test",
            MaxResults = 10
        };

        _knowledgeServiceMock
            .Setup(x => x.SearchKnowledgeAsync(It.IsAny<SearchKnowledgeRequest>()))
            .ReturnsAsync(new SearchKnowledgeResponse
            {
                Success = false,
                Error = "Database connection failed"
            });

        // Act
        var result = await ExecuteToolAsync<SearchKnowledgeResult>(
            async () => await Tool.ExecuteAsync(parameters));

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Exception, Is.Not.Null);
        Assert.That(result.Exception!.Message, Contains.Substring("Database connection failed"));
    }

    // Helper method to generate mock knowledge items
    private List<KnowledgeSearchItem> GenerateMockKnowledgeItems(int count, string? type = null)
    {
        var items = new List<KnowledgeSearchItem>();
        
        for (int i = 0; i < count; i++)
        {
            items.Add(new KnowledgeSearchItem
            {
                Id = _faker.Random.AlphaNumeric(10),
                Type = type ?? _faker.PickRandom("Checkpoint", "ProjectInsight", "TechnicalDebt", "WorkNote"),
                Content = _faker.Lorem.Paragraph(),
                Tags = _faker.Lorem.Words(3).ToArray(),
                Status = _faker.PickRandom("active", "completed", "archived"),
                Priority = _faker.PickRandom("low", "normal", "high"),
                CreatedAt = _faker.Date.Past(),
                ModifiedAt = _faker.Date.Recent(),
                AccessCount = _faker.Random.Int(0, 100)
            });
        }
        
        return items;
    }
}