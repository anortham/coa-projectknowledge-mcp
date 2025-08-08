using COA.Mcp.Framework.Server;
using COA.Mcp.Framework.Server.Services;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Storage;
using COA.ProjectKnowledge.McpServer.Data;
using Microsoft.EntityFrameworkCore;
using COA.ProjectKnowledge.McpServer.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace COA.ProjectKnowledge.McpServer;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Determine mode from args early
        bool isHttpMode = args.Contains("--mode") && args.Contains("http");

        // Load configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Use framework's builder
        var builder = new McpServerBuilder()
            .WithServerInfo("ProjectKnowledge", "1.0.0");

        // Configure logging
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            // Since we redirected stdout to stderr in STDIO mode, we can keep logging
            logging.SetMinimumLevel(LogLevel.Warning); // Reduce verbosity
            logging.AddConfiguration(configuration.GetSection("Logging"));
        });

        // Register configuration
        builder.Services.AddSingleton<IConfiguration>(configuration);

        // Register core services
        builder.Services.AddSingleton<IPathResolutionService, PathResolutionService>();
        
        // Configure Entity Framework
        builder.Services.AddDbContext<KnowledgeDbContext>((serviceProvider, options) =>
        {
            var pathService = serviceProvider.GetRequiredService<IPathResolutionService>();
            var dbPath = configuration["ProjectKnowledge:Database:Path"] 
                ?? Path.Combine(pathService.GetKnowledgePath(), "workspace.db");
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory))
            {
                pathService.EnsureDirectoryExists(directory);
            }
            
            options.UseSqlite($"Data Source={dbPath}");
        });
        
        // Keep old service for compatibility during migration
        builder.Services.AddSingleton<KnowledgeDatabase>();
        builder.Services.AddSingleton<KnowledgeService>();
        
        // Add new EF Core service
        builder.Services.AddScoped<KnowledgeServiceEF>();
        builder.Services.AddSingleton<IWorkspaceResolver, WorkspaceResolver>();
        builder.Services.AddSingleton<WorkspaceResolver>();
        builder.Services.AddSingleton<CheckpointService>();
        builder.Services.AddSingleton<ChecklistService>();
        builder.Services.AddSingleton<RelationshipService>();
        builder.Services.AddSingleton<MarkdownExportService>();
        
        // Federation services
        builder.Services.AddHttpClient("Federation", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton<FederationService>();

        // Register tools in DI first (required for constructor dependencies)
        builder.Services.AddScoped<StoreKnowledgeTool>();
        builder.Services.AddScoped<SearchKnowledgeTool>();
        builder.Services.AddScoped<CreateCheckpointTool>();
        builder.Services.AddScoped<GetCheckpointTool>();
        builder.Services.AddScoped<ListCheckpointsTool>();
        builder.Services.AddScoped<CreateChecklistTool>();
        builder.Services.AddScoped<UpdateChecklistItemTool>();
        builder.Services.AddScoped<GetChecklistTool>();
        builder.Services.AddScoped<CreateRelationshipTool>();
        builder.Services.AddScoped<GetRelationshipsTool>();
        builder.Services.AddScoped<ExportKnowledgeTool>();
        builder.Services.AddScoped<GetTimelineTool>();

        // Discover and register all tools from assembly
        builder.DiscoverTools(typeof(Program).Assembly);

        // Mode was already determined at the top
        
        if (isHttpMode)
        {
            // HTTP mode - run as ASP.NET Core API for federation
            await RunHttpServerAsync(configuration);
            return; // Exit after HTTP server stops
        }
        else
        {
            // STDIO mode - run as MCP client with auto-started HTTP service
            builder.UseStdioTransport();
            
            // Auto-start HTTP service for federation
            if (configuration.GetValue<bool>("ProjectKnowledge:Federation:Enabled", true))
            {
                var port = configuration.GetValue<int>("ProjectKnowledge:Federation:Port", 5100);
                builder.UseAutoService(config =>
                {
                    config.ServiceId = "projectknowledge-http";
                    config.ExecutablePath = Assembly.GetExecutingAssembly().Location;
                    config.Arguments = new[] { "--mode", "http" };
                    config.Port = port;
                    config.HealthEndpoint = $"http://localhost:{port}/api/knowledge/health";
                    config.AutoRestart = true;
                    config.MaxRestartAttempts = 3;
                    config.HealthCheckIntervalSeconds = 60;
                });
            }
        }

        // Initialize database before starting
        var serviceProvider = builder.Services.BuildServiceProvider();
        
        // Run EF Core migrations
        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }
        
        // Also initialize old database for compatibility
        var database = serviceProvider.GetRequiredService<KnowledgeDatabase>();
        await database.InitializeAsync();

        // Run the server
        try
        {
            // NO console output in STDIO mode - it breaks the JSON-RPC protocol!
            await builder.RunAsync();
        }
        catch (Exception ex)
        {
            // In case of error, we can write to stderr (not stdout)
            if (!isHttpMode)
            {
                System.Diagnostics.Debug.WriteLine($"Server error: {ex}");
            }
            throw;
        }
    }
    
    private static async Task RunHttpServerAsync(IConfiguration configuration)
    {
        var port = configuration.GetValue<int>("ProjectKnowledge:Federation:Port", 5100);
        var builder = WebApplication.CreateBuilder();
        
        // Configure services
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "ProjectKnowledge API", Version = "v1" });
        });
        
        // Add CORS
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                var origins = configuration.GetSection("ProjectKnowledge:Federation:AllowedOrigins").Get<string[]>() 
                    ?? new[] { "*" };
                policy.WithOrigins(origins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });
        
        // Register our services
        builder.Services.AddSingleton<IConfiguration>(configuration);
        builder.Services.AddSingleton<IPathResolutionService, PathResolutionService>();
        builder.Services.AddSingleton<KnowledgeDatabase>();
        builder.Services.AddSingleton<KnowledgeService>();
        builder.Services.AddSingleton<IWorkspaceResolver, WorkspaceResolver>();
        builder.Services.AddSingleton<WorkspaceResolver>();
        builder.Services.AddSingleton<CheckpointService>();
        builder.Services.AddSingleton<ChecklistService>();
        builder.Services.AddSingleton<RelationshipService>();
        builder.Services.AddSingleton<MarkdownExportService>();
        builder.Services.AddHttpClient("Federation", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton<FederationService>();
        
        var app = builder.Build();
        
        // Initialize database
        var database = app.Services.GetRequiredService<KnowledgeDatabase>();
        await database.InitializeAsync();
        
        // Configure pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        
        app.UseCors();
        app.MapControllers();
        
        // Custom port binding
        app.Urls.Add($"http://localhost:{port}");
        
        await app.RunAsync();
    }
}