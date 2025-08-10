using COA.ProjectKnowledge.McpServer.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace COA.ProjectKnowledge.McpServer.Services;

/// <summary>
/// Background service for performing knowledge maintenance tasks like indexing, cleanup, and optimization.
/// Implements IHostedService for automatic lifecycle management.
/// </summary>
public class KnowledgeMaintenanceService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KnowledgeMaintenanceService> _logger;
    private readonly TimeSpan _maintenanceInterval;
    private readonly TimeSpan _indexingInterval;
    
    public KnowledgeMaintenanceService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<KnowledgeMaintenanceService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        
        // Configure intervals from settings
        _maintenanceInterval = TimeSpan.FromMinutes(
            configuration.GetValue<int>("Mcp:Features:BackgroundServices:MaintenanceIntervalMinutes", 60));
        _indexingInterval = TimeSpan.FromMinutes(
            configuration.GetValue<int>("Mcp:Features:BackgroundServices:IndexingIntervalMinutes", 30));
    }

    public bool IsEnabled => _configuration.GetValue<bool>("Mcp:Features:BackgroundServices", true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation("Background maintenance service is disabled");
            return;
        }

        _logger.LogInformation("Knowledge maintenance service started");
        
        // Initial delay before starting maintenance
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        var lastMaintenance = DateTime.UtcNow;
        var lastIndexing = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                
                // Run indexing tasks
                if (now - lastIndexing >= _indexingInterval)
                {
                    await RunIndexingTasksAsync(stoppingToken);
                    lastIndexing = now;
                }
                
                // Run maintenance tasks
                if (now - lastMaintenance >= _maintenanceInterval)
                {
                    await RunMaintenanceTasksAsync(stoppingToken);
                    lastMaintenance = now;
                }

                // Wait before next check (check every minute)
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in knowledge maintenance service");
                // Continue running despite errors
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Knowledge maintenance service stopped");
    }

    /// <summary>
    /// Runs knowledge indexing tasks for better search performance
    /// </summary>
    private async Task RunIndexingTasksAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting indexing tasks");
        
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();

        try
        {
            // Update FTS index statistics
            await context.Database.ExecuteSqlRawAsync("INSERT INTO knowledge_fts(knowledge_fts) VALUES('optimize');", cancellationToken);
            
            // Update access counts and last accessed times for popular items
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE Knowledge SET AccessedAt = datetime('now') WHERE AccessCount > 10 AND AccessedAt < @cutoff",
                new[] { new Microsoft.Data.Sqlite.SqliteParameter("@cutoff", cutoffDate) },
                cancellationToken);

            _logger.LogDebug("Indexing tasks completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during indexing tasks");
        }
    }

    /// <summary>
    /// Runs maintenance tasks like cleanup and optimization
    /// </summary>
    private async Task RunMaintenanceTasksAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting maintenance tasks");

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        
        try
        {
            // Clean up orphaned relationships
            var orphanedRelationships = await context.Database.ExecuteSqlRawAsync(
                @"DELETE FROM Relationships 
                  WHERE FromId NOT IN (SELECT Id FROM Knowledge) 
                  OR ToId NOT IN (SELECT Id FROM Knowledge)",
                cancellationToken);

            if (orphanedRelationships > 0)
            {
                _logger.LogInformation("Cleaned up {Count} orphaned relationships", orphanedRelationships);
            }

            // Archive very old items based on configuration
            var archiveAfterDays = _configuration.GetValue<int>("ProjectKnowledge:Maintenance:ArchiveAfterDays", 365);
            if (archiveAfterDays > 0)
            {
                var archiveDate = DateTime.UtcNow.AddDays(-archiveAfterDays);
                var archivedCount = await context.Database.ExecuteSqlRawAsync(
                    "UPDATE Knowledge SET Status = 'archived' WHERE CreatedAt < @archiveDate AND Status != 'archived'",
                    new[] { new Microsoft.Data.Sqlite.SqliteParameter("@archiveDate", archiveDate) },
                    cancellationToken);

                if (archivedCount > 0)
                {
                    _logger.LogInformation("Archived {Count} old knowledge items", archivedCount);
                }
            }

            // Vacuum database if needed (optimize storage)
            var dbSize = await GetDatabaseSizeAsync(context, cancellationToken);
            var vacuumThresholdMB = _configuration.GetValue<int>("ProjectKnowledge:Maintenance:VacuumThresholdMB", 100);
            
            if (dbSize > vacuumThresholdMB * 1024 * 1024)
            {
                _logger.LogInformation("Database size is {SizeMB}MB, running VACUUM", dbSize / 1024 / 1024);
                await context.Database.ExecuteSqlRawAsync("VACUUM;", cancellationToken);
                _logger.LogInformation("Database VACUUM completed");
            }

            // Update statistics
            await context.Database.ExecuteSqlRawAsync("ANALYZE;", cancellationToken);

            _logger.LogDebug("Maintenance tasks completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during maintenance tasks");
        }
    }

    /// <summary>
    /// Gets the current database size in bytes
    /// </summary>
    private async Task<long> GetDatabaseSizeAsync(KnowledgeDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            var result = await context.Database.SqlQuery<long>(
                $"SELECT page_count * page_size as size FROM pragma_page_count(), pragma_page_size()")
                .FirstOrDefaultAsync(cancellationToken);
            
            return result;
        }
        catch
        {
            return 0;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Knowledge maintenance service is stopping...");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Knowledge maintenance service stopped");
    }
}