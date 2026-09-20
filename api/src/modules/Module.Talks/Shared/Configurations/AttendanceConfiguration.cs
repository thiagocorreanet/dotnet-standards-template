using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Module.Talks.Domain;
using Shared.Data;

namespace Module.Talks.Shared.Configurations;

internal sealed class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.ConfigureBaseEntity("Attendances");
        builder.HasIndex(p => new { p.TalkId, p.PersonId }).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
        builder.HasIndex(p => p.PersonId);
    }
}
