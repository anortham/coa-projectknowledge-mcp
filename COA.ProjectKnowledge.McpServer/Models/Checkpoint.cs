namespace COA.ProjectKnowledge.McpServer.Models;

/// <summary>
/// Session checkpoint for state persistence
/// </summary>
public class Checkpoint : Knowledge
{
    public Checkpoint()
    {
        Type = KnowledgeTypes.Checkpoint;
        Id = ChronologicalId.Generate();
    }
    
    /// <summary>
    /// Session ID this checkpoint belongs to
    /// </summary>
    public string SessionId 
    { 
        get => GetMetadata<string>("sessionId") ?? string.Empty;
        set => SetMetadata("sessionId", value);
    }
    
    /// <summary>
    /// Sequential checkpoint number within session
    /// </summary>
    public int SequenceNumber
    {
        get => GetMetadata<int>("sequenceNumber");
        set => SetMetadata("sequenceNumber", value);
    }
    
    /// <summary>
    /// Files that were open/modified at checkpoint time
    /// </summary>
    public string[] ActiveFiles
    {
        get => GetMetadata<string[]>("activeFiles") ?? Array.Empty<string>();
        set => SetMetadata("activeFiles", value);
    }
}