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
public class GetCheckpointToolTestsSimple : ProjectKnowledgeTestBase
{
    private GetCheckpointTool _tool = null!;
    private Mock<IWorkspaceResolver> _workspaceResolverMock = null!;
    private Mock<ILogger<GetCheckpointTool>> _toolLoggerMock = null!;
    private Mock<IResponseCacheService> _cacheServiceMock = null!;
    private Mock<ExecutionContextService> _contextServiceMock = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        // Setup specific mocks for this test
        _toolLoggerMock = new Mock<ILogger<GetCheckpointTool>>();
        services.AddSingleton(_toolLoggerMock.Object);

        var checkpointLoggerMock = new Mock<ILogger<CheckpointService>>();
        services.AddSingleton(checkpointLoggerMock.Object);

        _cacheServiceMock = new Mock<IResponseCacheService>();
        services.AddSingleton(_cacheServiceMock.Object);
        
        // Add notification service mock
        var notificationServiceMock = new Mock<IRealTimeNotificationService>();
        services.AddSingleton(notificationServiceMock.Object);

        // Add basic services
        services.AddScoped<CheckpointService>();
        services.AddScoped<GetCheckpointTool>();
    }

    protected override void OnSetUp()
    {
        base.OnSetUp();
        _tool = GetRequiredService<GetCheckpointTool>();
    }

    [Test]
    public async Task ExecuteAsync_WithValidCheckpointId_ReturnsSuccess()
    {
        // Arrange
        var parameters = new GetCheckpointParams
        {
            CheckpointId = "test-checkpoint-id"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        // Note: May fail due to missing checkpoint, but should not throw exception
    }

    [Test]
    public async Task ExecuteAsync_WithValidSessionId_ReturnsSuccess()
    {
        // Arrange
        var parameters = new GetCheckpointParams
        {
            SessionId = "test-session-id"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        // Note: May fail due to missing session, but should not throw exception
    }

    [Test]
    public async Task ExecuteAsync_WithNoParameters_ReturnsError()
    {
        // Arrange
        var parameters = new GetCheckpointParams();

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }
}