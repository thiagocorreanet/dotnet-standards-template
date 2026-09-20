using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Module.Events.Domain;
using Shared.Data;

namespace Module.Events.Shared.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ConfigureBaseEntity("Events");
        builder.Property(e => e.EventName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.EventDescription).HasMaxLength(4000);
        builder.Property(e => e.EventRemoteUrl).HasMaxLength(500);
        builder.Property(e => e.EventCancellationReason).HasMaxLength(1000);
        builder.HasIndex(e => e.EventStartDate);
        builder.HasIndex(e => e.EventStatus);
        builder.HasIndex(e => e.VenueId);
        builder.HasMany(e => e.Registrations).WithOne(i => i.Event).HasForeignKey(i => i.EventId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Registrations).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude(false);
        builder.HasMany(e => e.Tracks).WithOne(t => t.Event).HasForeignKey(t => t.EventId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Tracks).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude(false);
    }
}
