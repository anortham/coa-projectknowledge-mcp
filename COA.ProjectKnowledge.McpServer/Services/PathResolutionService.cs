using Microsoft.Extensions.Configuration;

namespace COA.ProjectKnowledge.McpServer.Services;

/// <summary>
/// Centralized path resolution service for all .coa directory operations
/// </summary>
public class PathResolutionService : IPathResolutionService
{
    private readonly IConfiguration _configuration;
    private readonly string _basePath;
    
    public PathResolutionService(IConfiguration configuration)
    {
        _configuration = configuration;
        _basePath = InitializeBasePath();
    }
    
    private string InitializeBasePath()
    {
        // Allow override via configuration
        var basePath = _configuration["ProjectKnowledge:BasePath"];
        
        if (string.IsNullOrEmpty(basePath))
        {
            // Default to ~/.coa (cross-platform user directory)
            basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                PathConstants.BaseDirectoryName
            );
        }
        else if (!Path.IsPathRooted(basePath))
        {
            // If relative path provided, resolve from user directory
            basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                basePath
            );
        }
        
        return Path.GetFullPath(basePath);
    }
    
    public string GetBasePath()
    {
        return _basePath;
    }
    
    public string GetKnowledgePath()
    {
        var knowledgePath = Path.Combine(_basePath, PathConstants.KnowledgeDirectoryName);
        return knowledgePath;
    }
    
    public string GetLogsPath()
    {
        var logsPath = Path.Combine(_basePath, PathConstants.LogsDirectoryName);
        return logsPath;
    }
    
    public string GetCachePath()
    {
        var cachePath = Path.Combine(_basePath, PathConstants.CacheDirectoryName);
        return cachePath;
    }
    
    public string GetExportsPath()
    {
        var exportsPath = Path.Combine(_basePath, PathConstants.ExportsDirectoryName);
        return exportsPath;
    }
    
    public string GetBackupsPath()
    {
        var backupsPath = Path.Combine(_basePath, PathConstants.BackupsDirectoryName);
        return backupsPath;
    }
    
    // Safe file system operations implementation
    
    public bool DirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }
    
    public bool FileExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch
        {
            return false;
        }
    }
    
    public string GetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
    
    public void EnsureDirectoryExists(string path)
    {
        if (!DirectoryExists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}