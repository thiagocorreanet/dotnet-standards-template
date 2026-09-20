using Microsoft.AspNetCore.Hosting;
using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Data;
using Shared.Messaging;
using Shared.Observability;
using Shared.WebHost.Middleware;
using Shared.WebHost.Modules;
using Shared.WebHost.OpenApi;
using Shared.WebHost.Security;
using Shared.WebHost.Operations;

namespace Shared.WebHost;

/// <summary>
/// Composição do host: tudo o que é transversal (observabilidade, segurança, dados, mensageria, OpenAPI) fica aqui,
/// e o <c>Program.cs</c> do host tem 5 linhas. Um futuro Host.Worker reutiliza as mesmas peças.
/// </summary>
public static class ModularWebHostExtensions
{
    public const string ModulesKey = "ModularApi.Modules";

    public static IHostApplicationBuilder AddModularWebHost(this WebApplicationBuilder builder, string serviceName)
    {
        // Mensagens humanas em pt-BR, sem alterar serialização, datas ou números dos contratos.
        ValidatorOptions.Global.LanguageManager.Culture = CultureInfo.GetCultureInfo("pt-BR");
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.AddServerHeader = false;
            o.Limits.MaxRequestBodySize = 1_048_576;
            o.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
        });
        var modules = ModuleDiscovery.Discover();
        builder.Services.AddSingleton<IReadOnlyList<IModule>>(modules);

        builder.AddObservability(serviceName);
        builder.AddSharedData();
        builder.AddSharedMessaging();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ICurrentUser, CurrentUser>();
        builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
        {
            // Status gerados pelo middleware também seguem o idioma das mensagens da API.
            if (!ctx.ProblemDetails.Extensions.ContainsKey("code"))
                ctx.ProblemDetails.Title = ctx.ProblemDetails.Status switch
                {
                    400 => "Requisição inválida",
                    401 => "Não autenticado",
                    403 => "Acesso negado",
                    404 => "Recurso não encontrado",
                    405 => "Método não permitido",
                    406 => "Formato de resposta não aceito",
                    413 => "Corpo da requisição muito grande",
                    415 => "Tipo de conteúdo não suportado",
                    429 => "Muitas requisições",
                    500 => "Erro interno",
                    503 => "Serviço indisponível",
                    _ => ctx.ProblemDetails.Title,
                };
            ctx.ProblemDetails.Extensions.TryAdd("traceId", System.Diagnostics.Activity.Current?.TraceId.ToString() ?? ctx.HttpContext.TraceIdentifier);
            ctx.ProblemDetails.Instance ??= ctx.HttpContext.Request.Path;
        });
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            o.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

        builder.Services.AddOpenApi("v1", o =>
        {
            o.AddDocumentTransformer((doc, ctx, ct) => new DocumentTransformer(ctx.ApplicationServices.GetRequiredService<IReadOnlyList<IModule>>()).TransformAsync(doc, ctx, ct));
            o.AddOperationTransformer<SecurityOperationTransformer>();
        });

        AddSecurity(builder);
        AddRateLimiting(builder);
        AddCors(builder);
        AddHealthChecks(builder);

        builder.Services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            // Os padrões de loopback permanecem; apenas proxies explicitamente configurados são adicionados.
            foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
                o.KnownProxies.Add(IPAddress.Parse(proxy));
        });

        foreach (var module in modules)
        {
            module.ConfigureServices(builder);
        }

        return builder;
    }

    public static WebApplication UseModularWebHost(this WebApplication app)
    {
        var modules = app.Services.GetRequiredService<IReadOnlyList<IModule>>();

        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseObservability();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseCors();
        app.UseMiddleware<IngressRateLimitMiddleware>();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("OpenApi:Enabled"))
        {
            app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
            app.MapOpenApi("/openapi/{documentName}.yaml").AllowAnonymous();
            // Persistência de autenticação desligada de forma explícita: o substituto EnablePersistentAuthentication()
            // só sabe ligar, e ScalarDocumentationTests exige "persistAuth":false no HTML publicado.
#pragma warning disable CS0618 // Rever quando o Scalar remover WithPersistentAuthentication.
            app.MapScalarApiReference(o => o
                .WithTitle("Modular API")
                .WithOpenApiRoutePattern("/openapi/{documentName}.json")
                .AddDocument("v1", "Modular API v1")
                .AddPreferredSecuritySchemes("Bearer")
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                .WithPersistentAuthentication(false)
                .DisableTelemetry()
                .DisableDefaultFonts()
                .DisableAgent())
                .AllowAnonymous();
#pragma warning restore CS0618
            app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription().AllowAnonymous();
        }

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") }).AllowAnonymous();
        app.MapOperations();

        foreach (var module in modules)
        {
            module.MapEndpoints(app);
        }

        return app;
    }

    private static void AddSecurity(WebApplicationBuilder builder)
    {
        builder.AddOidcAuthentication();

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Administration, p => p.RequireRole(DefaultRoles.Administrator))
            .AddPolicy(Policies.Management, p => p.RequireRole(DefaultRoles.Administrator, DefaultRoles.Organizer));
    }

    private static void AddRateLimiting(WebApplicationBuilder builder)
    {
        var limit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 300);
        var window = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ctx.User.FindFirst(OidcOptions.UserIdClaim)?.Value ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromSeconds(window), QueueLimit = 0 }));
            o.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.ContentType = "application/problem+json";
                await ctx.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Muitas requisições",
                    Detail = "Limite de requisições excedido. Tente novamente em instantes.",
                    Type = Shared.Http.Results.ResultHttpExtensions.ProblemTypeBase + "RateLimitExceeded",
                }, ct);
            };
        });
    }

    private static void AddCors(WebApplicationBuilder builder)
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Any(x => !Uri.TryCreate(x, UriKind.Absolute, out var uri) || x.Contains('*') ||
            uri.GetLeftPart(UriPartial.Authority) != x ||
            (uri.Scheme != "https" && !(builder.Environment.IsDevelopment() && uri.Scheme == "http"))))
            throw new InvalidOperationException("Cors:AllowedOrigins deve conter origens explícitas.");
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("X-Correlation-Id", "Location")));
    }

    private static void AddHealthChecks(WebApplicationBuilder builder)
    {
        var cs = builder.Configuration.GetConnectionString(DataServiceCollectionExtensions.ConnectionStringName) ?? string.Empty;
        builder.Services.AddHealthChecks()
            .AddNpgSql(cs, name: "postgres", tags: ["ready"], failureStatus: HealthStatus.Unhealthy);
    }

    /// <summary>Atalho para módulos: registra validators FluentValidation do assembly.</summary>
    public static IServiceCollection AddModuleValidators(this IServiceCollection services, System.Reflection.Assembly assembly)
    {
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        return services;
    }
}
