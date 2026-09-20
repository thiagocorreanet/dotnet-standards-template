using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Shared.WebHost.Modules;

namespace Shared.WebHost.OpenApi;

/// <summary>Informações gerais (markdown), esquema Bearer e descrição de cada módulo como tag.</summary>
internal sealed class DocumentTransformer(IReadOnlyList<IModule> modules) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Modular API",
            Version = "v1",
            Description = """
                Template de API modular.

                ## Convenções
                - Prefixo `api/v1/{module}`; recursos no plural, sem verbos na URL.
                - Erros seguem **RFC 9457 (Problem Details)** com a extensão `code` (estável, ex.: `Events.EventNotFound`).
                - Listagens são paginadas: `page` e `pageSize` (máx. 100) e retornam `items`, `total`, `totalPages`.
                - Todo response carrega `X-Correlation-Id` (aceita o enviado pelo cliente) para correlação com logs e traces.
                - Autenticação **Bearer JWT**: obtenha um access token no provedor OIDC usando Authorization Code + PKCE.

                ## Módulos
                Cada módulo possui seu schema no banco, seus casos de uso e seu grupo de endpoints (uma tag por módulo).
                """,
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Informe apenas o token (o prefixo `Bearer` é adicionado automaticamente).",
        };

        document.Tags ??= new HashSet<OpenApiTag>();
        foreach (var module in modules)
        {
            document.Tags.Add(new OpenApiTag { Name = module.Name, Description = module.Description });
        }

        return Task.CompletedTask;
    }
}

/// <summary>Adiciona o requisito Bearer nas operações protegidas (as anônimas ficam sem cadeado).</summary>
internal sealed class SecurityOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var anonymous = metadata.OfType<IAllowAnonymous>().Any();
        var protectedEndpoint = metadata.OfType<IAuthorizeData>().Any();
        if (protectedEndpoint && !anonymous)
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [],
            });
        }

        return Task.CompletedTask;
    }
}
