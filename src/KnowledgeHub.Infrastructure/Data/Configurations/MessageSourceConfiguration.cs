using KnowledgeHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Data.Configurations;

public class MessageSourceConfiguration : IEntityTypeConfiguration<MessageSource>
{
    public void Configure(EntityTypeBuilder<MessageSource> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.MessageId);
        builder.HasIndex(s => s.DocumentChunkId);
    }
}
