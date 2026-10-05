# Changelog

Mudanças nos pacotes `Shared.*` da base (prefixo `SharedPackagePrefix`) e no template. Formato inspirado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/); versões seguem [SemVer](https://semver.org/lang/pt-BR/). Mudança incompatível de API pública, de configuração ou de schema exige versão major. Como atualizar um projeto gerado: [`docs/upgrading.md`](docs/upgrading.md).

Este arquivo fica só no repositório de origem; o template não o exporta.

## [1.0.0] - não publicada

Primeira versão distribuída como pacotes. Projetos gerados antes dela têm `Shared.*` como código-fonte; a migração para pacotes está descrita em `docs/upgrading.md`.

### Adicionado
- `Shared.Kernel`: `Result`, `Error`, `ErrorType` e entidades base, sem ASP.NET Core nem EF Core (ADR-008).
- `IAccessPolicy<TRequest>` por caso de uso, registrada por varredura; caso de uso sem policy derruba o startup (ADR-008).
- `[Command]` sem argumento (chave = módulo) e com placeholders por recurso, `[Command("events:{EventId}")]` (ADR-009).
- Leitura de perfis OIDC configurável: `Oidc:RoleClaimPath`, `Oidc:RoleMap`, `Oidc:RequireTokenType`/`TokenType`.
- Inbox por consumidor em `AddIntegrationEventHandler`, com `[SkipInbox]` e `[InboxConsumer]`; retenção `Outbox:InboxRetentionDays` (ADR-005).
- Pacotes com SourceLink e PDB embutido.
- Rate limiting: limites separados `RateLimiting:AuthenticatedPermitLimit` e `AnonymousPermitLimit` (padrão `PermitLimit`), `Retry-After` do limiter, health checks isentos e políticas nomeadas por módulo com `AddFixedWindowRateLimitPolicy`.

### Alterado
- Chave da connection string fixa: `ConnectionStrings:Database` (era o nome do projeto). **Incompatível:** renomeie a variável `ConnectionStrings__<Projeto>` para `ConnectionStrings__Database`.
- Anotações de auditoria `Shared:Sensitive`/`Shared:AuditValue` (eram `<Projeto>:Sensitive`/`AuditValue`); snapshots de migração precisam do mesmo nome.
- Handlers de evento rodam em escopo DI próprio por handler (antes, um escopo por mensagem).
- `Oidc:AllowedRoles` configurado substitui a lista padrão; antes, o binder acrescentava os valores aos três perfis padrão.
- Contratos dos módulos de exemplo saíram de `Shared.Contracts` para `Shared.Contracts.Modules`, que fica no projeto.

### Corrigido
- Listagem de auditoria: `sortBy=entityName` e `sortBy=userName`, aceitos pelo validator e documentados no OpenAPI, eram ignorados e ordenavam por `occurredOn`. Agora ordenam pelo campo pedido.

### Removido
- `IModuleAccessPolicy`.
- Log `Information` de toda paginação em `PagingExtensions`.

### Migrações exigidas nos módulos
- Tabela `InboxMessages` em cada schema de módulo (`AddInbox`).
