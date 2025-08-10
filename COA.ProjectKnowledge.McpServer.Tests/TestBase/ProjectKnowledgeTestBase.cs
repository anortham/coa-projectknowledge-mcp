using COA.Mcp.Framework.Testing.Base;
using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Data.Entities;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Tools;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Linq;

namespace COA.ProjectKnowledge.McpServer.Tests.TestBase;

public abstract class ProjectKnowledgeTestBase : McpTestBase
{
    protected KnowledgeDbContext DbContext { get; private set; } = null!;
    protected SqliteConnection Connection { get; private set; } = null!;
    protected IConfiguration Configuration { get; private set; } = null!;

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        // Load test configuration
        Configuration = new ConfigurationBuilder()
            .SetBasePath(TestContext.CurrentContext.TestDirectory)
            .AddJsonFile("appsettings.test.json")
            .Build();

        services.AddSingleton(Configuration);

        // Configure in-memory SQLite database for testing
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseSqlite(Connection)
            .Options;

        DbContext = new KnowledgeDbContext(options);
        services.AddSingleton(DbContext);
        
        // Also add as scoped for services that expect it
        services.AddScoped<KnowledgeDbContext>(_ => DbContext);

        // Add common mocked services that all tests need
        AddCommonTestServices(services);

        // Add other common services
        ConfigureTestServices(services);
    }

    private void AddCommonTestServices(IServiceCollection services)
    {
        // Mock common services that most tests need
        var workspaceResolverMock = new Mock<IWorkspaceResolver>();
        workspaceResolverMock.Setup(x => x.GetCurrentWorkspace()).Returns("TestWorkspace");
        services.AddSingleton(workspaceResolverMock.Object);

        // Mock RealTimeNotificationService 
        var notificationLoggerMock = new Mock<ILogger<RealTimeNotificationService>>();
        var notificationService = new RealTimeNotificationService(
            notificationLoggerMock.Object,
            Configuration,
            null); // WebSocket service is optional
        services.AddSingleton(notificationService);

        // Add ExecutionContextService
        var executionContextLoggerMock = new Mock<ILogger<ExecutionContextService>>();
        services.AddSingleton(executionContextLoggerMock.Object);
        services.AddSingleton<ExecutionContextService>();

        // Add common tool logger mocks that most tests need
        services.AddSingleton(new Mock<ILogger<UpdateChecklistItemTool>>().Object);
        services.AddSingleton(new Mock<ILogger<CreateChecklistTool>>().Object);
        services.AddSingleton(new Mock<ILogger<CreateCheckpointTool>>().Object);
        services.AddSingleton(new Mock<ILogger<StoreKnowledgeTool>>().Object);
        services.AddSingleton(new Mock<ILogger<SearchKnowledgeTool>>().Object);
        services.AddSingleton(new Mock<ILogger<ExportKnowledgeTool>>().Object);
    }

    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
        // Override in derived classes to add specific test services
    }

    protected override void OnSetUp()
    {
        base.OnSetUp();

        // Ensure database is created with schema
        DbContext.Database.EnsureCreated();

        // Allow derived classes to seed data
        SeedTestData();
    }

    protected virtual void SeedTestData()
    {
        // Override in derived classes to seed test data
    }

    protected override void OnTearDown()
    {
        base.OnTearDown();

        // Clean up database
        DbContext?.Dispose();
        Connection?.Close();
        Connection?.Dispose();
    }


    protected async Task<KnowledgeEntity> CreateTestKnowledgeAsync(
        string type = "TestType",
        string content = "Test content",
        string? id = null)
    {
        var knowledge = new KnowledgeEntity
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Type = type,
            Content = content,
            Workspace = "TestWorkspace",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            AccessCount = 0
        };

        DbContext.Knowledge.Add(knowledge);
        await DbContext.SaveChangesAsync();
        return knowledge;
    }

    protected void AssertKnowledgeExists(string id)
    {
        var exists = DbContext.Knowledge.Any(k => k.Id == id);
        Assert.That(exists, Is.True, $"Knowledge with ID {id} should exist");
    }

    protected void AssertKnowledgeNotExists(string id)
    {
        var exists = DbContext.Knowledge.Any(k => k.Id == id);
        Assert.That(exists, Is.False, $"Knowledge with ID {id} should not exist");
    }
}