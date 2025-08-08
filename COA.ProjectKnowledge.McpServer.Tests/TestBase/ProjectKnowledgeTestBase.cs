using COA.Mcp.Framework.Testing.Base;
using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Data.Entities;
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

        // Add other common services
        ConfigureTestServices(services);
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