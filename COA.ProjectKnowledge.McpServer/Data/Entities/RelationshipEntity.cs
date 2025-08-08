using System.ComponentModel.DataAnnotations;

namespace COA.ProjectKnowledge.McpServer.Data.Entities;

public class RelationshipEntity
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string FromId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string ToId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string RelationshipType { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public KnowledgeEntity FromKnowledge { get; set; } = null!;
    public KnowledgeEntity ToKnowledge { get; set; } = null!;
}