using Microsoft.EntityFrameworkCore;
using Module.Identity.Domain;
using Shared.Data;
using Shared.Data.Migrations;

namespace Module.Identity.Shared;
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "Identity";
    public override string Schema => SchemaName;
    public DbSet<User> Users => Set<User>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.ConfigureBaseEntity("Users");
        user.Property(x => x.Issuer).HasMaxLength(512);
        user.Property(x => x.Subject).HasMaxLength(255);
        user.Property(x => x.UserName).HasMaxLength(200).Sensitive();
        user.Property(x => x.Email).HasMaxLength(254).Sensitive();
        user.HasIndex(x => new { x.Issuer, x.Subject }).IsUnique();
        base.OnModelCreating(modelBuilder);
    }
}
public sealed class IdentityDbContextFactory : DesignTimeDbContextFactoryBase<IdentityDbContext>
{
    protected override string Schema => IdentityDbContext.SchemaName;
    protected override IdentityDbContext Create(DbContextOptions<IdentityDbContext> options) => new(options);
}
