using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Module.Venues.Domain;
using Shared.Data;

namespace Module.Venues.Shared.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ConfigureBaseEntity("Rooms");
        builder.Property(s => s.RoomName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.RoomResources).HasMaxLength(500);
        builder.HasIndex(s => new { s.VenueId, s.RoomName }).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
    }
}
