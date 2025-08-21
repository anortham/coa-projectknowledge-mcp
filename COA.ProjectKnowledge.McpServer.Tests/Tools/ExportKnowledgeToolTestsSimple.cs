using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Tests.TestBase;
using COA.ProjectKnowledge.McpServer.Tools;
using COA.ProjectKnowledge.McpServer.Resources;
using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Protocol;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Threading.Tasks;

namespace COA.ProjectKnowledge.McpServer.Tests.Tools;

[TestFixture]
public class ExportKnowledgeToolTestsSimple : ProjectKnowledgeTestBase
{
    private ExportKnowledgeTool _tool = null!;
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

        // Add mock for path resolution service
        var pathResolutionMock = new Mock<IPathResolutionService>();
        var testBasePath = Path.Combine(Path.GetTempPath(), "ProjectKnowledgeTests");
        var testExportsPath = Path.Combine(testBasePath, "exports");
        pathResolutionMock.Setup(x => x.GetBasePath()).Returns(testBasePath);
        pathResolutionMock.Setup(x => x.GetExportsPath()).Returns(testExportsPath);
        pathResolutionMock.Setup(x => x.EnsureDirectoryExists(It.IsAny<string>()))
            .Callback<string>(path => Directory.CreateDirectory(path));
        services.AddSingleton(pathResolutionMock.Object);

        // Add logger mocks for services
        var relationshipLoggerMock = new Mock<ILogger<RelationshipService>>();
        services.AddSingleton(relationshipLoggerMock.Object);
        
        var markdownLoggerMock = new Mock<ILogger<MarkdownExportService>>();
        services.AddSingleton(markdownLoggerMock.Object);
        
        var exportLoggerMock = new Mock<ILogger<ExportKnowledgeTool>>();
        services.AddSingleton(exportLoggerMock.Object);

        // Add missing service mocks - use interface mocks instead of concrete classes
        var resourceProviderMock = new Mock<IResourceProvider>();
        services.AddSingleton<KnowledgeResourceProvider>(provider => 
        {
            // Create with minimal dependencies
            var knowledgeService = provider.GetRequiredService<KnowledgeService>();
            var resourceCache = provider.GetRequiredService<IResourceCache<ReadResourceResult>>();
            var logger = provider.GetRequiredService<ILogger<KnowledgeResourceProvider>>();
            return new KnowledgeResourceProvider(knowledgeService, resourceCache, logger);
        });
        
        var resourceCacheMock = new Mock<IResourceCache<ReadResourceResult>>();
        services.AddSingleton(resourceCacheMock.Object);
        
        var resourceProviderLoggerMock = new Mock<ILogger<KnowledgeResourceProvider>>();
        services.AddSingleton(resourceProviderLoggerMock.Object);
        
        var tokenEstimatorMock = new Mock<ITokenEstimator>();
        services.AddSingleton(tokenEstimatorMock.Object);

        // Add services
        services.AddScoped<KnowledgeService>();
        services.AddScoped<RelationshipService>();
        services.AddScoped<MarkdownExportService>();
        services.AddScoped<ExportKnowledgeTool>();
    }

    protected override void OnSetUp()
    {
        base.OnSetUp();
        _tool = GetRequiredService<ExportKnowledgeTool>();
    }

    [Test]
    public async Task ExecuteAsync_WithDefaultSettings_ReturnsSuccess()
    {
        // Arrange
        var parameters = new ExportKnowledgeParams();

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Test]
    public async Task ExecuteAsync_WithTypeFilter_ReturnsSuccess()
    {
        // Arrange
        var parameters = new ExportKnowledgeParams
        {
            FilterByType = KnowledgeTypes.ProjectInsight
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Test]
    public async Task ExecuteAsync_WithCustomWorkspace_ReturnsSuccess()
    {
        // Arrange
        var parameters = new ExportKnowledgeParams
        {
            Workspace = "CustomWorkspace"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }
}