using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Module.Venues.Domain;
using Shared.Data;

namespace Module.Venues.Shared.Configurations;

internal sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.ConfigureBaseEntity("Venues");
        builder.Property(l => l.VenueName).HasMaxLength(150).IsRequired();
        builder.Property(l => l.VenueDescription).HasMaxLength(1000);
        builder.Property(l => l.AddressStreet).HasMaxLength(200);
        builder.Property(l => l.AddressNumber).HasMaxLength(20);
        builder.Property(l => l.AddressNeighborhood).HasMaxLength(100);
        builder.Property(l => l.AddressCity).HasMaxLength(100).IsRequired();
        builder.Property(l => l.AddressState).HasMaxLength(2).IsFixedLength().IsRequired();
        builder.Property(l => l.AddressPostalCode).HasMaxLength(10);
        builder.HasIndex(l => l.VenueName).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
        builder.HasMany(l => l.Rooms).WithOne(s => s.Venue).HasForeignKey(s => s.VenueId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(l => l.Rooms).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude(false);
    }
}
