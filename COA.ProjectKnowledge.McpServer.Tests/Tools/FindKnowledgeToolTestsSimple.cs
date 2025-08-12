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
using System.Threading.Tasks;

namespace COA.ProjectKnowledge.McpServer.Tests.Tools;

[TestFixture]
public class FindKnowledgeToolTestsSimple : ProjectKnowledgeTestBase
{
    private FindKnowledgeTool _tool = null!;
    private IServiceScope _testScope = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        
        var toolLoggerMock = new Mock<ILogger<FindKnowledgeTool>>();
        services.AddSingleton(toolLoggerMock.Object);
        
        var builderLoggerMock = new Mock<ILogger<KnowledgeSearchResponseBuilder>>();
        services.AddSingleton(builderLoggerMock.Object);
        
        services.AddScoped<KnowledgeSearchResponseBuilder>();
    }

    [SetUp]
    public override void SetUp()
    {
        base.SetUp();
        
        // Create scope for the test to ensure proper service lifetimes
        _testScope = ServiceProvider.CreateScope();
        var knowledgeService = _testScope.ServiceProvider.GetRequiredService<KnowledgeService>();
        var responseBuilder = _testScope.ServiceProvider.GetRequiredService<KnowledgeSearchResponseBuilder>();
        var logger = _testScope.ServiceProvider.GetRequiredService<ILogger<FindKnowledgeTool>>();
        
        _tool = new FindKnowledgeTool(knowledgeService, responseBuilder, logger);
    }
    
    [TearDown]
    public override void TearDown()
    {
        _testScope?.Dispose();
        base.TearDown();
    }

    [Test]
    public async Task ExecuteAsync_SimpleTest_Works()
    {
        // Arrange - Add test data using the same context the tool will use
        var context = _testScope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        
        var item = new KnowledgeEntity
        {
            Id = ChronologicalId.Generate(),
            Type = KnowledgeTypes.ProjectInsight,
            Content = "Test content for search",
            Workspace = "test-workspace",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            AccessedAt = DateTime.UtcNow,
            AccessCount = 0,
            Tags = System.Text.Json.JsonSerializer.Serialize(new[] { "test", "search" }),
            Priority = "normal",
            Status = "active",
            Metadata = "{}"
        };
        
        context.Knowledge.Add(item);
        await context.SaveChangesAsync();
        
        // Act
        var parameters = new FindKnowledgeParams
        {
            Query = "test",
            Workspace = "test-workspace",
            MaxResults = 10
        };
        
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].Content.Should().Contain("Test content");
    }
    
    [Test]
    public async Task ExecuteAsync_EmptyDatabase_ReturnsNoResults()
    {
        // Arrange - Clear database using the same context the tool will use
        var context = _testScope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        context.Knowledge.RemoveRange(context.Knowledge);
        await context.SaveChangesAsync();
        
        // Act
        var parameters = new FindKnowledgeParams
        {
            Query = "",
            Workspace = "test-workspace",
            MaxResults = 10
        };
        
        var result = await _tool.ExecuteAsync(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Items.Should().BeEmpty();
    }
}