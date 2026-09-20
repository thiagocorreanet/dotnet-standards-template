# Arquitetura aplicada: decidir dentro do monolito modular

Guia de consulta para decidir **onde** uma responsabilidade mora, **quando** um padrão novo se justifica e **como** evoluir esta base sem quebrar o que ela protege.

Complementa três documentos, sem repeti-los:

- [`CLAUDE.md`](../CLAUDE.md) — contrato de trabalho e invariantes.
- [`architecture.md`](architecture.md) — as decisões já tomadas (ADR-001 a ADR-007) e suas consequências.
- [`dotnet-practices.md`](dotnet-practices.md) — como escrever o código do dia a dia.

Aqui está o raciocínio que liga os três: como aplicar princípios de arquitetura a um monolito modular real, com fronteira transacional explícita, Outbox e autorização por recurso.

## Consulta rápida

| Preciso decidir ou revisar… | Seção |
|---|---|
| Se a mudança é essencial, preferência ou condicional | [1. Níveis de decisão](#1-niveis) |
| Implementar uma funcionalidade do início ao fim | [2. Roteiro](#2-roteiro) |
| Por que a estrutura é esta e quando ela deixa de servir | [3. Proporção](#3-proporcao) |
| Onde colocar código e quais referências são permitidas | [4. Estrutura e dependências](#4-estrutura) |
| Aplicar SOLID sem inflar o desenho | [5. SOLID](#5-solid) |
| Em qual mecanismo uma regra deve morar | [6. Regras](#6-regras) |
| Escolher chave de consistência e entender seu custo | [7. Consistência](#7-consistencia) |
| Fazer dois módulos conversarem | [8. Entre módulos](#8-entre-modulos) |
| Publicar e consumir eventos com segurança | [9. Eventos](#9-eventos) |
| Definir contrato HTTP estável | [10. Contratos](#10-contratos) |
| Modelar dados e evoluir schema | [11. Persistência](#11-persistencia) |
| Tratar autorização, privacidade e observabilidade como arquitetura | [12. Segurança](#12-seguranca), [13. Observabilidade](#13-observabilidade) |
| Escolher a camada de teste pelo risco | [14. Testes](#14-testes) |
| Avaliar um padrão ou biblioteca nova | [15. Decisões condicionais](#15-condicionais) |
| Extrair um módulo ou registrar uma decisão | [16. Evolução](#16-evolucao) |
| Fechar uma PR | [17. Checklist](#17-checklist) |

<a id="1-niveis"></a>
## 1. Três níveis de decisão

- **Essencial** — protege correção, segurança ou integridade. Não é negociável em revisão. Exemplo: autorizar o acesso ao recurso, não só ao endpoint; gravar estado e evento na mesma transação.
- **Preferência** — bom ponto de partida desta base, ajustável com justificativa. Exemplo: organizar cada caso de uso em `UseCases/<Name>/` com as cinco responsabilidades separadas; usar `Result` em vez de exceção.
- **Condicional** — acrescenta custo e exige necessidade identificada. Exemplo: cache distribuído, broker externo, multi-tenancy, extração de um módulo.

Essencial é a **garantia**, não a ferramenta. "Detectar edição concorrente" é essencial quando a regra exige; o token de concorrência é uma das implementações. "Coordenar invariantes entre módulos" é essencial no exemplo; o advisory lock do PostgreSQL é a implementação escolhida em ADR-004.

Preferência não vira bloqueio de revisão sem problema concreto. Condicional não entra sem ADR.

Antes de acrescentar qualquer abstração, explicite: o comportamento que ela protege, a mudança que deverá acomodar e o custo de mantê-la. Uma interface com uma única implementação vale a pena quando delimita uma fronteira real — `IPeopleModuleApi` é exatamente isso. Uma classe interna estável continua concreta.

<a id="2-roteiro"></a>
## 2. Roteiro para implementar uma funcionalidade

1. **Descreva a operação.** Quem executa, o que entra, o que sai, quais regras valem, quais falhas são esperadas.
2. **Localize o módulo dono.** Quem possui o dado e a regra? Se a resposta for "dois módulos", volte à seção [8](#8-entre-modulos) antes de escrever código.
3. **Defina a fronteira.** É leitura ou escrita? Escrita entra com `[Command("chave")]`; escolha a chave pelo conjunto de invariantes, não pelo endpoint.
4. **Defina autorização.** Perfil basta ou existe propriedade do recurso? Acrescente o caso na `IModuleAccessPolicy` do módulo antes de implementar o fluxo feliz.
5. **Escreva o domínio primeiro.** Entidade com método nomeado, normalização, erro estável em `<Name>Errors.cs`, evento com `RecordEvent` quando houver fato a publicar.
6. **Escreva o caso de uso.** Verificações, chamada ao domínio, `ExecuteInTransactionAsync`, retorno `Result`.
7. **Escreva request, validator e endpoint.** Só formato no validator; só transporte e documentação no endpoint.
8. **Proteja a invariante no banco.** Índice único, constraint ou exclusão, com migração revisada.
9. **Teste pelo risco.** Domínio em unidade; acesso indevido, concorrência e contrato HTTP em integração com PostgreSQL real.
10. **Feche.** Documentação do endpoint, evento registrado, `dotnet test`, checklist da seção [17](#17-checklist).

Fluxo típico de uma escrita, já implementado pela base:

```text
HTTP → autenticação OIDC → limite por usuário → policy de perfil do endpoint
     → validação (FluentValidation) → decoração transacional (BEGIN + advisory lock)
     → policy de recurso do módulo → caso de uso → domínio
     → estado + Outbox + CommandReceipt → COMMIT
```

Leituras passam por autenticação, policy do módulo e telemetria, sem transação nem lock. Não crie etapas artificiais numa consulta simples.

<a id="3-proporcao"></a>
## 3. Por que esta estrutura e quando ela deixa de servir

A escolha está registrada em ADR-001: monolito modular com vertical slices, em vez de `Domain`/`Application`/`Infrastructure` repetidos por convenção. O critério é o mesmo que se aplicaria a qualquer sistema novo:

| Contexto | Estrutura proporcional | Sinal de que precisa evoluir |
|---|---|---|
| CRUD pequeno ou integração simples | Um projeto com pastas por funcionalidade | Regra relevante se mistura com transporte e persistência |
| Muitos casos de uso com mudanças localizadas | Vertical slices | Regras comuns pedem modelo próprio |
| Regras de negócio complexas | Domínio separado das camadas externas | Novos módulos têm donos e ritmos diferentes |
| **Várias capacidades no mesmo produto** | **Monolito modular com contrato interno e propriedade dos dados — esta base** | **Um módulo precisa de escala ou implantação independente** |
| Processamento demorado ou recuperável | Trabalho em segundo plano com armazenamento durável — a Outbox | API e processamento exigem recursos diferentes |
| Autonomia operacional comprovada | Serviços separados | Custo de coordenação supera o benefício |

Separação lógica e distribuição física são decisões distintas. Vários projetos continuam sendo um processo e uma unidade de implantação. O que esta base compra com isso: transação local real, chamada entre módulos sem rede, um deploy, uma rota de telemetria. O que ela cobra: deploy e recursos compartilhados, escritas do mesmo conjunto de invariantes serializadas, disciplina de ownership que nenhuma pasta impõe sozinha.

Microsserviço exige justificativa que compense rede, falha parcial, contratos, observabilidade distribuída e coordenação de dados. Quantidade de endpoints ou tamanho de classe não justifica.

<a id="4-estrutura"></a>
## 4. Estrutura e direção das dependências

```text
Host.Api ──────────────► Module.*            (descoberta de assemblies Module.*.dll)
Host.Api ──────────────► Shared.WebHost, Shared.Data, Shared.Messaging, Shared.Observability
Module.<Name> ─────────► Shared.Contracts, Shared.Data, Shared.Http, Shared.Observability
Module.<Name> ──╳─────► Module.<Other>       (proibido; teste de arquitetura falha)
Module.<Name>/Domain ──╳─────► UseCases/, Shared/  (proibido; teste de arquitetura falha)
Shared.* ──────╳─────► Module.*              (infraestrutura não conhece negócio)
```

Dentro de um módulo, as camadas existem como **pastas com regra de dependência**, não como assemblies:

| Responsabilidade | Local | Pode conhecer |
|---|---|---|
| Regra, estado e transição de negócio | `Domain/` | Nada além de `Shared.Contracts` e BCL |
| Coordenação do caso de uso | `UseCases/<Name>/*UseCase.cs` | `Domain/`, o `DbContext` do módulo, contratos de outros módulos |
| Transporte HTTP e documentação | `UseCases/<Name>/*Endpoint.cs` | O contrato `IUseCase`, nada de regra |
| Formato de entrada | `UseCases/<Name>/*Validator.cs` | O request e helpers do módulo |
| Infraestrutura do módulo | `Shared/` | EF Core, DI, o domínio, handlers |
| Contrato entre módulos | `Shared.Contracts/` | Somente DTOs e interfaces |

`internal` limita acesso por assembly. Como cada módulo é um assembly, `internal` é uma fronteira real aqui: casos de uso, endpoints, validators e policies são `internal`; só o `IModule`, o `DbContext` e a implementação do contrato público precisam ser `public`.

Pasta e namespace localizam código; não impedem acoplamento. Por isso as fronteiras que importam têm teste em `Tests.Architecture`: dependência entre módulos, independência do `Domain`, ausência de repositório, convenções de nome e namespace de casos de uso. Ao criar um módulo, ele entra automaticamente nesses testes pela descoberta de assemblies — não há lista para manter.

Contratos vivem junto de quem precisa deles: `IPeopleModuleApi` está em `Shared.Contracts` porque os consumidores são outros módulos; a implementação `PeopleModuleApi` está no módulo dono. Essa é a regra de dependência invertida na prática, sem criar um projeto por camada.

<a id="5-solid"></a>
## 5. SOLID aplicado com critério

| Princípio | Aplicação útil nesta base | Exagero a evitar |
|---|---|---|
| SRP | Separar transporte (endpoint), coordenação (caso de uso), regra (domínio) e adaptação (módulo) | Criar uma classe por linha, ou confundir responsabilidade com número de métodos |
| OCP | Ponto de extensão identificado: `IModule`, `IModuleAccessPolicy`, `IIntegrationEventHandler` | Sistema de plug-ins para variação imaginada |
| LSP | Todo `IModuleAccessPolicy` decide de verdade; todo `IUseCase` retorna `Result`, não lança para regra | Implementar contrato com operação essencial jogando `NotSupportedException` |
| ISP | Contrato de módulo expõe o mínimo: `IPeopleModuleApi` devolve resumo com id, nome e e-mail de pessoas ativas | Interface que publica `DbSet` ou `IQueryable` e promete independência que não entrega |
| DIP | Módulo depende de `Shared.Contracts`; a implementação é registrada pelo dono | `IClasse` para toda classe, inclusive tipo interno estável |

Usar um container não garante inversão de dependência: se o contrato expõe tipos do fornecedor ou mora na infraestrutura, o acoplamento continua. O teste é simples — o consumidor consegue descrever o que precisa sem citar EF Core, HTTP ou o schema do outro módulo?

Prefira composição quando a herança serviria só para reaproveitar código. `BaseEntity` é herança legítima: define identidade, auditoria, soft delete e emissão de eventos, e todo herdeiro honra esse contrato.

<a id="6-regras"></a>
## 6. Onde uma regra mora

Coloque a regra no lugar que consegue garanti-la em **todos** os caminhos de execução.

| Tipo de regra | Exemplo | Mecanismo desta base |
|---|---|---|
| Formato da entrada | Campo obrigatório, tamanho, e-mail válido, CPF com dígitos corretos | `*Validator` (FluentValidation), filtro do endpoint |
| Permissão de perfil | Só administrador altera roles | `RequireAuthorization` / role no endpoint |
| Permissão sobre o recurso | Só o dono edita a própria pessoa | `IModuleAccessPolicy`, dentro da transação nos comandos |
| Invariante do agregado | Capacidade não pode ficar abaixo de confirmados | Método do domínio |
| Coordenação | Carregar, aplicar, publicar evento, persistir | Caso de uso |
| Invariante persistida | Unicidade de e-mail, não sobreposição de sala | Índice único, constraint, exclusão temporal |
| Invariante entre módulos | Palestra não pode referenciar local inexistente | Chave de `[Command]` compartilhada + contrato do módulo dono |

Validação antecipada melhora a resposta ao usuário, mas não substitui a proteção no ponto de gravação. Quem depende de dado concorrente precisa de proteção na persistência: consulta prévia serve para a mensagem, constraint serve para a correção.

Nomeie o caso de uso pela intenção: `RegisterParticipant`, `IssueCertificate`, `ChangeEventStatus`. Não crie `PersonManager` nem serviço genérico que cresce sem limite — a estrutura de pastas já impede isso, desde que você não concentre operações não relacionadas num mesmo caso de uso.

<a id="7-consistencia"></a>
## 7. Fronteira transacional e chave de consistência

Esta é a decisão arquitetural mais específica da base (ADR-003 e ADR-004) e a que mais erra quem chega agora.

**O que acontece numa escrita.** O `TransactionalUseCaseDecorator` abre a transação, define `lock_timeout` e adquire `pg_advisory_xact_lock` derivado do SHA-256 da chave declarada em `[Command]`. Só então a autorização de recurso, as leituras, as regras e as gravações acontecem. O `CommandReceipt` é gravado na mesma transação, como prova de commit.

**Por que antes.** Ler e validar fora do lock permite que outra requisição altere o estado entre a decisão e a gravação. A fronteira existe para que a decisão e o efeito sejam atômicos entre `DbContext`s e réplicas, sem transação distribuída.

**Como escolher a chave.**

1. Liste as invariantes que a operação precisa preservar.
2. Liste todos os escritores que podem violá-las — incluindo outros módulos e rotinas administrativas.
3. Se esse conjunto já é coordenado por uma chave existente, use a mesma chave.
4. Só crie chave nova quando o conjunto for comprovadamente independente do existente.

O exemplo usa uma única chave (`event-management-example`) para todos os comandos, porque as invariantes atravessam quatro módulos. Isso serializa as escritas do exemplo: custo deliberado, documentado, e não uma promessa de throughput. Uma evolução medida pode adotar locks por agregado, com ordem estável e a matriz completa de participantes — e isso é um ADR, não um ajuste local.

**Chave nova mal escolhida falha em silêncio.** Duas chaves diferentes para escritores da mesma invariante não produzem erro: produzem corrupção sob concorrência. Toda mudança de chave precisa de teste concorrente.

**Consequências para o caso de uso.** Como a unidade inteira é reexecutada em falha transitória, com escopo e `DbContext` novos, o caso de uso precisa ser puro em relação ao mundo externo: sem efeito colateral fora do banco, sem estado retido, sem chamada externa. Efeito externo vira registro na Outbox.

**Commit indeterminado é um estado previsto.** Se a confirmação falha e a prova não pode ser verificada, a resposta é 503 e a operação não é declarada nem confirmada nem desfeita. Clientes que precisam reenviar com segurança precisam de chave de idempotência de negócio — o `CommandReceipt` resolve a tentativa interna, não o reenvio HTTP.

<a id="8-entre-modulos"></a>
## 8. Comunicação e propriedade entre módulos

Cada módulo possui seu schema, suas tabelas e suas regras. Não há FK entre schemas, por decisão: a integridade referencial cruzada seria uma dependência física que a fronteira lógica não quer. Em troca, a coordenação é responsabilidade explícita do comando.

Escolha o canal pela necessidade:

| Necessidade | Canal | Consequência |
|---|---|---|
| Preciso de um dado de outro módulo **agora**, para decidir | Interface de `Shared.Contracts` (`I<Name>ModuleApi`) | Chamada síncrona no mesmo processo e na mesma transação lógica; o dono controla o que expõe |
| Outro módulo precisa **reagir** a um fato | Evento de integração via Outbox | Assíncrono, pelo menos uma vez, sem ordem garantida |
| Preciso alterar dado de outro módulo | Operação exposta pelo módulo dono | Nunca escreva na tabela alheia |
| Preciso de leitura ampla para relatório | Contrato de leitura deliberado | Documente como contrato, não como atalho |

Regras que decorrem disso:

- Contrato transporta DTO. Nunca entidade EF, nunca `IQueryable`, nunca tipo de infraestrutura.
- O módulo dono decide o que expõe. `IPeopleModuleApi` devolve o mínimo (id, nome, e-mail, apenas pessoas ativas) porque mais do que isso vazaria decisão de privacidade do dono.
- HTTP local entre módulos do mesmo processo é erro: acrescenta serialização, perde a transação e finge uma fronteira que não existe.
- Se uma regra exige transação atravessando módulos, isso é um acoplamento real: registre-o com a chave de consistência compartilhada, em vez de fingir independência.

Quando a resposta para "de quem é essa regra?" for "dos dois", a saída correta quase nunca é duplicar: é decidir o dono e expor uma operação.

<a id="9-eventos"></a>
## 9. Eventos, Outbox e consumidores

O contrato de entrega está em ADR-005. O que decidir ao criar um evento:

**O fato merece um evento?** Evento representa algo que aconteceu e é interessante para outro módulo. Se ninguém reage e o dado é reconstruível, documente a decisão de não publicar em vez de criar infraestrutura sem consumidor.

**Nome estável.** `context.fact.v1`, independente do nome CLR, declarado em `[EventContract]`. O nome é contrato de armazenamento: há mensagens persistidas com ele. Renomear ou remover a v1 antes de drenar as mensagens antigas é mudança incompatível. Evolução compatível acrescenta v2 e mantém v1 até a drenagem.

**`requiresConsumer`.** `true` quando a ausência de consumidor é falha de configuração — o efeito é obrigatório. `false` para evento observacional. Escolha explicitamente; o padrão é `true`.

**Idempotência é do consumidor.** A entrega é pelo menos uma vez e a ordem não é garantida entre réplicas. O consumidor precisa de deduplicação própria por evento e consumidor, ou de efeito naturalmente idempotente — a auditoria usa a chave do evento com `INSERT ON CONFLICT`. Deduplicação de broker, quando houver um no futuro, não substitui isso.

**Escopo e falha.** Cada mensagem recebe um escopo DI isolado; handlers da mesma mensagem compartilham o escopo e devem ser independentes entre si. Falha de um handler repete a mensagem inteira — projete para isso. Esgotadas as tentativas, a mensagem vai para dead letter, que é terminal; replay é ação administrativa com `reasonCode` e registro do ator, nunca automática.

**Evento de domínio em memória não é mensagem durável.** Nesta base, tudo que é publicado passa pela Outbox, na mesma transação do estado. Não invente um despachante em memória paralelo.

<a id="10-contratos"></a>
## 10. Contratos de API

Um caso de uso, um endpoint, dentro do grupo do módulo (`api/v1/<route>`), com uma única tag no OpenAPI. O contrato público é composto de: rota, verbo, corpo de entrada, corpo de saída, status possíveis e **códigos de erro**.

| Situação | Status | Origem nesta base |
|---|---|---|
| Entrada inválida | 400 | Filtro de validação (`ValidationProblemDetails`) |
| Credencial ausente ou inválida | 401 | Pipeline OIDC ou `ErrorType.Unauthorized` |
| Autenticado sem permissão | 403 | `IModuleAccessPolicy` ou `ErrorType.Forbidden` |
| Recurso inexistente | 404 | `ErrorType.NotFound` |
| Conflito de estado ou unicidade | 409 | `ErrorType.Conflict` |
| Invariante de negócio violada | 422 | `ErrorType.BusinessRule` |
| Criado | 201 | `ToCreatedResult` com location |
| Sem conteúdo | 204 | `ToNoContentResult` |
| Falha inesperada | 500 | `GlobalExceptionHandler`, sanitizado |
| Resultado indeterminado | 503 | `CommitOutcomeUnknownException` |

O campo `code` do ProblemDetails é a parte estável para o cliente: `People.EmailAlreadyRegistered` não muda porque a mensagem em português mudou. Trate renomeação de código como quebra de contrato.

Mudança compatível: acrescentar campo opcional na resposta, acrescentar endpoint, acrescentar código de erro novo em situação nova. Mudança incompatível: renomear campo, mudar tipo ou nulabilidade, mudar significado de valor de enum, remover código de erro, mudar status de uma situação existente. Incompatibilidade exige versão nova do grupo, não ajuste silencioso.

<a id="11-persistencia"></a>
## 11. Persistência e evolução de schema

Um schema por módulo, um `DbContext` por módulo, migrações dentro do módulo. EF Core é usado diretamente (ADR-001): o `DbContext` já é a unidade de trabalho, e uma camada que apenas o renomeia foi recusada.

Decisões que continuam suas:

| Situação | Orientação |
|---|---|
| Consulta simples dentro do caso de uso | `DbContext` direto, projetando para DTO |
| Leitura com formato próprio | Projete; não carregue o agregado inteiro por obrigação |
| Regra rica sobre o agregado | Carregue a entidade e use seus métodos; a persistência continua no caso de uso |
| Gargalo confirmado | Inspecione SQL e plano; SQL explícito é aceitável com justificativa |
| Escrita em volume | `ExecuteUpdate`/`ExecuteDelete` só com auditoria e eventos compensados explicitamente |

Evolução de schema é mudança de código **e** de dados: revise o SQL gerado, avalie perda, lock e duração. A aplicação é um job explícito com credencial DDL separada da credencial de runtime; o startup normal recusa migrações pendentes. Quando versões antiga e nova precisarem coexistir, introduza a mudança compatível, migre consumidores e dados, e só depois remova a estrutura antiga — renomear ou remover coluna derruba instâncias ainda rodando a versão anterior.

Rollback de binário não é rollback de dados. Recuperação exige backup testado, não apenas pipeline verde.

<a id="12-seguranca"></a>
## 12. Segurança como decisão de arquitetura

Keycloak/OIDC é o emissor; a API não guarda senha nem chave de emissão (ADR-002). A identidade interna é `(issuer, subject)` mapeado para um Guid estável — e-mail não é chave de vínculo, porque muda e pode ser reaproveitado.

O que isso implica no desenho:

- **Autorização acontece em dois lugares, com papéis diferentes.** Perfil no endpoint é barreira grossa; propriedade do recurso é decisão do módulo, dentro da fronteira transacional. Um identificador válido na URL não prova permissão.
- **A API decide, o IdP não.** Roles vêm do client da API e de uma allowlist; claims internas recebidas são removidas. Remoção de role apenas no IdP leva até 300 s mais o skew para valer; mudança urgente exige também corte local.
- **Falha fechada.** Execução sem policy registrada é rejeitada; policy que não reconhece o request nega.
- **Privacidade é restrição de arquitetura, não filtro no final.** Auditoria mascara valores por padrão; telemetria não exporta payload, SQL com valores nem stack livre. `AuditValue()` é exceção explícita para dado não sensível e de cardinalidade controlada.
- **Single-organization por decisão.** Multi-tenancy toca entidades, contratos, índices, policies, dados históricos e testes de isolamento. É um projeto, não uma claim.

Detalhamento e limites conhecidos em [`security.md`](security.md).

<a id="13-observabilidade"></a>
## 13. Observabilidade como requisito

Uma rota: Serilog e OpenTelemetry para o Collector, e do Collector para o backend (ADR-006). Não adicione uma segunda instrumentação ou exportação direta da aplicação; isso duplica dados, contorna a sanitização e cria duas verdades.

Ao criar um módulo, registre sua `ModuleTelemetry` e deixe o decorator de telemetria envolver os casos de uso. Ao criar uma métrica, defina rótulos de cardinalidade limitada — módulo, rota, resultado. Identificador de pessoa ou de recurso não é dimensão de métrica; pode ser propriedade de log ou atributo de trace, respeitando acesso e retenção.

Indicadores que valem a pena: latência p95/p99 por rota, taxa de falha, idade da mensagem pendente mais antiga na Outbox, entregas falhas, exportações recusadas. Alerta existe com ação esperada, não porque a métrica existe.

Liveness e readiness são coisas diferentes: indisponibilidade transitória do banco não deve reiniciar todas as instâncias.

<a id="14-testes"></a>
## 14. Testes proporcionais ao risco

| Tipo | O que verificar | Projeto |
|---|---|---|
| Unitário | Cálculo, transição, normalização, validator | `Tests.Unit` |
| Integração | Consulta traduzida, constraint, transação, lock, concorrência, Outbox | `Tests.Integration` |
| API | Rota, binding, validação, autorização, ProblemDetails, OIDC | `Tests.Integration` com `ApiFactory` |
| Funcional | Jornada de negócio em Gherkin pt-BR | `Tests.Functional` |
| Arquitetura | Fronteiras entre módulos e convenções | `Tests.Architecture` |

Teste de arquitetura é parte do desenho, não burocracia: ele é o único mecanismo que impede a fronteira entre módulos de erodir em silêncio. Ao criar um módulo, ele já entra nos testes pela descoberta de assemblies.

Priorize o que pode causar perda, acesso indevido ou duplicidade:

- Usuário de outro dono usa um identificador válido — leitura e escrita precisam negar.
- Dois clientes editam a mesma versão — o segundo não deve sobrescrever em silêncio quando o contrato exige detecção.
- A mesma mensagem é entregue duas vezes — o efeito de negócio não pode duplicar.
- A resposta se perde após o commit — reenviar ou consultar precisa preservar o resultado.
- Duas réplicas processam a fila ao mesmo tempo — claim, lease e confirmação condicional precisam segurar.

Persistência importante é testada com PostgreSQL real, no mesmo provider de produção. EF InMemory e SQLite não provam SQL, constraint nem semântica transacional. Duas instâncias de `WebApplicationFactory` compartilhando o mesmo banco exercitam concorrência lógica, não failover nem partição de rede — não afirme o que o teste não demonstra.

<a id="15-condicionais"></a>
## 15. Padrões e ferramentas que exigem justificativa

O benefício precisa superar o custo de código, operação e aprendizado. Antes de propor, escreva o ADR.

| Recurso | Adotar quando | Não adotar / adiar quando |
|---|---|---|
| Interface própria | Protege fronteira real entre módulos ou ponto de extensão do host | Apenas replica uma classe interna estável |
| Repositório | Nunca nesta base sem ADR que reabra ADR-001 | Sempre que for só renomear `DbSet` |
| Unit of Work adicional | Coordena responsabilidade que o `DbContext` e o decorator não atendem | Só encaminha `SaveChangesAsync` |
| MediatR ou despachante | Pipeline que os decorators atuais não resolvem | `IUseCase` + decorators já cobrem validação, autorização, transação e telemetria |
| CQRS com modelos separados | Leitura e escrita têm necessidades realmente diferentes e medidas | A divisão só acrescenta objetos |
| Event sourcing | O histórico de eventos é o modelo de verdade | Auditoria e histórico de alterações já bastam — e já existem |
| Mapeamento automático | Volume repetitivo com transformação verificável | Mapeamento explícito é curto e contém decisão de exposição |
| Broker externo | Consumidores fora do processo, escala própria, ordenação garantida necessária | Outbox in-process entrega o requisito atual |
| Cache (local ou distribuído) | Necessidade medida, com chave, escopo de autorização, validade e invalidação definidos | O problema é uma consulta ruim ou chamada desnecessária |
| Segundo `DbContext` num módulo | Nunca sem revisar a composição — `AddUseCasesFromAssembly` resolve o contexto com `Single` | Sempre; divida o módulo antes |
| Chave de consistência nova | O conjunto de invariantes é comprovadamente independente | Há qualquer escritor compartilhado; teste concorrente obrigatório |
| Multi-tenancy | Requisito de produto com isolamento definido | Antes de ter modelo de dados, policies e testes de isolamento |
| Extrair um módulo para outro processo | Autonomia, escala ou isolamento comprovados | Fronteiras ainda mudando; veja a seção [16](#16-evolucao) |

<a id="16-evolucao"></a>
## 16. Evolução e registro de decisões

**Melhorar o que existe.** Escolha uma dor concreta: regra duplicada, alteração espalhada, consulta cara, teste frágil, falha recorrente. Proteja o comportamento com teste, ajuste a fronteira envolvida, compare o resultado. Não misture reorganização ampla com mudança funcional na mesma PR.

**Extrair um módulo do monolito.** A ordem importa:

1. Estabilize o contrato do módulo e a propriedade dos dados — nenhum outro módulo lendo tabela dele.
2. Elimine a participação dele na chave de consistência compartilhada, ou aceite consistência eventual explicitamente onde havia transação.
3. Só então avalie latência, falha parcial, autenticação entre serviços, compatibilidade de contrato e observabilidade distribuída.

A existência de uma interface C# não torna uma chamada remota equivalente a uma local: o que era rollback vira compensação, e o que era leitura consistente vira dado possivelmente defasado. Extração é ADR novo, com plano de dados.

**Registrar a decisão.** ADRs desta base vivem em [`architecture.md`](architecture.md), numerados. Uma decisão nova segue o mesmo formato enxuto:

```text
Título e data:
Problema e restrições:
Decisão:
Alternativas consideradas:
Benefício esperado:
Custos e limitações aceitos:
Evidência e forma de verificar:
Condição que justifica rever:
```

Reavalie uma decisão quando houver evidência nova: latência crescente, incidentes recorrentes, dificuldade de testar, mudanças que sempre atravessam módulos, custo de implantação. Registre também o que **não** ficou provado — esta base é implementada e testada, não uma declaração de prontidão universal.

<a id="17-checklist"></a>
## 17. Checklist de arquitetura antes da PR

Marque só o que se aplica; registre o motivo quando a exclusão ajudar o revisor.

- [ ] A mudança está no módulo dono do dado e da regra.
- [ ] Nenhuma dependência nova entre módulos; contratos continuam transportando DTOs.
- [ ] `Domain/` continua sem conhecer `UseCases/`, `Shared/` e infraestrutura.
- [ ] Nenhum padrão condicional entrou sem ADR.
- [ ] Escrita de negócio está na fronteira transacional, com a chave de consistência correta e testada sob concorrência.
- [ ] O caso de uso é reexecutável: sem efeito externo, sem estado retido.
- [ ] Autorização cobre o recurso e o dono, não apenas o perfil.
- [ ] Invariantes persistidas têm constraint ou índice; a migração foi revisada.
- [ ] Evento novo tem nome estável, decisão sobre `requiresConsumer` e consumidor idempotente.
- [ ] Contrato HTTP: mudanças incompatíveis identificadas e tratadas.
- [ ] Telemetria e auditoria não recebem dado pessoal, token ou payload.
- [ ] Os riscos de perda, acesso indevido e duplicidade têm teste na camada adequada.
- [ ] `cd api && dotnet test` passou; o modo sem exemplo continua válido quando o núcleo foi tocado.
- [ ] Documentação afetada atualizada, incluindo ADR quando a decisão mudou.

## Manutenção deste guia

Atualize quando a experiência do projeto indicar orientação melhor, registrando problema observado, decisão e efeito. Mudança de invariante entra em [`CLAUDE.md`](../CLAUDE.md) e [`AGENTS.md`](../AGENTS.md); decisão arquitetural vira ADR em [`architecture.md`](architecture.md); prática de código vai para [`dotnet-practices.md`](dotnet-practices.md).

Base conceitual: *Arquitetura Limpa*, de Robert C. Martin — especialmente a regra de dependência e o custo de limites — adaptada a um monolito modular real, sem transformar seus princípios em obrigação de criar projetos por camada.
