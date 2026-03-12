using KnowledgeHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Data.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Content).IsRequired();

        builder.HasIndex(m => m.ConversationId);

        builder.HasMany(m => m.Sources)
            .WithOne(s => s.Message)
            .HasForeignKey(s => s.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
