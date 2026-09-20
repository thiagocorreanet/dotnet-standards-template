using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Module.Talks.Domain;
using Shared.Data;

namespace Module.Talks.Shared.Configurations;

internal sealed class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        builder.ConfigureBaseEntity("Certificates");
        builder.Property(c => c.CertificateCode).HasMaxLength(CertificateCodeGenerator.Size).IsFixedLength().IsRequired();
        builder.HasIndex(c => c.CertificateCode).IsUnique();
        builder.HasIndex(c => new { c.TalkId, c.PersonId }).IsUnique();
        builder.Property(c => c.PersonNameSnapshot).HasMaxLength(150).Sensitive();
        builder.Property(c => c.TalkTitleSnapshot).HasMaxLength(200);
        builder.Property(c => c.EventNameSnapshot).HasMaxLength(200);
    }
}
