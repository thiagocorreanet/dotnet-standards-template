# Boas práticas de .NET e C# nesta base

Guia de consulta para implementar, revisar e manter código neste monolito modular. Complementa [`CLAUDE.md`](../CLAUDE.md) (contrato de trabalho e invariantes) e [`architecture-practices.md`](architecture-practices.md) (onde uma regra mora e quando um padrão se justifica).

O objetivo é produzir código correto, compreensível, seguro e fácil de alterar **dentro das decisões já tomadas**. Este guia não reabre a escolha de arquitetura: ela está em [`architecture.md`](architecture.md).

Escopo: C# 13 / .NET 10, ASP.NET Core com Minimal APIs, EF Core 10 com Npgsql, FluentValidation, Serilog e OpenTelemetry. Os exemplos vêm do código real do repositório e pressupõem os tipos citados.

## Consulta rápida

| Durante o desenvolvimento | Consulte |
|---|---|
| Decidir se uma mudança vale a pena | [1. Critérios](#1-criterios) |
| Nomear, comentar, escolher idioma | [2. Nomes](#2-nomes) |
| Dividir métodos e controlar efeitos | [3. Métodos](#3-metodos), [4. Duplicação](#4-duplicacao) |
| Modelar dados e invariantes | [5. Tipos](#5-tipos), [6. Domínio](#6-dominio) |
| Escrever um caso de uso | [7. Casos de uso](#7-casos-de-uso), [8. DI](#8-di) |
| I/O, cancelamento, concorrência | [9. Async](#9-async) |
| Definir falhas e contratos HTTP | [10. Erros](#10-erros), [11. Endpoints](#11-endpoints) |
| Autorizar e proteger a fronteira | [12. Segurança](#12-seguranca) |
| Consultar e gravar dados | [13. Consultas](#13-consultas), [14. Integridade](#14-integridade) |
| Publicar eventos e chamar serviços externos | [15. Integrações](#15-integracoes) |
| Configurar, registrar e diagnosticar | [16. Configuração e logs](#16-configuracao), [17. Desempenho](#17-desempenho) |
| Testar e revisar | [18. Testes](#18-testes), [19. Refatoração](#19-refatoracao), [20. Checklist](#20-checklist) |
| Escolher entre alternativas recorrentes | [21. Matriz de decisões](#21-decisoes) |
| Mexer em solução, SDK, pacotes e build | [22. Repositório e build](#22-repositorio) |

<a id="1-criterios"></a>
## 1. Critérios para decidir

Priorize correção, segurança e integridade dos dados. Depois, clareza, simplicidade, manutenção e testabilidade. Requisitos reais de desempenho e disponibilidade fazem parte da correção.

Antes de propor uma mudança, responda:

1. Qual problema concreto existe?
2. Que comportamento ou invariante está sendo protegido?
3. A mudança reduz o esforço total para entender e alterar o código?
4. Que complexidade, dependência ou risco ela acrescenta?
5. Como verificar o resultado, preferencialmente com teste?

Contagem de linhas, parâmetros ou classes é sinal para investigar, não veredito. Um caso de uso pode coordenar vários passos; um `switch` pode expressar bem uma decisão finita; uma classe concreta pode bastar.

Esta base tem decisões registradas. Divergir delas exige ADR, não comentário em PR. Não justifique uma mudança apenas com "Clean Code", "SOLID" ou "é o padrão": explique o benefício para este código.

<a id="2-nomes"></a>
## 2. Nomes, idioma e comentários

### Idioma

Identificadores, arquivos, rotas, JSON, enums, roles, schemas, códigos de erro e propriedades estruturadas de log são em **inglês**. Mensagens de erro e validação, comentários, XML docs, descrições do OpenAPI e documentação são em **pt-BR**. A tabela completa está em [`language-conventions.md`](language-conventions.md), e `LanguageConventionTests` protege o padrão.

Consequência prática num mesmo arquivo:

```csharp
public const string CpfInvalid = "O campo {PropertyName} deve ser um CPF válido (11 dígitos, com ou sem máscara).";
```

Nome em inglês, texto exibido em português. Não misture: `MensagemErro`, `CreateEventoRequest` ou `"Invalid request"` numa resposta ao usuário são todos erro de convenção.

### Nomes devem explicar o significado

- Use o vocabulário do domínio e uma palavra por conceito.
- Especifique unidade quando houver ambiguidade: `LockTimeoutSeconds`, `MaxConcurrentDeliveries`.
- Predicados para booleanos: `IsActive`, `IsAuthenticated`, `RequiresConsumer`.
- Ações explícitas em métodos: `CreatePerson`, `MarkProcessedAsync`, `CanExecuteAsync`.
- Investigue `Manager`, `Helper`, `Data` e `Process` quando ocultarem a responsabilidade.

Sufixos obrigatórios por convenção da base, verificados em `Tests.Architecture`: `*UseCase`, `*Endpoint`, `*Validator`, `*DbContext`, `*Module`, `*AccessPolicy`, `*ModuleApi`. `*Repository` é proibido. Os testes cobrem o sufixo do tipo e o namespace `UseCases`, não quantos arquivos você usa: os módulos de exemplo separam um arquivo por responsabilidade e `Module.Identity` concentra as cinco num arquivo só.

### Estilo

`PascalCase` para tipos, métodos e propriedades; `camelCase` para parâmetros e locais; prefixo `I` em interfaces; sufixo `Async` em métodos assíncronos. Namespaces com declaração de arquivo (`file_scoped`, severidade `warning` no `.editorconfig`). `IDE0005` (using desnecessário) é warning e `EnforceCodeStyleInBuild` está ligado: o build reclama.

`var` quando o tipo é evidente ou melhora a leitura; tipo explícito quando ele comunica algo importante.

### Comentários

Documente decisão não óbvia, restrição externa e consequência. Os comentários mais valiosos desta base explicam por que uma simplificação aparente quebraria algo:

```csharp
// Aguarda a conclusão da transação anterior antes de interpretar ausência como rollback.
await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", timeout.Token);
```

Remova código comentado e comentário desatualizado; histórico é do controle de versão. Use XML doc quando o consumidor precisar entender parâmetros, unidades, nulabilidade, efeitos ou falhas — especialmente em `Shared.*` e `Shared.Contracts`, que são superfície pública entre módulos. `GenerateDocumentationFile` está ligado, com `CS1591` suprimido: documente o que importa, não tudo.

<a id="3-metodos"></a>
## 3. Métodos, parâmetros e efeitos

Um método deve ter propósito reconhecível e nível de abstração compreensível. Extraia quando existir um conceito que mereça nome, uma responsabilidade independente ou repetição real. Avalie a leitura do fluxo completo depois de dividir: uma cascata de métodos minúsculos pode exigir mais navegação que um bloco coeso.

Use guard clauses para reduzir aninhamento. Num caso de uso, o padrão é: verificar, retornar erro cedo, seguir.

```csharp
if (emailInUse)
{
    logger.LogInformation("Criação de pessoa rejeitada porque o e-mail normalizado já está cadastrado");
    return PeopleErrors.EmailAlreadyRegistered;
}
```

Parâmetros tornam dependências visíveis. Agrupe quando formarem um conceito real (`PagedRequest`); não transforme locais em campos só para encurtar assinatura — num caso de uso isso cria dependência de ordem e atrapalha a reexecução.

Um booleano que seleciona operações distintas merece revisão: prefira dois métodos nomeados. Booleano que é dado legítimo (`isActive`) pode ficar.

Torne os efeitos previsíveis. `CanExecuteAsync` decide; `NormalizeEmail` normaliza. Um método de consulta não persiste. Se a operação altera estado, isso faz parte do contrato e do nome.

<a id="4-duplicacao"></a>
## 4. Duplicação e abstrações

Centralize conhecimento que precisa permanecer consistente: uma regra de elegibilidade, um cálculo, um contrato de integração. Antes de unir trechos parecidos, confirme que têm o mesmo significado e os mesmos motivos para mudar. Semelhança visual pode ser coincidência; se a abstração começa a receber flags e exceções por chamador, reavalie a união.

Duplicação **entre módulos** merece atenção extra: unificar código de dois módulos em `Shared.*` só é correto se o conceito for genérico. Regra, schema ou nome de módulo de negócio não sobe para `Shared.*` — isso quebra o invariante de que o núcleo não conhece os exemplos.

Nesta base, os seguintes mecanismos **não** são adotados por padrão e exigem driver concreto: repositório genérico, Unit of Work adicional, MediatR ou despachante equivalente, AutoMapper, broker externo, CQRS com modelos separados, event sourcing, microsserviços. Ver a tabela de decisões condicionais em [`architecture-practices.md`](architecture-practices.md).

Quando o EF Core já atende ao caso de uso, uma camada que apenas replica `DbSet` e `SaveChanges` acrescenta pouco e é recusada por teste de arquitetura.

<a id="5-tipos"></a>
## 5. Tipos, nulabilidade e convenções de modelo

`<Nullable>enable</Nullable>` vale para toda a solução. Use `Person?` quando não encontrar é resultado válido; retorne coleção vazia quando a consulta rodou e não achou nada. Evite `!` como hábito: nulabilidade é análise estática e não valida JSON, banco nem entrada externa. Valide nas fronteiras e use guard quando o contrato interno exigir.

Convenções de modelo aplicadas automaticamente por `ModuleModelConventions` a todo `ModuleDbContext`:

| Convenção | Efeito |
|---|---|
| `string` sem configuração | `HasMaxLength(200)` |
| `decimal` | `HasPrecision(18, 2)` |
| `enum` | Convertido para texto, `HasMaxLength(50)` |
| Entidade `IAuditableEntity` raiz | Índice em `DeletedAt`, filtro global `SoftDelete`, campos de auditoria dimensionados, coleção `Events` ignorada |
| Todo contexto | Tabelas `OutboxMessages`, `CommandReceipts` e `OutboxReplayAudit` no schema do módulo |

Escolha de tipos:

- **Identificadores:** `Guid` versão 7 (`Guid.CreateVersion7()`), gerados na aplicação, `ValueGeneratedNever()`. São ordenáveis no tempo e amigáveis a índice B-tree. Não gere PK no banco.
- **Instantes:** `DateTimeOffset`, persistido como `timestamptz`. Use `DateOnly` para data civil sem horário. Offset não identifica fuso com regras; agendamento local exige também o identificador do fuso.
- **Dinheiro:** `decimal`, com moeda, escala e arredondamento definidos pela regra e testados.
- **DTOs:** `record` posicional para Request/Response e eventos. `record` e `init` não tornam imutável o objeto referenciado.
- **Value object:** introduza quando proteger uma regra ou eliminar ambiguidade real. `Cpf` existe porque normaliza e valida dígitos verificadores; não crie um tipo por primitivo sem benefício.

<a id="6-dominio"></a>
## 6. Domínio: entidades que protegem transições

Entidades principais herdam de `BaseEntity`, que fornece PK Guid v7, campos de auditoria, soft delete e acúmulo de eventos de integração. `Domain/` não conhece EF Core, ASP.NET Core nem o container — e um teste de arquitetura garante que também não conheça `UseCases/` nem `Shared/` do próprio módulo.

Padrão de entidade:

```csharp
public sealed class Person : BaseEntity
{
    private Person() { }                                   // EF materializa por aqui

    public string PersonEmail { get; private set; } = string.Empty;

    public static Person Create(string personName, string personEmail, /* ... */)
    {
        var person = new Person();
        person.Update(personName, personEmail, /* ... */, isActive: true);
        person.RecordEvent(new PersonCreated(person.Id));   // vai para a Outbox na mesma transação
        return person;
    }

    public void MarkDeleted() => RecordEvent(new PersonDeleted(Id));

    public static string NormalizeEmail(string personEmail) => personEmail.Trim().ToLowerInvariant();
}
```

Regras a seguir:

- **Setters privados.** A transição é um método com nome (`Cancel`, `MarkDeleted`, `LinkUser`), não atribuição livre de `Status`.
- **Normalização no domínio.** E-mail em minúsculas, CPF só com dígitos — a mesma normalização vale para a verificação de unicidade e para o índice.
- **Eventos com `RecordEvent`.** O `AuditSaveChangesInterceptor` recolhe `IEventEmitter.Events` no `SaveChanges`, grava `OutboxMessage` no mesmo schema e limpa a coleção. Não crie mensagem de Outbox à mão.
- **Nada de I/O.** Se a regra precisa de dados de outro agregado, o caso de uso busca e passa o valor.
- **Erros do módulo em um só lugar:** `Domain/<Name>Errors.cs`, com código estável `Module.Reason`.

Cadastro sem comportamento relevante pode ter modelo simples. Não invente invariante para justificar um método.

<a id="7-casos-de-uso"></a>
## 7. Casos de uso: contrato e limites

```csharp
public interface IUseCase<in TRequest, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
```

Registro por varredura de assembly (`AddUseCasesFromAssembly`), sempre `scoped`. A composição resulta em:

```text
TelemetryUseCaseDecorator
  └─ TransactionalUseCaseDecorator   (apenas com [Command])
       └─ AuthorizedUseCaseDecorator (IModuleAccessPolicy do módulo)
            └─ SeuUseCase
```

Consultas sem `[Command]` recebem telemetria e autorização, no escopo da requisição.

**O que um caso de uso pode fazer:** ler e gravar pelo `DbContext` do próprio módulo; chamar o domínio; chamar outro módulo por interface de `Shared.Contracts`; retornar `Result`.

**O que não pode:**

- Chamar serviço externo com efeito, enviar e-mail, escrever arquivo ou publicar fora da Outbox. Um comando é reexecutável: o efeito se repetiria.
- Guardar estado entre chamadas ou entre tentativas. Cada tentativa recebe escopo e `DbContext` novos.
- Lançar exceção para expressar regra de negócio.
- Acessar o `DbContext` de outro módulo, ou receber `HttpContext`.
- Ignorar `CancellationToken`.

Escritas marcam a fronteira transacional com a chave de consistência:

```csharp
[Command("event-management-example")]
internal sealed class CreatePersonUseCase(...) : IUseCase<CreatePersonRequest, CreatePersonResponse>
```

A decoração abre a transação, aplica `lock_timeout` e adquire `pg_advisory_xact_lock` derivado da chave **antes** de qualquer leitura. Dentro do caso de uso, use `db.ExecuteInTransactionAsync(...)`: ele participa da transação já aberta e só cria uma local quando não houver ambiente (caso de consumidor autônomo).

Grave `CommandReceipt` não é responsabilidade sua: o decorator faz isso na mesma transação para provar o commit.

<a id="8-di"></a>
## 8. Injeção de dependência e ciclos de vida

Receba dependências pelo construtor primário. Não resolva serviços por `IServiceProvider` dentro de regra de negócio; a resolução explícita pertence à composição e à criação de escopos.

| Lifetime | Uso nesta base | Cuidado |
|---|---|---|
| `Scoped` | `DbContext`, casos de uso, access policies, `ICurrentUser` | Não capturar em singleton nem deixar escapar do escopo |
| `Singleton` | `ModuleTelemetry`, processador e sonda de Outbox, opções | Precisa suportar uso concorrente e não guardar dado de requisição |
| `Transient` | Raro; serviços leves sem estado | Atenção a descartáveis e alocação |

`AddModuleDbContext<T>` registra o contexto como scoped. Hosted services são singleton: `OutboxProcessor` e `OutboxProbe` criam um escopo por unidade de trabalho (`CreateAsyncScope`) e o descartam. Faça o mesmo em qualquer `BackgroundService` novo — e respeite o token de encerramento.

O container ser seguro para resolução concorrente não torna as instâncias thread-safe. Deixe o container descartar o que ele criou.

Composição do host fica em `Shared.WebHost` (`AddModularWebHost`/`UseModularWebHost`) e em `IModule.ConfigureServices` de cada módulo. `Program.cs` permanece legível e trata os comandos `migrate`, `bootstrap-identity` e `healthcheck` antes de montar o pipeline. Não chame `BuildServiceProvider()` durante o registro.

<a id="9-async"></a>
## 9. Async, cancelamento e concorrência

Use APIs assíncronas para I/O e propague `await` pela cadeia. Evite `.Result`, `.Wait()` e `.GetAwaiter().GetResult()`. `Task`/`Task<T>` são o retorno padrão; `async void` só em handler de evento que exija a assinatura.

Receba e repasse `CancellationToken` em toda operação de banco, HTTP e espera. Na requisição, o token acompanha o cliente; em worker, o token é do ciclo de vida do host.

**Cancelamento não desfaz efeito já confirmado.** Se o cliente desconectar depois do commit, a operação aconteceu. Diferencie cancelamento do chamador, timeout de dependência e falha inesperada; não converta tudo em 500.

`DbContext` não é thread-safe e não suporta operações paralelas. Aguarde cada operação antes de reutilizá-lo. Paralelismo real exige contextos independentes e análise de consistência — dentro de um comando, isso normalmente contraria a fronteira transacional.

`Task.WhenAll` serve para operações independentes com concorrência limitada. Não dispare uma tarefa por item em coleção grande. Trabalho que precisa sobreviver à requisição vai para a Outbox, não para uma task abandonada segurando serviços scoped.

O processador de Outbox limita entregas ativas por processo (`Outbox:MaxConcurrentDeliveries`, padrão 4) e adquire a capacidade **antes** do claim, para não manter lease parada esperando semáforo. Consumidor que ignora `CancellationToken` continua executando mesmo após o timeout: o limite protege a fila, não código externo.

<a id="10-erros"></a>
## 10. Validação, `Result` e exceções

Três mecanismos, com papéis distintos:

| Situação | Mecanismo | Resposta |
|---|---|---|
| Formato, obrigatoriedade, tamanho, faixa | FluentValidation via `WithValidation<TRequest>()` | 400 com `ValidationProblemDetails` |
| Acesso ao recurso negado | `IModuleAccessPolicy` | 403 `Authorization.ResourceDenied` |
| Desfecho de negócio esperado | `Result` + `Error` | Status conforme `ErrorType` |
| Ausência normal em busca | `Error.NotFound` ou tipo anulável interno | 404 |
| Uso inválido de API interna | Guard e exceção de argumento/estado | 500 sanitizado |
| Falha de infraestrutura | Exceção; retry transitório pelo decorator | 500, ou 503 em commit indeterminado |

`Result` nunca carrega exceção de negócio:

```csharp
return PeopleErrors.EmailAlreadyRegistered;   // conversão implícita Error -> Result<T>
return Result.Success(new CreatePersonResponse(person.Id, person.PersonName, person.PersonEmail));
```

`Error` tem código estável no formato `Module.Reason`, que vira o campo `code` do ProblemDetails e é contrato com o front e com dashboards. Renomear um código é mudança incompatível.

Mapeamento de `ErrorType` para HTTP, em `ResultHttpExtensions`:

| `ErrorType` | Status |
|---|---|
| `Validation` | 400 |
| `Unauthorized` | 401 |
| `Forbidden` | 403 |
| `NotFound` | 404 |
| `Conflict` | 409 |
| `BusinessRule` | 422 |
| `Failure` | 500 |

Escolha o tipo pelo significado, não pelo status desejado: conflito de unicidade é `Conflict`; invariante violada é `BusinessRule`.

Capture exceção quando puder recuperar, traduzir ou estabelecer uma fronteira. Ao relançar, use `throw;`. Não devolva sucesso, `null` ou lista vazia para esconder falha, e não registre o mesmo erro em cada camada — o `GlobalExceptionHandler` já trata a fronteira, e `PrivacyLogSink` sanitiza o que é exportado.

<a id="11-endpoints"></a>
## 11. Endpoints e contratos HTTP

Cada caso de uso expõe exatamente um endpoint, mapeado no grupo do módulo. `MapModuleGroup` já aplica prefixo `api/v1/{route}`, tag única no OpenAPI, `RequireAuthorization()` e as respostas 401/403/500 documentadas.

No endpoint ficam apenas: rota e verbo, filtro de validação, documentação, `Produces`, e a conversão de `Result` em `IResult`. Nada de regra.

```csharp
group.MapGet("{id:guid}", async (Guid id, IUseCase<GetPersonRequest, GetPersonResponse> useCase, CancellationToken ct) =>
        (await useCase.HandleAsync(new GetPersonRequest { PersonId = id }, ct)).ToHttpResult())
```

Extensões disponíveis: `ToHttpResult()`, `ToCreatedResult(location)`, `ToNoContentResult()`.

Convenções de contrato:

- Receba apenas campos que o cliente pode alterar. Identidade e permissão vêm do token, não do corpo.
- `Request` e `Response` são `record` no diretório do caso de uso; não exponha entidade EF.
- Identificador de rota entra no request por propriedade `Guid` gravável terminada em `Id` — o filtro de validação a materializa antes de validar.
- Listagens usam `PagedRequest`/`PagedResult`, com `PageSize` limitado a 100 e ordenação estável.
- A descrição em `WithDescription` é markdown em pt-BR e documenta campos, erros possíveis com os códigos estáveis, evento publicado e perfil exigido. O Scalar é a interface publicada (apenas em `Development` com `OpenApi:Enabled=true`).

Alteração de nome de campo, tipo, nulabilidade, valor de enum ou semântica é mudança de contrato: avalie consumidores antes.

<a id="12-seguranca"></a>
## 12. Segurança nas fronteiras

Autenticar identifica; autorizar decide. Keycloak/OIDC emite o token; a API valida assinatura, issuer, audience, expiração e tipo, e resolve `(issuer, subject)` para o Guid interno. Token autenticado sem vínculo local ativo não entra.

`[Authorize]` e roles no endpoint são barreira de perfil. **Propriedade e contexto do recurso são decididos pela `IModuleAccessPolicy` do módulo**, que roda dentro da fronteira transacional nos comandos:

```csharp
internal sealed class PeopleAccessPolicy(PeopleDbContext db, ICurrentUser user) : IModuleAccessPolicy
{
    public Assembly ModuleAssembly => typeof(PeopleModule).Assembly;

    public async Task<bool> CanExecuteAsync(object request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        var id = request switch { GetPersonRequest r => r.PersonId, /* ... */ _ => Guid.Empty };
        return id != Guid.Empty && await db.People.AnyAsync(p => p.Id == id && p.UserId == user.Id, ct);
    }
}
```

Ao acrescentar um caso de uso, acrescente o caso correspondente na policy. Um `switch` que não reconhece o request retorna `Guid.Empty` e nega — falha fechada, mas o teste de IDOR é obrigatório mesmo assim.

Demais controles:

- Valide tamanho, formato e limites de toda entrada externa; paginação é limitada.
- Parametrize SQL. `ExecuteSqlInterpolated` é seguro; concatenação não.
- Não grave senha, token, connection string, documento ou dado pessoal em código, resposta, log ou auditoria.
- Segredo vem de arquivo (`/run/secrets`, `AddKeyPerFile`) ou do provedor do ambiente. User Secrets é desenvolvimento.
- A base é **single-organization**. Multi-tenancy exige `TenantId` em entidades, contratos, índices, policies, dados históricos e testes de isolamento — não basta uma claim.

Detalhes e limites em [`security.md`](security.md).

<a id="13-consultas"></a>
## 13. EF Core: consultas

Filtre e projete antes de materializar. Selecione só o necessário, limite resultados, ordene de forma determinística.

```csharp
var emailInUse = await db.People
    .TagWith("People.CreatePerson.CheckEmail")
    .AnyAsync(p => p.PersonEmail == email, cancellationToken);
```

Regras da base:

- **`TagWith` com constante**, no formato `Module.UseCase` ou `Module.UseCase.Intent`. A tag vira comentário no SQL e correlaciona a consulta ao caso de uso em `pg_stat_activity` e na telemetria. O `QueryTagInterceptor` não bloqueia consulta sem tag: registra warning e incrementa a métrica `db.queries.untagged`, tornando a regra mensurável. Nunca interpole valor na tag — ela é exportada, e valor em comentário SQL vira vazamento.
- **`AsNoTracking`** em leitura de entidade que não será alterada. Projeção para DTO já evita rastreamento de entidades.
- **Paginação** por `ToPagedResultAsync(paging, ct)` sobre consulta já projetada e ordenada.
- **Existência** com `AnyAsync`; contagem só quando o número for necessário.
- **Filtro global `SoftDelete`** remove registros com `DeletedAt` preenchido. Para incluir excluídos, use `IgnoreQueryFilters` de forma deliberada e documente o motivo — não desative o filtro no modelo.
- Investigue N+1, `Include` excessivo e consulta dentro de laço. Examine o SQL antes de propor índice.
- Não devolva `IQueryable` em contrato de módulo nem em resposta HTTP.

`ExecuteUpdateAsync` e `ExecuteDeleteAsync` executam direto no banco: **não passam pelo change tracker e, portanto, não geram auditoria, não preenchem `UpdatedAt`/`DeletedAt`, não aplicam soft delete e não publicam os eventos acumulados na entidade.** Eles são usados de propósito na infraestrutura da Outbox, onde esse comportamento é o desejado. Em domínio, carregue a entidade e chame o método dela. Se precisar deles por volume, defina explicitamente filtros, versão e verificação de linhas afetadas, e compense a auditoria.

<a id="14-integridade"></a>
## 14. Integridade, concorrência e migrações

Invariante persistida precisa de constraint no banco. "Consultar se existe e depois inserir" não impede duas requisições concorrentes; a consulta prévia serve para a mensagem de erro amigável, o índice único serve para a correção. A base usa também exclusão temporal do PostgreSQL para sobreposição de agenda no exemplo.

A fronteira transacional de um comando é o advisory lock derivado da chave de `[Command]`. Escolha a chave pelo **conjunto de invariantes** que precisa ser coordenado, não pelo nome do endpoint. Todos os escritores participantes, inclusive rotinas administrativas, precisam declarar a mesma chave. O custo é serialização das escritas daquele conjunto — deliberado, não acidental.

Limites padrão: 3 tentativas, `lock_timeout` de 10 s, comando SQL de 30 s (`CommandTransactionOptions`). A expiração não é deadline global de uma sequência arbitrária de consultas.

Reexecução e commit indeterminado:

- Falha transitória (`NpgsqlException { IsTransient: true }`, `TimeoutException`, `DbUpdateException` com causa transitória) refaz a unidade inteira em escopo novo.
- Se a confirmação falhou, uma conexão nova adquire a mesma trava e procura o `CommandReceipt`. Achou: devolve o resultado já construído. A transação anterior terminou sem a prova: pode refazer. Não deu para verificar: `CommitOutcomeUnknownException` → 503.
- O receipt resolve a tentativa interna. Ele **não** é chave de idempotência de um cliente que reenviou HTTP. Operação com efeito externo precisa de chave de negócio própria e contrato específico.

Conflito de atualização concorrente entre dois clientes exige política explícita: rejeitar, recarregar ou reconciliar, com token de concorrência quando a regra pedir detecção. Comparar com a versão que **o cliente recebeu** é diferente de recarregar a mais recente no início do update.

Migrações: geradas com `dotnet ef` a partir de `api`, com `--output-dir Shared/Migrations` no projeto do módulo e `Host.Api` como startup. Revise o SQL. A aplicação é um job explícito (`Host.Api migrate`) sob advisory lock, com credencial DDL própria; o startup normal recusa migrações pendentes. O migrador recusa históricos EF em schemas não registrados — essa proteção não é conversão de dados.

<a id="15-integracoes"></a>
## 15. Eventos e integrações externas

### Eventos entre módulos

Estado e evento são atômicos: o interceptor grava `OutboxMessage` na mesma transação do agregado. A entrega é **pelo menos uma vez**, in-process, com claim token e lease; não há ordem garantida entre réplicas.

```csharp
[EventContract("people.person-created.v1", requiresConsumer: false)]
public sealed record PersonCreated(Guid PersonId) : IntegrationEvent;
```

- O nome `context.fact.v1` é estável e independe do nome CLR. Remover a v1 antes de drenar mensagens antigas é mudança incompatível.
- `requiresConsumer: true` significa que a ausência de consumidor é falha; use quando o efeito for obrigatório.
- **Todo consumidor precisa ser idempotente** por evento e por consumidor. A auditoria usa chave do evento com `INSERT ON CONFLICT`.
- Cada mensagem recebe um escopo DI isolado; handlers da mesma mensagem compartilham esse escopo e devem ser independentes. Falha de um handler repete a mensagem inteira.
- Dead letter é terminal explícito; replay é administrativo, exige `reasonCode` e registra o ator.

### Serviços externos

Não há cliente HTTP externo nesta base, e isso é deliberado. Ao introduzir um:

- Encapsule o fornecedor em um adaptador; DTOs, autenticação e interpretação de erro ficam nesse limite.
- Use `IHttpClientFactory` com cliente tipado, `BaseAddress` e timeout configurados no registro.
- **Nunca dentro da transação do comando.** Grave a intenção na Outbox e execute o efeito no consumidor.
- Defina timeout por tentativa e orçamento total. Retry só para falha transitória e operação segura de repetir, com jitter e limite. Timeout não prova que o destino não executou.
- Para operação com efeito, projete idempotência de ponta a ponta: chave estável com escopo correto, registro atômico da chave e do efeito, tratamento de reuso com payload diferente, prazo de guarda do resultado.

<a id="16-configuracao"></a>
## 16. Configuração, logs e diagnóstico

Agrupe configuração relacionada em options tipadas e valide na inicialização com `ValidateOnStart` quando a aplicação não puder funcionar com valor inválido. Em variável de ambiente, `Outbox__BatchSize` representa `Outbox:BatchSize`. Não espalhe leitura de configuração dentro de regra.

Logs são estruturados, com template estável, **propriedades em inglês e texto em pt-BR**:

```csharp
logger.LogInformation("Pessoa {PersonId} criada", person.Id);
logger.LogWarning("Reexecutando operação {OperationId}, tentativa {Attempt}, causa {ErrorType}",
    operationId, attempt, exception.GetType().Name);
```

Proibido em log, métrica, trace e auditoria: senha, token, connection string, CPF, e-mail, request ou DTO completo, SQL com valores, stack trace livre, mensagem de exceção de terceiro. Logs com `Exception` são substituídos por evento sanitizado antes dos sinks — tipo, código, fingerprint e contexto permitido. Essa sanitização não torna seguro qualquer texto livre que você escrever.

Métricas usam rótulos de cardinalidade limitada: módulo, rota, resultado. Nunca `PersonId`, e-mail ou URL livre. Métrica HTTP é agregada por rota.

Amostragem de traces: 100% local, parent-based 10% em produção. A Outbox propaga `traceparent`; mensagem tardia pode cair fora da janela consultada.

<a id="17-desempenho"></a>
## 17. Desempenho e cache

Comece pelo que é mensurável: volume de dados, número de consultas, chamadas externas, contenção de lock, limite de concorrência. Registre o cenário e a métrica que quer melhorar.

Para otimização não óbvia: reproduza, meça, identifique o gargalo, altere, meça de novo e confirme que o comportamento continua correto.

Não introduza `Span<T>`, pooling, `ValueTask`, serialização própria ou código inseguro por expectativa. O gargalo típico desta base é consulta, quantidade de viagens ao banco ou disputa pela chave de consistência — investigue essas três antes.

Não há cache na base. Antes de adicionar: defina proprietário, chave, escopo de autorização, validade, invalidação e tolerância a dado antigo. Cache que muda decisão de negócio é questão de correção, não de desempenho. Chave compartilhada para conteúdo que depende do usuário é falha de segurança.

<a id="18-testes"></a>
## 18. Testes

Quatro suítes, com papéis distintos:

| Projeto | O que verifica | Infraestrutura |
|---|---|---|
| `Tests.Unit` | Regras de domínio, validators, `Result`, sanitização de log, agendamento da Outbox | Nenhuma |
| `Tests.Integration` | Contrato HTTP, OIDC, policies e IDOR, transação e concorrência, Outbox, privilégios de banco, migrações, convenções de idioma | PostgreSQL real via Testcontainers, `ApiFactory` |
| `Tests.Functional` | Jornadas de negócio descritas em Gherkin pt-BR | PostgreSQL real, Reqnroll |
| `Tests.Architecture` | Fronteiras entre módulos, independência do `Domain`, convenções de nome e namespace, ausência de repositório | Reflexão sobre os assemblies descobertos |

Escolha a camada pelo risco:

| O que precisa de confiança | Onde testar |
|---|---|
| Cálculo, transição, normalização | Unidade |
| Tradução LINQ, constraint, transação, lock, concorrência | Integração com PostgreSQL real |
| Rota, binding, validação, autorização, ProblemDetails | Integração HTTP com `ApiFactory` |
| Entrega, repetição, dead letter, idempotência do consumidor | Integração, com duas instâncias quando o risco for de réplica |
| Fronteira arquitetural | Arquitetura |

Regras:

- **Não use EF InMemory nem SQLite** como substituto do PostgreSQL. Eles não provam SQL, constraint, filtro parcial nem semântica transacional.
- Priorize cenários capazes de causar perda, acesso indevido ou duplicidade: dois clientes editando a mesma versão, mesma mensagem entregue duas vezes, resposta perdida após o commit, usuário de outro dono usando um identificador válido.
- Mantenha testes determinísticos. Controle relógio e aleatoriedade; não use espera fixa quando houver condição verificável.
- Teste comportamento observável, não a ordem de chamadas internas. Não crie teste de getter para subir cobertura.
- Dois `WebApplicationFactory` compartilhando PostgreSQL real exercitam concorrência entre processos lógicos, mas não simulam duas máquinas, partição de rede ou failover.

Cobertura tem pisos em `api/coverage-policy.json` (70% de linhas, 60% de branches no total, mais gates específicos). Cobertura é evidência de execução, não de correção. Comandos e limites em [`quality-gates.md`](quality-gates.md).

<a id="19-refatoracao"></a>
## 19. Refatoração e automação

Refatore em passos pequenos, preservando comportamento externo. Identifique contratos afetados e confirme os testes antes e depois. Separe mudança estrutural de mudança de regra quando viável: facilita revisão e investigação de regressão.

Execute o que o repositório define: `dotnet test`, `check-dependencies`, `validate-infra`, `validate-production --fixture` e, ao mexer no núcleo ou na geração, `test-template.mjs` — que prova que o projeto **sem exemplo** continua compilando e passando.

Analyzers em `latest-recommended` com `EnforceCodeStyleInBuild`, e o build da solução fica **sem avisos**. Cada grupo de `NoWarn` em `Directory.Build.props` tem o motivo escrito ao lado; confira o que é realmente necessário com `dotnet build ModularApi.slnx -t:Rebuild -p:NoWarn=` antes de acrescentar um código. Aviso pontual se corrige ou se suprime localmente com `#pragma` e justificativa, nunca com supressão ampla. Regras de estilo não se aplicam a migrações geradas: isso está escopado em `.editorconfig`. `NU1901`–`NU1904` são erro: vulnerabilidade em pacote quebra o build.

Uma revisão deve apresentar evidência, consequência e mudança sugerida, e diferenciar defeito, risco de manutenção e preferência de estilo.

<a id="20-checklist"></a>
## 20. Checklist antes de concluir

Marque como não aplicável o que estiver fora do escopo.

- [ ] Comportamento esperado e casos de falha estão claros.
- [ ] Nomes, idioma, unidades e efeitos estão corretos nas duas convenções (inglês técnico, pt-BR humano).
- [ ] A mudança está no módulo responsável; nenhuma dependência nova entre módulos.
- [ ] `Domain/` continua independente de `UseCases/` e `Shared/`.
- [ ] Cada abstração nova resolve um problema concreto e não reintroduz padrão recusado pela base.
- [ ] Escrita de negócio está marcada com `[Command]` e com a chave de consistência correta.
- [ ] O caso de uso não tem efeito externo, estado retido nem exceção para regra de negócio.
- [ ] A access policy cobre o novo request; há teste de acesso indevido.
- [ ] Invariante persistida tem constraint ou índice único.
- [ ] Consultas projetam o necessário, têm ordenação estável, `TagWith` constante e tracking adequado.
- [ ] Nenhum `ExecuteUpdate`/`ExecuteDelete` em domínio sem compensar auditoria e eventos.
- [ ] Evento novo tem contrato estável, consumidor idempotente e decisão sobre `requiresConsumer`.
- [ ] Async e cancelamento corretos; nenhum uso paralelo do mesmo `DbContext`.
- [ ] Logs ajudam a investigar sem expor dado pessoal, token ou payload.
- [ ] Pacote novo entrou em `Directory.Packages.props` e os lockfiles foram atualizados.
- [ ] Migração revisada, com SQL inspecionado e plano de aplicação.
- [ ] Testes relevantes nas quatro suítes; `cd api && dotnet test` passou.
- [ ] Documentação e OpenAPI atualizados junto com o código.

<a id="21-decisoes"></a>
## 21. Matriz de decisões frequentes

| Dúvida | Nesta base | Quando reconsiderar |
|---|---|---|
| Extrair outro método? | Extraia um conceito que torne a leitura mais clara. | Se só deslocar linhas e aumentar navegação. |
| Criar interface? | Só para fronteira real: contrato entre módulos, ponto de extensão do host. | Classe concreta interna estável não precisa. |
| Criar repositório? | Não. `DbContext` direto; teste de arquitetura recusa o nome. | Nunca, sem ADR. |
| Exceção ou `Result`? | `Result` para desfecho esperado; exceção para falha inesperada. | Preserve a semântica; não converta exceção em 400. |
| Novo `DbContext` no módulo? | Não. Um por módulo; a composição depende disso. | Necessidade real exige revisar `AddUseCasesFromAssembly`. |
| Chamar outro módulo? | Interface de `Shared.Contracts` (síncrono) ou evento (assíncrono). | Nunca referência direta nem HTTP local. |
| Nova chave de `[Command]`? | Só se o conjunto de invariantes for realmente independente do existente. | Chave nova mal escolhida quebra coordenação silenciosamente. |
| `AsNoTracking`? | Em leitura de entidade não alterada. | Projeção para DTO já dispensa. |
| `ExecuteUpdate`/`ExecuteDelete`? | Infraestrutura, com filtros e verificação explícitos. | Em domínio, perde auditoria, soft delete e eventos. |
| `IgnoreQueryFilters`? | Uso deliberado e documentado. | Não remova o filtro global do modelo. |
| Executar em paralelo? | Só tarefas independentes, com limite; nunca no mesmo `DbContext`. | Dentro de comando, costuma contrariar a transação. |
| Fazer retry? | O decorator já refaz o comando em falha transitória. | Não empilhe outro retry sobre ele. |
| Chamar serviço externo? | Pela Outbox, fora da transação, com idempotência no destino. | Nunca dentro do caso de uso transacional. |
| Adicionar cache? | Exija benefício medido e regra de invalidação e isolamento. | Consulta mal feita não se resolve com cache. |
| Adicionar pacote? | Avalie manutenção, licença, compatibilidade e custo de remoção. | Prefira recurso nativo; atualize os lockfiles. |
| Subir código para `Shared.*`? | Só se for genérico e sem conhecimento de módulo de negócio. | Regra de domínio compartilhada indica contrato, não utilitário. |

<a id="22-repositorio"></a>
## 22. Solução, SDK, pacotes e build

| Arquivo | Responsabilidade |
|---|---|
| `api/global.json` | SDK 10.0.401 com `rollForward: latestPatch`. Alinha máquina local e CI. |
| `api/ModularApi.slnx` | Solução no formato XML. Projeto novo entra aqui e no `Host.Api` quando for módulo. |
| `api/Directory.Build.props` | `net10.0`, nullable, `ImplicitUsings`, analyzers, lockfile, auditoria NuGet, `IncludeExample`. |
| `api/Directory.Packages.props` | Central Package Management: **toda** versão de pacote vive aqui. |
| `api/*/packages.lock.json` | Restore reproduzível; o CI roda `--locked-mode`. |
| `api/.editorconfig` | Estilo mínimo obrigatório (UTF-8, LF, namespace file-scoped, ordem de usings). |
| `api/coverage-policy.json` e `coverage.runsettings` | Pisos de cobertura e coleta via Coverlet/VSTest. |
| `.template.config/template.json` | O que o `dotnet new` inclui, renomeia e exclui no modo sem exemplo. |

Ao adicionar um pacote: declare `PackageVersion` em `Directory.Packages.props`, use `PackageReference` **sem** `Version` no projeto, rode `dotnet restore` e versione o lockfile alterado. Centralizar versão não adiciona o pacote a todos os projetos.

Ao adicionar um módulo: crie o projeto em `api/src/modules/`, referencie apenas os `Shared.*` necessários, adicione a referência em `Host.Api` (a descoberta procura assemblies `Module.*`) e inclua-o na solução. Faça build limpo ao remover um módulo, para não conservar DLL antiga. Passo a passo em [`extending.md`](extending.md).

Coverlet usa VSTest; não troque para Microsoft.Testing.Platform sem adaptar o coletor. `LangVersion` está em `latest` por decisão do repositório, alinhado ao SDK fixado em `global.json` — não é convite para depender de preview.

## Manutenção deste guia

Atualize quando uma experiência concreta justificar orientação melhor: registre o problema observado, a decisão e o efeito. Mudança de invariante entra também em [`CLAUDE.md`](../CLAUDE.md) e [`AGENTS.md`](../AGENTS.md); mudança de decisão arquitetural vira ADR em [`architecture.md`](architecture.md).

Base conceitual: *Código Limpo*, de Robert C. Martin, adaptado a C#/.NET sem transformar seus princípios em limites numéricos, e a documentação oficial da plataforma. Selecione na documentação da Microsoft a versão correspondente ao SDK deste repositório, porque APIs e defaults mudam entre versões.
