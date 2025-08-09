namespace COA.ProjectKnowledge.McpServer.Services;

/// <summary>
/// Interface for centralized path resolution
/// </summary>
public interface IPathResolutionService
{
    /// <summary>
    /// Gets the base directory path
    /// </summary>
    /// <returns>The full path to the base directory (e.g., ".coa")</returns>
    string GetBasePath();
    
    /// <summary>
    /// Gets the knowledge directory path
    /// </summary>
    /// <returns>The full path to the knowledge directory (e.g., ".coa/knowledge")</returns>
    string GetKnowledgePath();
    
    /// <summary>
    /// Gets the logs directory path
    /// </summary>
    /// <returns>The full path to the logs directory (e.g., ".coa/knowledge/logs")</returns>
    string GetLogsPath();
    
    /// <summary>
    /// Gets the cache directory path
    /// </summary>
    /// <returns>The full path to the cache directory (e.g., ".coa/cache")</returns>
    string GetCachePath();
    
    /// <summary>
    /// Gets the exports directory path
    /// </summary>
    /// <returns>The full path to the exports directory (e.g., ".coa/exports")</returns>
    string GetExportsPath();
    
    /// <summary>
    /// Gets the backups directory path
    /// </summary>
    /// <returns>The full path to the backups directory (e.g., ".coa/backups")</returns>
    string GetBackupsPath();
    
    /// <summary>
    /// Safely checks if a directory exists
    /// </summary>
    bool DirectoryExists(string path);
    
    /// <summary>
    /// Safely checks if a file exists
    /// </summary>
    bool FileExists(string path);
    
    /// <summary>
    /// Safely gets the full path
    /// </summary>
    string GetFullPath(string path);
    
    /// <summary>
    /// Ensures a directory exists, creating it if necessary
    /// </summary>
    void EnsureDirectoryExists(string path);
}