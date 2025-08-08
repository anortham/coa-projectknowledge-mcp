using System.ComponentModel.DataAnnotations;

namespace COA.ProjectKnowledge.McpServer.Data.Entities;

public class KnowledgeEntity
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Type { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public string? Metadata { get; set; }
    
    public string? Tags { get; set; }
    
    [MaxLength(50)]
    public string? Status { get; set; }
    
    [MaxLength(20)]
    public string? Priority { get; set; }
    
    [MaxLength(255)]
    public string? Workspace { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime ModifiedAt { get; set; }
    
    public DateTime? AccessedAt { get; set; }
    
    public int AccessCount { get; set; }
    
    public DateTime? ArchivedAt { get; set; }
    
    public DateTime? ExpiresAt { get; set; }
    
    // Navigation properties
    public ICollection<RelationshipEntity> RelationshipsFrom { get; set; } = new List<RelationshipEntity>();
    public ICollection<RelationshipEntity> RelationshipsTo { get; set; } = new List<RelationshipEntity>();
}