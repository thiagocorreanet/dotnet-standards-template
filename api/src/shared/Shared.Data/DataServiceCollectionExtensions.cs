using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Shared.Data.Interceptors;
using Shared.Data.Migrations;
using Shared.Data.Outbox;
using Shared.Data.Transactions;

namespace Shared.Data;

public static class DataServiceCollectionExtensions
{
    public const string ConnectionStringName = "ModularApi";

    /// <summary>Infraestrutura de dados compartilhada (interceptors, registro de contextos, migrador). Chamado uma vez pelo host.</summary>
    public static IHostApplicationBuilder AddSharedData(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton<AuditSaveChangesInterceptor>();
        builder.Services.TryAddSingleton<QueryTagInterceptor>();
        builder.Services.TryAddSingleton<ModuleDbContextRegistry>();
        builder.Services.TryAddSingleton<ICommandTransactionObserver, NullCommandTransactionObserver>();
        builder.Services.AddOptions<CommandTransactionOptions>().BindConfiguration("Transactions")
            .Validate(o => o.MaxAttempts is >= 1 and <= 5 && o.LockTimeoutSeconds is >= 1 and <= 60
                && o.CommandTimeoutSeconds is >= 1 and <= 120, "Transactions inválido")
            .ValidateOnStart();
        builder.Services.AddSingleton<DatabaseMigrationHostedService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<DatabaseMigrationHostedService>());
        return builder;
    }

    /// <summary>
    /// Registra DbContext scoped, schema e histórico próprios, interceptors de auditoria/outbox e a fonte Outbox.
    /// A fronteira de comando controla retry com escopo novo; não há retry transparente no DbContext.
    /// </summary>
    public static IHostApplicationBuilder AddModuleDbContext<TContext>(this IHostApplicationBuilder builder, string schema)
        where TContext : DbContext, IModuleDbContext
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} não configurada");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} não configurada");
        var connectionSettings = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        if (connectionSettings.IncludeErrorDetail || connectionSettings.LogParameters)
            throw new InvalidOperationException("Include Error Detail e Log Parameters devem permanecer desativados.");

        builder.Services.AddDbContext<TContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema);
                npgsql.MigrationsAssembly(typeof(TContext).Assembly.GetName().Name);
                // Retry da unidade inteira, com contexto novo, na fronteira de execução do comando.
                npgsql.CommandTimeout(builder.Configuration.GetValue("Transactions:CommandTimeoutSeconds", 30));
            });
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>(), sp.GetRequiredService<QueryTagInterceptor>());
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
            if (builder.Environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
            }
        });

        builder.Services.AddSingleton<IOutboxStore, OutboxStore<TContext>>();

        var registry = builder.Services.BuildRegistryPlaceholder();
        registry.Add(typeof(TContext), schema);
        return builder;
    }

    private static ModuleDbContextRegistry BuildRegistryPlaceholder(this IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ModuleDbContextRegistry));
        if (descriptor?.ImplementationInstance is ModuleDbContextRegistry existing)
        {
            return existing;
        }

        var registry = new ModuleDbContextRegistry();
        services.Replace(ServiceDescriptor.Singleton(registry));
        return registry;
    }
}
