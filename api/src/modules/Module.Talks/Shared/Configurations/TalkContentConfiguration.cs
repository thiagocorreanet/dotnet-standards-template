using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Module.Talks.Domain;
using Shared.Data;

namespace Module.Talks.Shared.Configurations;

internal sealed class TalkContentConfiguration : IEntityTypeConfiguration<TalkContent>
{
    public void Configure(EntityTypeBuilder<TalkContent> builder)
    {
        builder.ConfigureBaseEntity("TalkContents");
        builder.Property(c => c.ContentTitle).HasMaxLength(200).IsRequired();
        builder.Property(c => c.ContentUrl).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.ContentDescription).HasMaxLength(1000);
        builder.HasIndex(c => c.TalkId);
    }
}
