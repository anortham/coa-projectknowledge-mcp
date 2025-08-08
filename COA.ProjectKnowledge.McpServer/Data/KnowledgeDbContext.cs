using Microsoft.EntityFrameworkCore;
using COA.ProjectKnowledge.McpServer.Data.Entities;

namespace COA.ProjectKnowledge.McpServer.Data;

public class KnowledgeDbContext : DbContext
{
    public KnowledgeDbContext(DbContextOptions<KnowledgeDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<KnowledgeEntity> Knowledge { get; set; } = null!;
    public DbSet<RelationshipEntity> Relationships { get; set; } = null!;
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure Knowledge entity
        modelBuilder.Entity<KnowledgeEntity>(entity =>
        {
            entity.ToTable("Knowledge");
            entity.HasKey(e => e.Id);
            
            // Indexes for performance
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.Workspace);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Priority);
            
            // Configure JSON columns
            entity.Property(e => e.Metadata).HasColumnType("TEXT");
            entity.Property(e => e.Tags).HasColumnType("TEXT");
        });
        
        // Configure Relationship entity
        modelBuilder.Entity<RelationshipEntity>(entity =>
        {
            entity.ToTable("Relationships");
            entity.HasKey(e => e.Id);
            
            // Configure foreign keys
            entity.HasOne(e => e.FromKnowledge)
                .WithMany(k => k.RelationshipsFrom)
                .HasForeignKey(e => e.FromId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.ToKnowledge)
                .WithMany(k => k.RelationshipsTo)
                .HasForeignKey(e => e.ToId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Indexes for performance
            entity.HasIndex(e => e.FromId);
            entity.HasIndex(e => e.ToId);
            entity.HasIndex(e => e.RelationshipType);
        });
    }
}