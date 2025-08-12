using COA.Mcp.Framework.Server;
using COA.Mcp.Framework.Server.Services;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Resources;
using COA.Mcp.Framework.TokenOptimization;
using COA.Mcp.Protocol;
using COA.Mcp.Framework.TokenOptimization.Actions;
using COA.Mcp.Framework.TokenOptimization.Caching;
using COA.Mcp.Framework.TokenOptimization.Intelligence;
using COA.Mcp.Framework.TokenOptimization.Storage;
using COA.ProjectKnowledge.McpServer.Services;
// using COA.ProjectKnowledge.McpServer.Storage; // Removed - migrated to EF Core
using COA.ProjectKnowledge.McpServer.Data;
using COA.ProjectKnowledge.McpServer.Resources;
using Microsoft.EntityFrameworkCore;
using COA.ProjectKnowledge.McpServer.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using System.Reflection;
using Serilog;

namespace COA.ProjectKnowledge.McpServer;

public class Program
{
    /// <summary>
    /// Configure shared services used by both STDIO and HTTP modes
    /// </summary>
    private static void ConfigureSharedServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register configuration
        services.AddSingleton<IConfiguration>(configuration);

        // Register core services
        services.AddSingleton<IPathResolutionService, PathResolutionService>();

        // Configure Entity Framework
        services.AddDbContext<KnowledgeDbContext>((serviceProvider, options) =>
        {
            var pathService = serviceProvider.GetRequiredService<IPathResolutionService>();
            var configuredPath = configuration["ProjectKnowledge:Database:Path"];

            string dbPath;
            if (!string.IsNullOrEmpty(configuredPath))
            {
                // Handle tilde expansion for cross-platform support
                if (configuredPath.StartsWith("~/"))
                {
                    dbPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        configuredPath.Substring(2) // Remove "~/"
                    );
                }
                else
                {
                    dbPath = configuredPath;
                }
            }
            else
            {
                // Default to user profile .coa directory with new filename
                dbPath = Path.Combine(pathService.GetKnowledgePath(), "knowledge.db");
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory))
            {
                pathService.EnsureDirectoryExists(directory);
            }

            options.UseSqlite($"Data Source={dbPath}");
        });

        // EF Core services
        services.AddScoped<KnowledgeService>();
        services.AddScoped<CheckpointService>();
        services.AddScoped<ChecklistService>();
        services.AddSingleton<IWorkspaceResolver, WorkspaceResolver>();
        services.AddSingleton<WorkspaceResolver>();

        // Relationship and export services
        services.AddScoped<RelationshipService>();
        services.AddScoped<MarkdownExportService>(); // Changed from Singleton - depends on scoped KnowledgeService
        services.AddScoped<FederationService>(); // Handles incoming knowledge from other MCP clients
        
        // WebSocket broadcast service (registered as HostedService and Singleton)
        services.AddSingleton<WebSocketBroadcastService>();
        services.AddHostedService(provider => provider.GetRequiredService<WebSocketBroadcastService>());
        
        // Real-time notification service for WebSocket broadcasts
        // Register as both interface and concrete type for DI resolution
        services.AddSingleton<RealTimeNotificationService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<RealTimeNotificationService>>();
            var config = provider.GetRequiredService<IConfiguration>();
            var webSocketService = provider.GetService<WebSocketBroadcastService>();
            return new RealTimeNotificationService(logger, config, webSocketService);
        });
        services.AddSingleton<IRealTimeNotificationService>(provider => 
            provider.GetRequiredService<RealTimeNotificationService>());
        
        // Execution context tracking service
        services.AddScoped<ExecutionContextService>();
        
        // Background service for database maintenance
        services.AddHostedService<KnowledgeMaintenanceService>();
        
        // Register Resource Provider and Registry
        // The framework provides IResourceCache (obsolete), but we need the generic version
        // Register our own instance of the generic cache to avoid using obsolete interfaces
        services.AddSingleton<IResourceCache<ReadResourceResult>, InMemoryResourceCache<ReadResourceResult>>();
        services.AddScoped<KnowledgeResourceProvider>();
        services.AddScoped<IResourceProvider>(provider => provider.GetRequiredService<KnowledgeResourceProvider>());
        
        // Register Token Optimization services (minimal set)
        services.AddSingleton<ITokenEstimator, DefaultTokenEstimator>();
        services.AddSingleton<IResponseCacheService, ResponseCacheService>();
        services.AddSingleton<IResourceStorageService, ResourceStorageService>();
        services.AddSingleton<ICacheKeyGenerator, CacheKeyGenerator>();
        
        // Register Response Builders
        services.AddScoped<ResponseBuilders.KnowledgeSearchResponseBuilder>();
        services.AddScoped<ResponseBuilders.CrossProjectSearchResponseBuilder>();
        
        // Register all MCP tools - required for DI to work with DiscoverTools
        services.AddScoped<Tools.StoreKnowledgeTool>();
        services.AddScoped<Tools.FindKnowledgeTool>(); // Enhanced search with temporal scoring
        services.AddScoped<Tools.CreateCheckpointTool>();
        services.AddScoped<Tools.GetCheckpointTool>();
        services.AddScoped<Tools.ListCheckpointsTool>();
        services.AddScoped<Tools.CreateChecklistTool>();
        services.AddScoped<Tools.GetChecklistTool>();
        services.AddScoped<Tools.UpdateChecklistItemTool>();
        services.AddScoped<Tools.GetTimelineTool>();
        services.AddScoped<Tools.GetWorkspacesTool>();
        services.AddScoped<Tools.SearchCrossProjectTool>();
        services.AddScoped<Tools.CreateRelationshipTool>();
        services.AddScoped<Tools.GetRelationshipsTool>();
        services.AddScoped<Tools.ExportKnowledgeTool>();
    }

    /// <summary>
    /// Configure Serilog with file logging only (no console to avoid breaking STDIO)
    /// </summary>
    private static void ConfigureSerilog(IConfiguration configuration)
    {
        // Create a temporary path service to get the logs directory
        var tempPathService = new PathResolutionService(configuration);
        var logsPath = tempPathService.GetLogsPath();
        tempPathService.EnsureDirectoryExists(logsPath);

        var logFile = Path.Combine(logsPath, "projectknowledge-.log");

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.File(
                logFile,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10 * 1024 * 1024, // 10MB
                retainedFileCountLimit: 7, // Keep 7 days of logs
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();
    }

    public static async Task Main(string[] args)
    {
        // Determine mode from args early
        bool isHttpMode = args.Contains("--mode") && args.Contains("http");
        bool isWebSocketMode = args.Contains("--mode") && args.Contains("websocket");

        // Load configuration early for logging setup
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Configure Serilog early - FILE ONLY (no console to avoid breaking STDIO)
        ConfigureSerilog(configuration);


        // Use framework's builder
        var builder = new McpServerBuilder()
        .WithServerInfo("ProjectKnowledge", "1.0.0");

        // Configure logging with Serilog
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSerilog(); // Use Serilog for all logging
        });

        // Configure shared services
        ConfigureSharedServices(builder.Services, configuration);

        // Auto-discover and register all tools from assembly
        builder.DiscoverTools(typeof(Program).Assembly);
        
        // Auto-discover and register all prompts from assembly
        builder.DiscoverPrompts(typeof(Program).Assembly);

        // Mode was already determined at the top

        if (isHttpMode)
        {
            // HTTP mode - run as ASP.NET Core API for federation
            await RunHttpServerAsync(configuration);
            return; // Exit after HTTP server stops
        }
        else if (isWebSocketMode)
        {
            // WebSocket mode - enable real-time bidirectional communication
            var wsPort = configuration.GetValue<int>("Mcp:Transport:WebSocket:Port", 8080);
            var wsHost = configuration.GetValue<string>("Mcp:Transport:WebSocket:Host", "localhost");
            
            builder.UseWebSocketTransport(options =>
            {
                options.Host = wsHost;
                options.Port = wsPort;
                options.UseHttps = false;
            });
        }
        else
        {
            // STDIO mode - run as MCP client with auto-started HTTP service
            builder.UseStdioTransport();

            // Auto-start HTTP service for federation
            if (configuration.GetValue<bool>("Mcp:Transport:Http:Enabled", true))
            {
                var port = configuration.GetValue<int>("Mcp:Transport:Http:Port", 5100);
                builder.UseAutoService(config =>
                {
                    config.ServiceId = "projectknowledge-http";
                    // Use dotnet to execute the DLL with quoted path for spaces
                    config.ExecutablePath = "dotnet";
                    var dllPath = Assembly.GetExecutingAssembly().Location;
                    config.Arguments = new[] { $"\"{dllPath}\"", "--mode", "http" };
                    config.Port = port;
                    config.HealthEndpoint = $"http://localhost:{port}/api/knowledge/health";
                    config.AutoRestart = true;
                    config.MaxRestartAttempts = 3;
                    config.HealthCheckIntervalSeconds = 60;
                });
            }
        }

        // Note: Resource provider registration is now handled by the framework
        // The framework's ResourceRegistry will discover IResourceProvider instances automatically

        // Initialize database before starting
        // We need to build a temporary service provider for database initialization
