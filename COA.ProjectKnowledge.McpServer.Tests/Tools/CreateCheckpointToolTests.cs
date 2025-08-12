using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Tools;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Tests.TestBase;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace COA.ProjectKnowledge.McpServer.Tests.Tools;

[TestFixture]
public class CreateCheckpointToolTests : ProjectKnowledgeTestBase
{
    private CreateCheckpointTool _tool = null!;
    private CheckpointService _checkpointService = null!;
    private Mock<IWorkspaceResolver> _workspaceResolverMock = null!;
    private Mock<ILogger<CheckpointService>> _loggerMock = null!;
    private Mock<IRealTimeNotificationService> _notificationServiceMock = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        // Setup mocks
        _workspaceResolverMock = new Mock<IWorkspaceResolver>();
        _workspaceResolverMock.Setup(x => x.GetCurrentWorkspace()).Returns("TestWorkspace");
        services.AddSingleton(_workspaceResolverMock.Object);

        _loggerMock = new Mock<ILogger<CheckpointService>>();
        services.AddSingleton(_loggerMock.Object);

        _notificationServiceMock = new Mock<IRealTimeNotificationService>();
        services.AddSingleton(_notificationServiceMock.Object);

        // Add services
        services.AddScoped<CheckpointService>();
        services.AddScoped<CreateCheckpointTool>();
    }

    protected override void OnSetUp()
    {
        base.OnSetUp();
        _checkpointService = GetRequiredService<CheckpointService>();
        _tool = GetRequiredService<CreateCheckpointTool>();
    }

    [Test]
    public async Task ExecuteAsync_WithValidRequest_CreatesCheckpoint()
    {
        // Arrange
        var parameters = new CreateCheckpointParams
        {
            Content = "## Progress\n- Implemented authentication\n- Added user registration",
            SessionId = "test-session-001",
            ActiveFiles = new[] { "AuthService.cs", "UserController.cs" }
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.CheckpointId.Should().NotBeNullOrEmpty();
        result.SessionId.Should().Be("test-session-001");
        result.SequenceNumber.Should().Be(0); // First checkpoint in session

        // Verify in database
        var checkpoint = await GetDbContext().Knowledge.FindAsync(result.CheckpointId);
        checkpoint.Should().NotBeNull();
        checkpoint!.Type.Should().Be(KnowledgeTypes.Checkpoint);
        checkpoint.Content.Should().Contain("Implemented authentication");
    }

    [Test]
    public async Task ExecuteAsync_WithActiveFiles_StoresActiveFilesInMetadata()
    {
        // Arrange
        var parameters = new CreateCheckpointParams
        {
            Content = "Working on authentication module",
            SessionId = "test-session-002",
            ActiveFiles = new[] { "AuthService.cs", "LoginController.cs", "TokenHelper.cs" }
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify active files are stored in metadata
        var checkpoint = await GetDbContext().Knowledge.FindAsync(result.CheckpointId);
        checkpoint.Should().NotBeNull();
        checkpoint!.Metadata.Should().Contain("activeFiles");
    }

    [Test]
    public async Task ExecuteAsync_WithExistingSession_IncrementsSequenceNumber()
    {
        // Arrange
        // Create first checkpoint
        await _tool.ExecuteAsync(new CreateCheckpointParams
        {
            Content = "First checkpoint",
            SessionId = "session-increment-test"
        });

        // Create second checkpoint
        var parameters = new CreateCheckpointParams
        {
            Content = "Second checkpoint",
            SessionId = "session-increment-test"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.SequenceNumber.Should().Be(1); // Should increment from previous
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyContent_ReturnsError()
    {
        // Arrange
        var parameters = new CreateCheckpointParams
        {
            Content = string.Empty,
            SessionId = "test-session-empty"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("CHECKPOINT_VALIDATION_ERROR");
    }

    [Test]
    public async Task ExecuteAsync_WithNoSessionId_GeneratesSessionId()
    {
        // Arrange
        var parameters = new CreateCheckpointParams
        {
            Content = "Checkpoint without session ID",
            // SessionId is null/empty
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.SessionId.Should().NotBeNullOrEmpty();
        result.SessionId.Should().StartWith("session-");
    }

    [Test]
    public async Task ExecuteAsync_WithMultilineContent_PreservesFormatting()
    {
        // Arrange
        var content = @"## Accomplished
- Fixed authentication bug
- Added unit tests

## Next Steps
1. Deploy to staging
2. Update documentation";

        var parameters = new CreateCheckpointParams
        {
            Content = content,
            SessionId = "formatting-test"
        };

        // Act
        var result = await _tool.ExecuteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify formatting is preserved
        var checkpoint = await GetDbContext().Knowledge.FindAsync(result.CheckpointId);
        checkpoint.Should().NotBeNull();
        checkpoint!.Content.Should().Contain("## Accomplished");
        checkpoint.Content.Should().Contain("## Next Steps");
        checkpoint.Content.Should().Contain("1. Deploy to staging");
    }
}