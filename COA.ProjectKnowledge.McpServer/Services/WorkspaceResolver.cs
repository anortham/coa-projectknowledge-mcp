using Microsoft.Extensions.Configuration;

namespace COA.ProjectKnowledge.McpServer.Services;

public class WorkspaceResolver : IWorkspaceResolver
{
    private readonly IConfiguration _configuration;
    private string? _currentWorkspace;
    
    public WorkspaceResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public string GetCurrentWorkspace()
    {
        if (!string.IsNullOrEmpty(_currentWorkspace))
        {
            return _currentWorkspace;
        }
        
        var strategy = _configuration["ProjectKnowledge:Workspace:DetectionStrategy"] ?? "GitRoot";
        
        switch (strategy)
        {
            case "GitRoot":
                _currentWorkspace = DetectGitRoot() ?? GetDefaultWorkspace();
                break;
            case "Environment":
                _currentWorkspace = Environment.GetEnvironmentVariable("PROJECTKNOWLEDGE_WORKSPACE") 
                    ?? GetDefaultWorkspace();
                break;
            default:
                _currentWorkspace = GetDefaultWorkspace();
                break;
        }
        
        return _currentWorkspace;
    }
    
    private string GetDefaultWorkspace()
    {
        return _configuration["ProjectKnowledge:Workspace:DefaultWorkspace"] ?? "default";
    }
    
    private string? DetectGitRoot()
    {
        try
        {
            var currentDir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(currentDir))
            {
                if (Directory.Exists(Path.Combine(currentDir, ".git")))
                {
                    return Path.GetFileName(currentDir) ?? "git-repo";
                }
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }
        }
        catch
        {
            // Ignore errors in git detection
        }
        
        return null;
    }
    
    public void SetWorkspace(string workspace)
    {
        _currentWorkspace = workspace;
    }
}