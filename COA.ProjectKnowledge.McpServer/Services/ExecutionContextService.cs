using COA.Mcp.Framework;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace COA.ProjectKnowledge.McpServer.Services;

/// <summary>
/// Service for managing and tracking tool execution context
/// </summary>
public class ExecutionContextService
{
    private readonly ILogger<ExecutionContextService> _logger;
    private readonly AsyncLocal<ToolExecutionContext?> _currentContext = new();
    
    public ExecutionContextService(ILogger<ExecutionContextService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Gets the current execution context
    /// </summary>
    public ToolExecutionContext? Current => _currentContext.Value;
    
    /// <summary>
    /// Creates a new execution context for a tool
    /// </summary>
    public ToolExecutionContext CreateContext(
        string toolName,
        string? sessionId = null,
        string? userId = null,
        Dictionary<string, object?>? customData = null)
    {
        var context = new ToolExecutionContext
        {
            ToolName = toolName,
            ExecutionId = GenerateExecutionId(),
            StartTime = DateTimeOffset.UtcNow,
            SessionId = sessionId,
            UserId = userId,
            Logger = _logger,
            CustomData = customData ?? new Dictionary<string, object?>()
        };
        
        // Add default custom data
        context.CustomData["MachineName"] = Environment.MachineName;
        context.CustomData["ProcessId"] = Environment.ProcessId;
        context.CustomData["ThreadId"] = Environment.CurrentManagedThreadId;
        
        _currentContext.Value = context;
        
        _logger.LogInformation(
            "Execution context created for {ToolName} with ID {ExecutionId}",
            toolName, context.ExecutionId);
        
        return context;
    }
    
    /// <summary>
    /// Runs a tool action within an execution context
    /// </summary>
    public async Task<TResult> RunWithContextAsync<TResult>(
        string toolName,
        Func<ToolExecutionContext, Task<TResult>> action,
        string? sessionId = null,
        Dictionary<string, object?>? customData = null)
    {
        var context = CreateContext(toolName, sessionId, customData: customData);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug(
                "Starting execution of {ToolName} [{ExecutionId}]",
                toolName, context.ExecutionId);
            
            var result = await action(context);
            
            stopwatch.Stop();
            context.Metrics.ExecutionTime = stopwatch.Elapsed;
            context.Metrics.TotalTime = DateTimeOffset.UtcNow - context.StartTime;
            
            _logger.LogInformation(
                "Completed execution of {ToolName} [{ExecutionId}] in {ElapsedMs}ms",
                toolName, context.ExecutionId, stopwatch.ElapsedMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            context.Metrics.ExecutionTime = stopwatch.Elapsed;
            
            _logger.LogError(ex,
                "Failed execution of {ToolName} [{ExecutionId}] after {ElapsedMs}ms",
                toolName, context.ExecutionId, stopwatch.ElapsedMilliseconds);
            
            throw;
        }
        finally
        {
            // Log performance metrics
            LogPerformanceMetrics(context);
            _currentContext.Value = null;
        }
    }
    
    /// <summary>
    /// Records a custom metric in the current context
    /// </summary>
    public void RecordMetric(string name, object value)
    {
        if (_currentContext.Value != null)
        {
            _currentContext.Value.Metrics.CustomMetrics[name] = value;
        }
    }
    
    /// <summary>
    /// Adds custom data to the current context
    /// </summary>
    public void AddContextData(string key, object? value)
    {
        if (_currentContext.Value != null)
        {
            _currentContext.Value.CustomData[key] = value;
        }
    }
    
    private string GenerateExecutionId()
    {
        // Generate a shorter, more readable ID
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = Random.Shared.Next(1000, 9999);
        return $"{timestamp}-{random}";
    }
    
    private void LogPerformanceMetrics(ToolExecutionContext context)
    {
        if (context.Metrics.TotalTime.TotalMilliseconds > 1000)
        {
            _logger.LogWarning(
                "Slow execution detected for {ToolName} [{ExecutionId}]: {TotalMs}ms",
                context.ToolName, context.ExecutionId, context.Metrics.TotalTime.TotalMilliseconds);
        }
        
        if (context.Metrics.CustomMetrics.Any())
        {
            _logger.LogDebug(
                "Custom metrics for {ToolName} [{ExecutionId}]: {Metrics}",
                context.ToolName, context.ExecutionId, 
                string.Join(", ", context.Metrics.CustomMetrics.Select(kvp => $"{kvp.Key}={kvp.Value}")));
        }
    }
}