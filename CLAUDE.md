# CLAUDE.md — contrato de trabalho neste repositório

Leia este arquivo antes de qualquer alteração, em qualquer pasta. Ele descreve como esta base funciona, o que não pode ser quebrado e onde cada coisa mora.

`AGENTS.md` traz os mesmos invariantes em forma condensada, para outros agentes. Ao mudar uma regra, atualize os dois arquivos.

Dois guias complementam este contrato e devem ser consultados durante a implementação:

- [Boas práticas de .NET e C# nesta base](docs/dotnet-practices.md) — como escrever o código do dia a dia.
- [Arquitetura aplicada](docs/architecture-practices.md) — onde uma regra mora e quando um padrão novo se justifica.

## O que é esta base

API .NET 10 em **monolito modular**: um processo ASP.NET Core, um PostgreSQL com **um schema por módulo**, Keycloak/OIDC como emissor de tokens, Outbox transacional para eventos, auditoria automática por interceptor e OpenTelemetry por uma rota única.

O repositório também é um template (`dotnet new modular-api`). Identidade e Auditoria são o núcleo; Locais, Pessoas, Eventos e Palestras são **exemplo removível** e a geração padrão não os inclui.

Não é arquitetura em camadas por projeto (`Domain`/`Application`/`Infrastructure` como assemblies). É **vertical slice dentro de cada módulo**, com fronteiras protegidas por testes de arquitetura em `api/tests/Tests.Architecture`.

## Mapa

```text
api/src/hosts/Host.Api          composição, comandos migrate e bootstrap-identity
api/src/modules/Module.<Name>   um módulo: Domain/, Shared/, UseCases/<Name>/
api/src/shared/Shared.Contracts contratos genéricos: ICurrentUser, PagedResult, identidade, eventos de integração, auditoria
api/src/shared/Shared.Contracts.Modules  I<Name>ModuleApi e eventos dos módulos deste projeto (não é pacote)
api/src/shared/Shared.Kernel    Result/Error, BaseEntity e interfaces de entidade; sem ASP.NET nem EF
api/src/shared/Shared.Data      ModuleDbContext, interceptors, Outbox, migrações
api/src/shared/Shared.Http      IUseCase, IEndpoint, IAccessPolicy, decorators, validação
api/src/shared/Shared.Messaging OutboxProcessor, OutboxProbe, publicação in-process
api/src/shared/Shared.Observability  Serilog, OTel, sanitização de logs e traces
api/src/shared/Shared.WebHost   pipeline HTTP, OIDC, descoberta de módulos, OpenAPI/Scalar
api/tests/                      Unit, Integration, Functional, Architecture
docs/                           decisões, guias, segurança, operação
scripts/                        ambiente local, smokes, validações, teste do template
infra/                          configuração local e produtiva, separadas
```

Dentro de um módulo:

```text
Module.<Name>/
  Domain/                    entidades, value objects, erros do módulo
  Shared/                    DbContext, IModule, telemetria, handlers, regras de acesso comuns
  Shared/Configurations/     IEntityTypeConfiguration por entidade
  Migrations/                migrações EF do schema do módulo
  UseCases/<Name>/           Endpoint, Request, Response, UseCase, Validator, AccessPolicy
```

## Invariantes

Estas regras protegem correção, segurança ou integridade dos dados. Quebrar qualquer uma é defeito, não preferência de estilo.

1. **Módulo não conhece módulo.** Um `Module.*` referencia apenas `Shared.*`. Comunicação síncrona usa interfaces de `Shared.Contracts`; assíncrona usa evento de integração. Nunca HTTP local entre módulos do mesmo processo, nunca entidade EF ou `IQueryable` atravessando o contrato.
2. **`Domain/` só depende de `System.*`, `Shared.Kernel` e `Shared.Contracts`.** Não conhece `UseCases/` nem `Shared/` do módulo, EF Core, ASP.NET Core, HTTP nem o container. O `Shared.Kernel` também não referencia ASP.NET Core nem EF Core; os testes de arquitetura garantem as duas coisas.
3. **Exatamente um `DbContext` por módulo.** `AddUseCasesFromAssembly` resolve o contexto com `Single(...)`; um segundo `DbContext` no assembly quebra a composição em runtime, não em compilação.
4. **Toda escrita de negócio é um caso de uso marcado com `[Command]`.** A decoração transacional abre a transação e adquire o advisory lock **antes** de qualquer leitura ou validação de invariante. `[Command]` usa o nome do módulo como chave; `[Command("events:{EventId}")]` trava só o recurso, com placeholders lidos de propriedades `Guid`/`string` do request (placeholder inexistente falha no startup); `[Command("chave-fixa")]` coordena um conjunto explícito, inclusive entre módulos.
5. **Autorização de recurso é obrigatória.** Cada caso de uso tem exatamente uma `IAccessPolicy<TRequest>` no próprio slice; sem ela, a composição falha no startup e o teste de arquitetura reprova. `RequireAuthorization()` no endpoint é barreira adicional, não substitui a policy.
6. **Sem repositório genérico, sem Unit of Work extra, sem MediatR, sem broker, sem microsserviço.** EF Core é usado diretamente. Um teste de arquitetura recusa qualquer tipo com `Repository` no nome. Padrão novo exige driver concreto registrado em ADR.
7. **Regra de negócio não lança exceção.** Casos de uso retornam `Result<T>` com `Error("Module.Reason", ...)`. Exceção é falha inesperada ou contrato interno violado.
8. **Nada pessoal em telemetria ou auditoria.** Sem senha, token, documento, e-mail, payload de request ou SQL com valores. A auditoria mascara valores por padrão; `AuditValue()` é exceção explícita para dado não sensível.
9. **Base de migrações nova.** Não apontar o migrador para bancos legados nem remover a proteção contra históricos EF em schemas não registrados.
10. **Idioma.** Identificadores, arquivos, rotas, JSON, enums, roles, schemas, eventos e códigos de erro em inglês. Mensagens humanas, comentários, XML docs e documentação em pt-BR. O `README.md` é exceção deliberada: é a porta de entrada pública do repositório e fica em inglês. Detalhes em [`docs/language-conventions.md`](docs/language-conventions.md).
11. **Infraestrutura compartilhada é genérica.** `Shared.*` não contém regra, schema ou nome de módulo de exemplo. O projeto gerado sem exemplo precisa compilar e passar nos testes.
12. **Configuração local e produtiva são independentes.** Não publicar banco, OTLP ou porta de gerenciamento em produção; não oferecer segredo padrão produtivo.

## Onde colocar código

| O que você está escrevendo | Onde mora |
|---|---|
| Regra que precisa valer em todos os caminhos | `Module.<Name>/Domain/` — método da entidade, não setter público |
| Código de erro e mensagem de negócio | `Module.<Name>/Domain/<Name>Errors.cs` |
| Módulo novo | `dotnet new modular-module -n <Nome>`, na pasta `api/` |
| Caso de uso novo (os seis arquivos e o teste) | `dotnet new modular-usecase -n <Caso> --module <Módulo> [--command]`, na pasta `api/` |
| Orquestração de um caso de uso | `Module.<Name>/UseCases/<Name>/<Name>UseCase.cs` |
| Contrato HTTP de entrada e saída | `<Name>Request`/`<Name>Response`, no mesmo diretório do caso de uso |
| Validação de formato, tamanho e obrigatoriedade | `<Name>Validator` (FluentValidation) |
| Rota, verbo, documentação OpenAPI | `<Name>Endpoint` |
| Quem pode agir sobre o recurso | `<Name>AccessPolicy`, no mesmo diretório do caso de uso |
| Regra de acesso repetida entre policies (ex.: "é organizador do evento") | serviço pequeno em `Module.<Name>/Shared/`, sem classe base |
| Mapeamento EF de uma entidade | `Module.<Name>/Shared/Configurations/` |
| Contrato consumido por outro módulo | `Shared.Contracts.Modules/<Name>/I<Name>ModuleApi.cs` |
| Evento publicado para outros módulos | `Shared.Contracts.Modules/<Name>/` com `[EventContract("context.fact.v1")]` |
| Consumidor de evento de outro módulo | `Module.<Name>/Shared/Handlers/` |
| Capacidade genérica reutilizável por qualquer módulo | `Shared.*`, sem citar nome de módulo de negócio (ADR-010) |

## Anatomia de um caso de uso

Um diretório por caso de uso, com seis responsabilidades: `Request`, `Response`, `Validator`, `UseCase`, `Endpoint` e `AccessPolicy`. Os módulos de exemplo usam um arquivo por responsabilidade; `Module.Identity` concentra as seis em um arquivo só, por serem casos pequenos. Os testes de arquitetura exigem os sufixos de tipo e o namespace `UseCases`, não a contagem de arquivos — escolha pela legibilidade e siga o módulo em que está mexendo.

```csharp
// CreatePersonEndpoint.cs — só adapta HTTP.
internal sealed class CreatePersonEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("", async (CreatePersonRequest request,
                IUseCase<CreatePersonRequest, CreatePersonResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request, ct)).ToCreatedResult(r => $"/api/v1/people/{r.Id}"))
            .WithName("CreatePerson")
            .WithSummary("Cadastra uma pessoa")
            .WithDescription("""Markdown em pt-BR: campos, erros, evento publicado, perfil exigido.""")
            .WithValidation<CreatePersonRequest>()
            .RequireAuthorization()
            .Produces<CreatePersonResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);
}

// CreatePersonAccessPolicy.cs — quem pode executar, decidido no servidor. Registrada por varredura.
internal sealed class CreatePersonAccessPolicy(PeopleDbContext db, ICurrentUser user) : IAccessPolicy<CreatePersonRequest>
{
    public async Task<bool> CanExecuteAsync(CreatePersonRequest request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        // false vira 403 Authorization.ResourceDenied
        return (request.UserId is null || request.UserId == user.Id)
            && !await db.People.TagWith("People.CreatePerson.Access").AnyAsync(p => p.UserId == user.Id, ct);
    }
}

// CreatePersonUseCase.cs — escrita: marcada como Command.
[Command("event-management-example")]
internal sealed class CreatePersonUseCase(PeopleDbContext db, ICurrentUser user)
    : IUseCase<CreatePersonRequest, CreatePersonResponse>
{
    public async Task<Result<CreatePersonResponse>> HandleAsync(CreatePersonRequest request, CancellationToken ct)
    {
        // 1. leituras e verificações já sob o lock do comando
        // 2. regra no domínio: Person.Create(...) grava o evento de integração
        // 3. db.ExecuteInTransactionAsync(...) participa da fronteira já aberta
        // 4. falha de negócio: return PeopleErrors.EmailAlreadyRegistered;
    }
}
```

O registro é por varredura de assembly, inclusive da policy: o `Module` não registra nada por caso de uso. Um caso de uso vira, de fora para dentro: `TelemetryUseCaseDecorator` → `TransactionalUseCaseDecorator` (só com `[Command]`) → `AuthorizedUseCaseDecorator` (a `IAccessPolicy<TRequest>` do slice) → seu `UseCase`. Consultas sem `[Command]` pulam a decoração transacional e rodam no escopo da requisição.

## Armadilhas desta base

- **Um comando pode ser reexecutado do zero.** Falha transitória cria escopo e `DbContext` novos e repete tudo: autorização, leituras, regras e gravações (padrão: 3 tentativas). Um caso de uso não pode manter estado entre tentativas, escrever em cache, enviar e-mail ou chamar serviço externo com efeito. Efeito externo vira intenção gravada na Outbox.
- **Commit indeterminado existe.** Se a confirmação falhar, uma conexão nova procura o `CommandReceipt` gravado na mesma transação. Sem prova verificável, a resposta é 503 de resultado indeterminado — não declare rollback nem sucesso.
- **`ExecuteUpdateAsync` e `ExecuteDeleteAsync` contornam o change tracker.** Isso significa: sem trilha de auditoria, sem preenchimento de `UpdatedAt`/`UpdatedBy`, sem soft delete e sem publicação dos eventos acumulados na entidade. Use-os apenas em infraestrutura que precisa desse comportamento (a própria Outbox usa); em domínio, carregue a entidade e chame o método dela.
- **Soft delete é filtro global nomeado.** `IsActive`/`DeletedAt` são preenchidos pelo interceptor. Consultar registros excluídos exige `IgnoreQueryFilters` deliberado, não remoção do filtro.
- **O validator roda antes do delegate do endpoint.** Identificadores de rota são materializados no request por reflexão: a propriedade precisa ser `Guid` gravável e terminar em `Id`. Rota `{id}` preenche o primeiro identificador vazio; rotas nomeadas casam pelo nome da propriedade.
- **A chave de `[Command]` define o conjunto serializado.** Todos os escritores que compartilham uma invariante precisam declarar a mesma chave, inclusive rotinas de manutenção. Escritas com a mesma chave são serializadas: isso é custo deliberado, não alto throughput. Chave por recurso (`{EventId}`) só serve quando a invariante não depende de outro recurso; propriedade usada em placeholder precisa de `NotEmpty` no Validator, porque valor vazio é contrato violado (500). Uma chave por comando; o exemplo mantém a chave compartilhada `event-management-example` (ADR-004).
- **Outbox entrega pelo menos uma vez e não preserva ordem entre réplicas.** Handler registrado por `AddIntegrationEventHandler` já nasce idempotente: a Inbox grava `(EventId, Consumer)` na mesma transação do efeito, no `DbContext` do módulo. Isso só cobre escrita nesse `DbContext`; efeito em outro lugar precisa de idempotência no destino. Opt-out é explícito, com `[SkipInbox("justificativa")]`. Fixe `[InboxConsumer("nome")]` antes de renomear um handler.
<!--#if (sharedSource) -->
- **Shared.* é código deste projeto.** As sete bibliotecas genéricas (`Kernel`, `Contracts`, `Data`, `Http`, `Messaging`, `Observability`, `WebHost`) foram copiadas da base na geração e evoluem aqui, sem feed nem pacote; outros projetos gerados do mesmo template têm a própria cópia. Continuam genéricas: nada de regra, schema ou nome de módulo de negócio (invariante 11). Melhorias da base não chegam sozinhas: compare com a versão nova do template e traga o que fizer sentido. Referencie uma biblioteca com `<SharedReference Include="Data" />`, nunca `ProjectReference` direto.
<!--#else -->
- **Shared.* chega como pacote no projeto gerado.** As sete bibliotecas genéricas vêm do feed da base (`api/nuget.config`, versão em `SharedPackagesVersion`); `Shared.Contracts.Modules`, os módulos, o host e os testes são código do projeto. Não copie código de `Shared.*` para dentro do projeto para "corrigir": corrija na base e publique versão nova. Atualização: [`docs/upgrading.md`](docs/upgrading.md). Referencie uma biblioteca com `<SharedReference Include="Data" />`, nunca `ProjectReference`/`PackageReference` direto.
<!--#endif -->
- **Pacotes são centralizados e travados.** Dependência nova entra em `api/Directory.Packages.props`, com `PackageReference` sem versão no projeto, e exige atualizar os `packages.lock.json` (`dotnet restore` grava; o CI roda `--locked-mode`).

## Comandos

```bash
cd api && dotnet test                     # solução inteira
dotnet test api/ModularApi.slnx --nologo -clp:ErrorsOnly
dotnet restore api/ModularApi.slnx --locked-mode
node scripts/check-dependencies.mjs
node scripts/validate-infra.mjs
node scripts/validate-production.mjs --fixture
node scripts/test-template.mjs            # gera e testa os dois modos do template e os geradores
```

Geradores (na pasta `api/`; detalhes em [`docs/extending.md`](docs/extending.md)):

```bash
cd api
dotnet new modular-module -n Billing                                   # Module.Billing completo, já registrado
dotnet new modular-usecase -n CreateInvoice --module Billing --command  # escrita: [Command] + POST
dotnet new modular-usecase -n GetInvoice --module Billing               # consulta: GET por id
dotnet restore                                                          # grava os packages.lock.json; faça commit
```

Integração e testes funcionais sobem PostgreSQL real por Testcontainers; Docker precisa estar disponível. Ambiente local completo, cobertura e gates: [`docs/quality-gates.md`](docs/quality-gates.md) e [`README.md`](README.md).

## Antes de encerrar uma tarefa

- [ ] A alteração está no módulo responsável e não criou dependência entre módulos.
- [ ] Escrita de negócio entra pela fronteira transacional com a chave de consistência correta.
- [ ] A autorização considera o recurso e o dono, não só o perfil no endpoint.
- [ ] Invariante persistida tem constraint ou índice único; consulta prévia sozinha não protege concorrência.
- [ ] Consultas projetam apenas o necessário, têm ordenação estável e usam `TagWith` com constante.
- [ ] Erro novo tem código estável `Module.Reason` e o `ErrorType` correspondente ao status HTTP desejado.
- [ ] Nenhum dado pessoal, token ou payload foi para log, métrica, trace ou auditoria.
- [ ] Caso de uso não narra o fluxo em log: entrada, sucesso, rejeição e duração já vêm do `TelemetryUseCaseDecorator`. Só `LogWarning` para situação anômala que não vira erro.
- [ ] Evento novo tem `[EventContract]`, consumidor registrado por `AddIntegrationEventHandler` (Inbox) ou com `[SkipInbox]` justificado, e decisão explícita sobre `requiresConsumer`.
- [ ] Idioma respeitado: identificador em inglês, mensagem e documentação em pt-BR, `README.md` em inglês.
- [ ] Testes cobrem regra, rejeição de acesso indevido e o risco de concorrência quando existir.
- [ ] `cd api && dotnet test` passou.
<!--#if (sourceRepository) -->
- [ ] [`docs/implementation-status.md`](docs/implementation-status.md) atualizado quando houver evidência de aceite, sem marcar requisito externo como verificado localmente. Este arquivo só existe no repositório de origem do template.
<!--#endif -->

## Documentação: o que ler e quando

| Situação | Documento |
|---|---|
| Escrever ou revisar código C# | [`docs/dotnet-practices.md`](docs/dotnet-practices.md) |
| Decidir onde uma regra mora ou se um padrão se justifica | [`docs/architecture-practices.md`](docs/architecture-practices.md) |
| Entender por que a base é assim | [`docs/architecture.md`](docs/architecture.md) (ADR-001 a ADR-010 aceitos; ADR-011 a ADR-015 propostos) |
| Criar um módulo novo ou remover o exemplo | [`docs/extending.md`](docs/extending.md) |
| Autenticação, autorização, privacidade | [`docs/security.md`](docs/security.md) |
| Rodar, diagnosticar, operar, restaurar | [`docs/runbooks.md`](docs/runbooks.md) |
| Gates de teste, cobertura e release | [`docs/quality-gates.md`](docs/quality-gates.md) |
| Instalar o template em um projeto novo | [`docs/installation-guide.md`](docs/installation-guide.md) |

`docs/reference/` e `docs/architecture-review.md` são material histórico do repositório de origem, não contrato atual, e não são exportados pelo template.

## Como escrever

Prosa em pt-BR, direta e verificável. Declare limites e o que **não** está garantido. Evite adjetivo de marketing, promessa de robustez e repetição do que o código já diz.

Comentário explica decisão não óbvia, restrição externa ou consequência — não narra a linha seguinte. XML doc existe quando o consumidor precisa entender parâmetro, unidade, nulabilidade, efeito ou falha.
