using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Module.Talks.Domain;
using Shared.Data;

namespace Module.Talks.Shared.Configurations;

internal sealed class TalkConfiguration : IEntityTypeConfiguration<Talk>
{
    public void Configure(EntityTypeBuilder<Talk> builder)
    {
        builder.ConfigureBaseEntity("Talks");
        builder.Property(p => p.TalkTitle).HasMaxLength(200).IsRequired();
        builder.Property(p => p.TalkDescription).HasMaxLength(4000);
        builder.Ignore(p => p.TalkDurationMinutes);

        builder.HasIndex(p => p.EventId);
        builder.HasIndex(p => p.TrackId);
        builder.HasIndex(p => new { p.RoomId, p.TalkStart });

        builder.HasMany(p => p.Speakers).WithOne().HasForeignKey(x => x.TalkId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Contents).WithOne().HasForeignKey(x => x.TalkId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Attendances).WithOne().HasForeignKey(x => x.TalkId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Certificates).WithOne().HasForeignKey(x => x.TalkId).OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(p => p.Speakers).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude(false);
        builder.Navigation(p => p.Contents).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude(false);
        builder.Navigation(p => p.Attendances).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude(false);
        builder.Navigation(p => p.Certificates).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude(false);
    }
}
