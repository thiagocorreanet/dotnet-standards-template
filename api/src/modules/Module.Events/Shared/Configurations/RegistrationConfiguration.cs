using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Module.Events.Domain;
using Shared.Data;

namespace Module.Events.Shared.Configurations;

internal sealed class RegistrationConfiguration : IEntityTypeConfiguration<Registration>
{
    public void Configure(EntityTypeBuilder<Registration> builder)
    {
        builder.ConfigureBaseEntity("Registrations");
        builder.Ignore(i => i.IsConfirmed);
        builder.HasIndex(i => new { i.EventId, i.RegistrationStatus });

        // Uma única inscrição confirmada por pessoa em cada evento; protege contra corrida entre requisições concorrentes.
        builder.HasIndex(i => new { i.EventId, i.PersonId })
            .IsUnique()
            .HasDatabaseName("IX_Registrations_EventId_PersonId_Confirmed")
            .HasFilter("\"RegistrationStatus\" = 'Confirmed'");
    }
}
