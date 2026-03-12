using KnowledgeHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Data.Configurations;

public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Content).IsRequired();
        builder.Property(c => c.EmbeddingId).HasMaxLength(100);

        builder.HasIndex(c => c.DocumentId);
        builder.HasIndex(c => c.EmbeddingId);
    }
}