#pragma warning disable ASP0000 // Do not call 'IServiceCollection.BuildServiceProvider' in 'ConfigureServices'
        var tempServiceProvider = builder.Services.BuildServiceProvider();
#pragma warning restore ASP0000
        using (var scope = tempServiceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
            await EnsureDatabaseSchemaAsync(dbContext);
        }
        tempServiceProvider.Dispose();

        // Run the server
        try
        {
            // NO console output in STDIO mode - it breaks the JSON-RPC protocol!
            await builder.RunAsync();
        }
        catch (Exception ex)
        {
            // Log to file and stderr (not stdout to avoid breaking STDIO)
            Log.Fatal(ex, "Fatal error occurred during startup");

            if (!isHttpMode)
            {
                // Write to stderr in STDIO mode
                Console.Error.WriteLine($"ProjectKnowledge startup failed: {ex.Message}");
            }
            throw;
        }
        finally
        {
            // Ensure Serilog is properly flushed and closed
            Log.CloseAndFlush();
        }
    }


    private static async Task RunHttpServerAsync(IConfiguration configuration)
    {
        var port = configuration.GetValue<int>("Mcp:Transport:Http:Port", 5100);
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

        // Add Memory Caching for HTTP mode
        builder.Services.AddMemoryCache();
        
        // Configure shared services
        ConfigureSharedServices(builder.Services, configuration);

        var app = builder.Build();

        // Initialize database schema for HTTP mode
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
            await EnsureDatabaseSchemaAsync(dbContext);
        }

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

    private static async Task EnsureDatabaseSchemaAsync(KnowledgeDbContext context)
    {
        // Check if database exists
        bool databaseExists = await context.Database.CanConnectAsync();

        if (!databaseExists)
        {
            // Create new database with latest schema
            await context.Database.EnsureCreatedAsync();
            return;
        }

        // Database exists - check if it has the required columns
        try
        {
            // Try a simple query that uses the Tags column to test if it exists
            await context.Knowledge.Where(k => k.Tags != null).CountAsync();
        }
        catch (SqliteException ex) when (ex.Message.Contains("no such column"))
        {
            // Missing columns detected - recreate the database with latest schema
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();

            Console.WriteLine("Database schema updated to include new columns.");
        }
    }
}