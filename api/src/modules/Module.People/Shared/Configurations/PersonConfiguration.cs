using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Module.People.Domain;
using Shared.Data;

namespace Module.People.Shared.Configurations;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ConfigureBaseEntity("People");
        builder.HasIndex(p => p.UserId).IsUnique().HasFilter("\"DeletedAt\" IS NULL AND \"UserId\" IS NOT NULL");
        builder.Property(p => p.PersonName).HasMaxLength(150).IsRequired();
        builder.Property(p => p.PersonEmail).HasMaxLength(200).IsRequired();
        builder.Property(p => p.PersonPhone).HasMaxLength(20);
        builder.Property(p => p.PersonDocument).HasMaxLength(Cpf.Size).IsFixedLength();
        builder.Property(p => p.PersonCompany).HasMaxLength(150);
        builder.Property(p => p.PersonJobTitle).HasMaxLength(100);
        builder.Property(p => p.PersonShortBio).HasMaxLength(2000);
        builder.Property(p => p.PersonPhotoUrl).HasMaxLength(500);
        builder.HasIndex(p => p.PersonEmail).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
        builder.HasIndex(p => p.PersonDocument).IsUnique().HasFilter("\"DeletedAt\" IS NULL AND \"PersonDocument\" IS NOT NULL");
        builder.HasIndex(p => p.PersonName);
    }
}
