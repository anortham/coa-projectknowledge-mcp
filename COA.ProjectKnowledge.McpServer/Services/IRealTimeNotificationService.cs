using COA.ProjectKnowledge.McpServer.Models;

namespace COA.ProjectKnowledge.McpServer.Services;

public interface IRealTimeNotificationService
{
    Task NotifyKnowledgeCreatedAsync(Knowledge knowledge);
    Task NotifyKnowledgeUpdatedAsync(Knowledge knowledge);
    Task NotifyKnowledgeDeletedAsync(string knowledgeId);
    Task NotifyCheckpointCreatedAsync(string sessionId, int sequenceNumber);
    Task NotifyChecklistItemCompletedAsync(string checklistId, string itemId);
    Task NotifyRelationshipCreatedAsync(string fromId, string toId, string relationshipType);
}