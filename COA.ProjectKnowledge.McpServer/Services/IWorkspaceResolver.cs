namespace COA.ProjectKnowledge.McpServer.Services;

public interface IWorkspaceResolver
{
    string GetCurrentWorkspace();
    string NormalizeWorkspaceName(string workspaceName);
    string GetCanonicalWorkspaceName(string workspaceName);
}