using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Module.Audit.Domain;

namespace Module.Audit.Shared.Configurations;

internal sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("AuditRecords");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Module).HasMaxLength(50).IsRequired();
        builder.Property(r => r.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(r => r.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Operation).HasMaxLength(20).IsRequired();
        builder.Property(r => r.PreviousData).HasColumnType("jsonb");
        builder.Property(r => r.NewData).HasColumnType("jsonb");
        builder.Property(r => r.UserName).HasMaxLength(150);
        builder.Property(r => r.TraceId).HasMaxLength(64);
        builder.HasIndex(r => new { r.Module, r.EntityName, r.EntityId });
        builder.HasIndex(r => r.OccurredOn);
        builder.HasIndex(r => r.UserId);
    }
}
