using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Module.Events.Domain;
using Shared.Data;

namespace Module.Events.Shared.Configurations;

internal sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    public void Configure(EntityTypeBuilder<Track> builder)
    {
        builder.ConfigureBaseEntity("Tracks");
        builder.Property(t => t.TrackName).HasMaxLength(120).IsRequired();
        builder.Property(t => t.TrackDescription).HasMaxLength(1000);
        builder.Property(t => t.TrackColor).HasMaxLength(7);
        builder.HasIndex(t => new { t.EventId, t.TrackName }).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
    }
}
