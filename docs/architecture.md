# Arquitetura e decisões

## Fronteiras

Um processo ASP.NET Core hospeda módulos independentes; um PostgreSQL contém schemas por módulo. Chamadas síncronas entre módulos usam interfaces de `Shared.Contracts`. Eventos persistidos seguem Outbox e consumidores in-process. A extração de um módulo para outro processo exige rever transações, contratos e consistência; não é apenas mudar DI.

O núcleo oferece Identidade/Auditoria. Locais, Pessoas, Eventos e Palestras são demonstração opt-in. Não há dependência do domínio de eventos nas bibliotecas compartilhadas. O template instala apenas os módulos presentes no projeto gerado.

Caminho de escrita: HTTP → autenticação → limite por usuário → policy de endpoint → validação → decoração transacional → lock PostgreSQL → policy do recurso → caso de uso/domínio → negócio + Outbox + receipt → commit. Leituras também exigem policy do módulo, mas não obtêm lock de escrita.

## ADR-001 — Monolito modular e vertical slices

Mantemos `Host.Api / Module.* / Shared.*` e `UseCases/<Name>`. Não criamos Domain/Application/Infrastructure como projetos repetidos por convenção. A regra de negócio fica no domínio; orquestração no caso de uso; adaptação no módulo. Contratos transportam DTOs, não entidades EF nem IQueryable.

Consequência: deploy e recursos são compartilhados. Testes arquiteturais protegem as referências; ownership e disciplina continuam necessários.

## ADR-002 — Keycloak exclusivo, identidade interna estável

Nenhuma senha de usuário ou chave de emissão JWT é armazenada pela API. Discovery/JWKS validam assinatura RSA, issuer exato, audience, expiração e tipo Bearer; `sub` é texto opaco. Identidade local = `(issuer, subject)` único → Guid interno.

Roles vêm do caminho configurado em `Oidc:RoleClaimPath` (padrão: client roles do Keycloak, `resource_access.{audience}.roles`), passam pelo mapa opcional `Oidc:RoleMap` e pela allowlist `Oidc:AllowedRoles`; claims internas recebidas são removidas. A exigência de `typ` é configurável (padrão `Bearer`) para IdPs que não emitem essa claim, com os riscos descritos em `security.md`. A allowlist configurada substitui a padrão: até esta revisão, o binder acrescentava os valores configurados aos três perfis padrão, e a configuração não conseguia reduzir a lista. A API decide propriedade e regras de recurso. Um token autenticado sem vínculo local ativo não entra.

Conta desativada e tokens anteriores ao corte local são rejeitados no próximo request. Remoção de role somente no IdP pode levar até 300 s + 15 s de skew; alteração urgente exige também corte local. Logout do provedor não invalida instantaneamente JWT já emitido.

## ADR-003 — Reexecução completa e commit indeterminado

Cada tentativa de comando cria escopo/DbContext novos e reexecuta autorização, leituras, regras e gravações. Não habilitamos retries transparentes do EF/Npgsql sobre contexto já alterado.

O receipt, com operationId, é gravado na mesma transação. Se a confirmação falhar, uma conexão nova adquire a mesma trava e procura a prova de commit. Se a prova existir, retorna o resultado já construído; se a transação anterior terminou sem a prova, a operação pode ser refeita. Se não for possível verificar, retorna 503 de resultado indeterminado; não declara rollback nem sucesso.

O receipt resolve a tentativa interna, **não** é uma chave de idempotência de um cliente que reenviou HTTP após queda total do processo. Operações futuras de pagamento ou efeitos externos precisam de chave de negócio/idempotency key, persistência do resultado e contrato específico. Não coloque chamadas externas com efeito dentro de unidade reexecutável: grave intenção na Outbox.

Limites padrão: 3 tentativas, lock 10 s, comando SQL 30 s. A expiração não representa deadline global de uma sequência arbitrária de consultas.

## ADR-004 — Consistência explícita no exemplo

Todos os comandos do exemplo usam a mesma chave PostgreSQL `event-management-example` antes de ler os módulos. Isso coordena invariantes entre DbContexts e réplicas sem transação distribuída, desde que **todas as escritas** respeitem a fronteira. Leituras cross-module enxergam commits anteriores; o mesmo lock impede alteração concorrente durante a decisão.

Há custo deliberado: as escritas do exemplo são serializadas. Não prometemos alto throughput desse desenho. Uma evolução medida pode adotar locks por agregado/recurso, com ordem estável e matriz completa dos participantes. A base passou a oferecer chave por recurso (ADR-009), mas o exemplo continua na chave compartilhada: suas invariantes atravessam módulos, e trocar a chave exige essa matriz e testes concorrentes.

Defesas adicionais: exclusão temporal PostgreSQL por sala/intervalo ativo, unicidade de certificado pessoa/palestra e de inscrições ativas. Períodos adjacentes são permitidos. Local referenciado não pode desaparecer; alterações de agenda com palestras são recusadas; capacidade não pode ficar abaixo de confirmados. Certificado conserva snapshot histórico e oculta titular na consulta anônima.

O esquema não possui FK cross-module intencionalmente. Scripts ad hoc ou novos comandos sem a coordenação podem quebrar invariantes. Use contratos e inclua testes concorrentes.

## ADR-005 — Outbox, pelo menos uma vez

Estado e evento são atômicos no schema produtor. Claim tem token e prazo; renovação e ack/nack verificam dono e lease ainda válida. Timeout limita a espera pelo handler, mas código que ignora CancellationToken pode continuar executando; fencing protege o estado da fila, não um efeito externo.

Entrega pode repetir e não preserva ordem global/por agregado entre réplicas. Handlers registrados por `AddIntegrationEventHandler` passam pela **Inbox** do módulo consumidor: a tabela `InboxMessages` (PK `EventId` + `Consumer`) é gravada com `INSERT … ON CONFLICT DO NOTHING` na mesma transação do efeito do handler, no `DbContext` do módulo. Par já existente significa efeito já confirmado, e o handler não roda; entrega concorrente do mesmo evento espera a transação que inseriu primeiro. O nome do consumidor é o `FullName` do handler ou `[InboxConsumer("nome")]`; renomear sem fixar o nome cria um consumidor novo. `[SkipInbox("justificativa")]` abre mão da Inbox. A Auditoria usa esse opt-out: a PK do evento com `INSERT ON CONFLICT` já torna o efeito idempotente, a tabela é append-only e é o consumidor de maior volume.

Cada handler registrado recebe escopo DI próprio, com `DbContext` próprio, para que o estado de um handler que falhou não contamine o seguinte. Falha de um handler faz a mensagem inteira ser repetida, mas a Inbox evita reaplicar os handlers que já tinham confirmado; por isso não se mantém estado de entrega por handler na Outbox. Custo: um INSERT por evento e consumidor e uma tabela que cresce até a retenção. Só a escrita no `DbContext` do próprio módulo entra na transação da Inbox; efeito em outro lugar (outro módulo, serviço externo) continua exigindo idempotência no destino. Um evento obrigatório sem consumidor falha. Eventos demonstrativos sem consumidor são explicitamente observacionais.

Cada módulo tem uma sequência independente de entregas. `Outbox:MaxConcurrentDeliveries` limita as entregas ativas por processo (padrão 4); a capacidade é adquirida **antes** do claim. Não se deixa mensagem com lease esperando no semáforo. Consumidores devem respeitar cancelamento: o limite não consegue interromper código externo que o ignore. A sonda é outro BackgroundService, com sequência independente por módulo e timeout próprio. Snapshot só é renovado após leitura bem-sucedida; falha mantém a idade do último sucesso. Não se acrescentou outro processo, broker ou garantia de ordenação entre réplicas.

Nomes de evento `domain.fact.v1` são estáveis, independentes de nomes CLR. Remover contratos/consumidores antes de drenar mensagens antigas é mudança incompatível. A padronização para inglês constitui uma nova base para bancos novos, sem compatibilidade implícita com eventos persistidos na versão anterior. Dead letter é terminal explícito; replay individual exige administrador e reasonCode, registra ator e nunca é automático. Retenção apaga somente processados/receipts antigos em lotes; pendentes e terminais não são apagados. Entradas da Inbox ficam `Outbox:InboxRetentionDays` (padrão 30, nunca menos que a retenção de processados); replay de dead letter mais antigo que esse prazo reaplica handlers que já tinham terminado.

## ADR-006 — Uma rota de telemetria

Serilog + OpenTelemetry → Collector → backend. Ambiente local: Prometheus, Tempo, Loki e Grafana com persistência. Produção: backend OTLP autenticado/TLS, escolhido pela operação. Azure pode ser esse destino através da plataforma de telemetria; não instalamos uma segunda instrumentação/exportação automática na aplicação.

Sem SQL com valores, payloads pessoais, tokens ou stack traces livres. Logs com Exception são substituídos, antes dos sinks configurados/DI/OTLP, por evento sanitizado: tipo, código, fingerprint técnico e contexto permitido, preservando nível/horário/trace/span. Mensagem, template original, Data, inner messages, stack bruto e propriedades arbitrárias não são exportados. Logs SQL automáticos sem exceção continuam suprimidos; o interceptor gera registro sanitizado. Mensagens normais continuam exigindo disciplina de minimização: um filtro não torna qualquer texto livre seguro. Métricas usam rótulos limitados, nunca pessoaId/e-mail/URL livre.

Collector expõe métricas internas em 8888 somente na rede privada. A coleta independente monitora disponibilidade, fila, exportações falhas e dados recusados. O smoke usa uma requisição OIDC nova, consulta seu trace e correlação no Loki, exige avanço do contador por rota e procura vazamento de sentinela de query. Métrica HTTP é agregada por rota, não rotulada com traceId.

Amostragem parent-based 10% em produção, 100% local. Outbox propaga traceparent; mensagens tardias/repetidas podem cair fora da janela consultada. Trace completo não é garantia de amostragem.

## ADR-007 — Implantação e responsabilidade operacional

Manifesto produtivo independente, TLS na borda, somente 443 publicada, proxies conhecidos, API não root/read-only, secrets por arquivo e usuários DDL/DML distintos. Migração é job explícito com lock; startup normal recusa migrações pendentes. Auditoria/replay têm SELECT/INSERT para runtime, sem UPDATE/DELETE.

Os dumps de demonstração comprovam restauração lógica local, não disaster recovery regional. Banco/IdP/telemetria de produção precisam de backup externo criptografado, alta disponibilidade conforme RTO, monitoramento e exercício com responsáveis. Privilégios de proprietário/DBA ainda podem alterar auditoria; imutabilidade forte exige cópia externa protegida.

## ADR-008 — Autorização por caso de uso e núcleo sem infraestrutura

**Título e data:** policy de acesso por slice e `Shared.Kernel`, 2026-10-05.

**Problema e restrições:**
- Cada módulo tinha uma `IModuleAccessPolicy` com `CanExecuteAsync(object request)`: um `switch` sobre o tipo do request, fora do slice e sem segurança de tipo, que crescia a cada caso de uso.
- A ausência de policy só aparecia na primeira chamada, e o decorator resolvia as policies de todos os módulos a cada execução.
- `Domain/` usava `Result`/`Error` de `Shared.Http` e `BaseEntity` de `Shared.Data`, projetos que dependem de ASP.NET Core e EF Core.
- A mudança não pode alterar quem acessa o quê, nem a posição da autorização (dentro da transação e do lock nos comandos).

**Decisão:**
- Cada caso de uso tem uma `IAccessPolicy<TRequest>` no próprio diretório.
- `AddUseCasesFromAssembly` registra a policy de cada request e interrompe a composição quando falta policy ou há mais de uma. O `AuthorizedUseCaseDecorator` mantém a posição e o erro `Authorization.ResourceDenied`.
- `Result`, `Error`, `ErrorType`, `BaseEntity`, `IAuditableEntity`, `IEventEmitter` e `IImmutableRecord` passam para `Shared.Kernel`, que referencia apenas `Shared.Contracts` (por `IIntegrationEvent`).

**Alternativas consideradas:**
- Classe base genérica de policy: recusada, porque esconderia a ordem das verificações (autenticado, administrador, dono).
- Manter a policy por módulo com validação de cobertura: recusada, porque mantém o `switch` sem tipo e fora do slice.
- Mover `IIntegrationEvent` para o Kernel: recusada por mexer em todos os contratos de evento sem ganho.

**Benefício esperado:**
- Autorização legível ao lado do caso de uso.
- Falha no startup em vez de 500 na primeira chamada.
- Resolução de uma policy por chamada.
- `Domain/` isolado de infraestrutura por teste.

**Custos e limitações aceitos:**
- Um arquivo a mais por slice.
- A abertura "autenticado / administrador" se repete em várias policies, de forma deliberada.
- Regra repetida entre policies vira serviço pequeno em `Module.<Name>/Shared/`, registrado pelo módulo.

**Evidência e forma de verificar:**
- `UseCaseConventionTests`: exatamente uma policy por caso de uso, no namespace do slice.
- `ModuleBoundaryTests`: dependências do Domain e do Kernel.
- `AccessPolicyCompositionTests`: falha de composição e erro 403.
- Os testes de integração e funcionais de acesso negado passaram sem alteração. A migração preservou regras implícitas; por exemplo, `ListPeople` continua só para administrador.

**Condição que justifica rever:** policies que precisem de dados de vários módulos de forma recorrente, ou necessidade de autorização declarativa (atributos) para auditoria externa de permissões.

## ADR-009 — Chave de consistência padrão e por recurso

**Título e data:** `[Command]` sem argumento e placeholders resolvidos do request, 2026-10-05.

**Problema e restrições:**
- A chave de `[Command("...")]` era uma string fixa. Não havia trava por recurso: todas as escritas com a mesma chave faziam fila numa só, mesmo sobre recursos sem relação.
- O dev precisava inventar uma chave até para o caso comum (invariantes de um módulo só).
- A chave errada não falha: produz corrupção sob concorrência.

**Decisão:**
- `[Command]` sem argumento usa o nome do módulo (`ModuleTelemetry.Module`, igual ao schema).
- `[Command("events:{EventId}")]` resolve os placeholders a partir de propriedades `Guid` ou `string` do request, antes de abrir a transação.
- O template é compilado em `AddUseCasesFromAssembly`. Placeholder inexistente, de outro tipo ou malformado falha no startup, e `UseCaseConventionTests` repete a validação.
- A chave é resolvida uma vez por chamada; todas as tentativas e o `VerifyCommittedAsync` usam a mesma trava resolvida.
- `Guid.Empty`, string vazia ou request nulo lançam exceção (contrato violado), sem recuar para a chave do módulo. O recuo deixaria dois comandos sobre o mesmo recurso com travas diferentes.
- Valores `string` são normalizados para maiúsculas; outra normalização é do request.

**Alternativas consideradas:**
- Recuar para a chave do módulo quando o valor vem vazio: recusada pelo motivo acima.
- Várias chaves por comando: adiada. Exige ordem estável de aquisição para evitar deadlock e a matriz de participantes.
- Trocar a chave do exemplo: fora de escopo (ADR-004).

**Benefício esperado:** padrão seguro sem escolha manual; paralelismo entre recursos independentes quando a invariante permite; erro de digitação no placeholder detectado no startup.

**Custos e limitações aceitos:**
- Uma chave por comando.
- Chave por recurso não protege invariante que também depende de outro recurso.
- Valor vazio em placeholder vira 500. Por isso a propriedade precisa de `NotEmpty` no Validator, que roda antes.
- O `Module.Identity` passou de `[Command("identity")]` para `[Command]` (chave `Identity`). A semântica é a mesma, mas durante um deploy gradual réplicas antigas e novas usam travas diferentes até a troca terminar.
- Chaves fixas mantêm o mesmo identificador de lock das versões anteriores.

**Evidência e forma de verificar:**
- `ConsistencyKeyLockTests` (PostgreSQL real): recursos diferentes em paralelo, mesmo recurso em fila e `[Command]` sem argumento em fila no módulo.
- `ConsistencyKeyTests`: parsing, normalização, valores vazios e compatibilidade do lock fixo.
- `UseCaseConventionTests`: placeholders válidos em todos os módulos.

**Condição que justifica rever:** comandos que precisem coordenar dois recursos ao mesmo tempo, ou contenção medida na chave do módulo.

## ADR-010 — Bibliotecas Shared.* distribuídas como pacotes

**Título e data:** pacotes NuGet para as bibliotecas genéricas, 2026-10-05.

**Problema e restrições:**
- Cada projeto gerado recebia uma cópia de ~3 mil linhas de `Shared.*`; correção na base precisava ser repetida em cada projeto.
- O repositório do template precisa continuar desenvolvendo `Shared.*` como projetos.
- `Shared.Contracts` misturava contratos genéricos com contratos dos módulos de cada projeto.

**Decisão:**
- **Pacotes:** `Shared.Kernel`, `Contracts`, `Data`, `Http`, `Messaging`, `Observability` e `WebHost` viram pacotes `Shared.<Nome>` com o prefixo de `SharedPackagePrefix`, versão SemVer única (1.0.0 inicial), SourceLink e PDB embutido.
- **Contratos dos módulos:** vão para `Shared.Contracts.Modules`, que fica no projeto, com os mesmos namespaces. O registro de eventos procura nos assemblies `Shared.Contracts*`.
- **Referências:** projetos declaram `SharedReference`, que vira `ProjectReference` na origem e `PackageReference` no gerado (`UseSharedPackages`).
- **Feed:** GitHub Packages, com `packageSourceMapping` (só os pacotes da base vêm do feed privado) e credenciais por variável de ambiente e secret de build.
- **Publicação:** workflow manual `release-packages`, com job de publicação no environment `packages` (aprovação).
- **Identity e Audit:** `Module.Identity` e `Module.Audit` continuam código-fonte no projeto. As migrações vivem no assembly do módulo, e o projeto precisa poder evoluir schema, regras e endpoints.
- **Nomes neutros no código compartilhado:** a chave da connection string passa a ser `ConnectionStrings:Database` e as anotações de auditoria, `Shared:*`. O código empacotado não acompanha mais a troca de nome do template.
- **Modo de geração (revisão de 2026-10-05):** o pacote passa a ser opcional. `--sharedMode source`, o padrão, copia o código das sete bibliotecas e os testes delas para o projeto, com `UseSharedPackages=false`, sem `nuget.config` e sem feed; o código copiado acompanha a troca de nome do template. `--sharedMode package` mantém o consumo por pacote descrito acima. Motivo: projetos gerados para produtos sem relação entre si (bancos, infraestrutura e ciclos de entrega próprios) preferem ser donos do código a depender de um feed privado e de uma versão central.

**Alternativas consideradas:**
- Pacote também para Identity/Audit: atualização central, mas migrações e regras presas à versão do pacote.
- Submódulo Git: acoplamento de build e fluxo mais difícil para o dev.
- Manter a cópia: o custo de propagar correções continuaria por projeto.

**Benefício esperado:** correção de infraestrutura propagada por troca de versão; o dev mexe só nos módulos.

**Custos e limitações aceitos:**
- **Credenciais:** feed privado exige credencial em máquina, CI e build de imagem.
- **Migrações:** versões que mudam convenções de modelo exigem migração nova em cada módulo.
- **Testes:** os testes de arquitetura do projeto usam alguns internals (`InternalsVisibleTo`); os testes das bibliotecas ficam só na origem.
- **Lock files:** não são copiados; o primeiro `restore` do projeto gera os seus.
- **Configuração:** a chave da connection string mudou (incompatível; registrado no `CHANGELOG.md`).

**Evidência e forma de verificar:**
- `test-template.mjs` empacota num feed em pasta, gera os três projetos consumindo os pacotes e recusa biblioteca copiada, prefixo renomeado ou solução listando as bibliotecas.
- `test-template.mjs` também gera um projeto no modo `source` e recusa biblioteca ausente, `UseSharedPackages` ligado, `nuget.config` ou solução sem as bibliotecas, antes de rodar os testes dele.
- O workflow de release valida versão, `CHANGELOG`, testes e template antes de publicar.

**Condição que justifica rever:** necessidade de versões independentes por biblioteca, ou de distribuir Identity/Audit de forma central.

## ADRs propostos (não implementados)

Os ADRs abaixo estão com status **Proposto**: registram opções e uma recomendação para decisões que a base ainda não tomou. Nada aqui está implementado. A equipe decide, e só então o ADR passa a **Aceito**, com código e testes.

## ADR-011 — Jobs agendados

**Status:** Proposto, 2026-10-05.

**Contexto:**
- **O que existe hoje:** não há agendador. As tarefas periódicas são da infraestrutura: o `OutboxProcessor` faz a retenção uma vez por hora e a `OutboxProbe` atualiza o snapshot.
- **O que vem pela frente:** produtos costumam precisar de rotinas de negócio, como expirar inscrições, enviar lembretes ou consolidar relatórios.
- **Restrições desta base:**
  - várias réplicas não podem executar a mesma rotina ao mesmo tempo;
  - escrita de negócio passa pela fronteira transacional com a chave de consistência (ADR-003, ADR-009) e pela policy de acesso;
  - efeito externo vira intenção na Outbox;
  - não há broker.

**Opções:**

| Opção | Prós | Contras |
|---|---|---|
| A. `BackgroundService` com `PeriodicTimer` e advisory lock por job (só uma réplica executa), mais uma tabela `JobRuns` no schema do módulo | Sem dependência nova. Reaproveita o lock e a observabilidade da base. A rotina chama um `IUseCase` com `[Command]`, igual à API. | Sem cron completo nem tratamento de execução perdida (misfire). O histórico é só o que a tabela guardar. Cada job precisa de código de agendamento. |
| B. Quartz.NET com job store PostgreSQL em cluster | Cron, misfire, clustering e histórico prontos. | Dependência e tabelas próprias (schema e migrações à parte). Mais um modelo mental. A execução precisa ser encaixada na fronteira transacional da base. |
| C. Hangfire com storage PostgreSQL | Painel, retries e fila persistente. | Painel é superfície de ataque (autorização própria). Serializa chamadas de método. Recursos relevantes são pagos. Mais pesado que o necessário. |
| D. Agendador externo (CronJob do Kubernetes, scheduler da nuvem) chamando um comando do host, como já existe `migrate` | Isolado do processo web. A operação controla horário, retry e alerta. | Depende da plataforma de deploy. A configuração fica fora do repositório da aplicação. Cada execução sobe um processo. |

**Recomendação:**
- **A** para rotinas curtas e frequentes, dentro do processo:
  - um `IScheduledJob` registrado pelo módulo;
  - advisory lock com a chave do job;
  - execução por `IUseCase` com identidade técnica de sistema, `[Command]` e policy própria;
  - métrica de última execução e alerta de atraso.
- **D** para lotes pesados ou raros.
- **Reavaliar B** quando houver muitos jobs com cron e necessidade real de misfire.

**Condição para decidir:**
- o primeiro requisito de rotina de negócio com horário;
- o SLO de atraso aceitável;
- a plataforma de deploy (se oferece CronJob).

## ADR-012 — Upload e armazenamento de arquivos

**Status:** Proposto, 2026-10-05.

**Contexto:**
- A API não recebe arquivos. O corpo das requisições é limitado a 1 MiB, e conteúdos de palestra são só URLs.
- Arquivos costumam ser dados pessoais ou documentos (RG, contrato, foto), com exigência de retenção e descarte.
- A unidade transacional é reexecutável (ADR-003), então gravar arquivo dentro do comando duplicaria efeito externo.

**Opções:**

| Opção | Prós | Contras |
|---|---|---|
| A. `bytea` no PostgreSQL, na tabela do módulo | Transação única com os metadados; backup junto do banco. | Banco e backups crescem rápido; streaming e limites ruins; pressão sobre o pool de conexões. |
| B. Object storage (S3 compatível ou Azure Blob) com URL pré-assinada; metadados e estado no módulo | Upload direto do cliente, sem passar pela API; escala e custo adequados; retenção por política do bucket. | Infraestrutura nova; consistência entre metadado e objeto exige estados e reconciliação; MinIO no ambiente local. |
| C. Volume de filesystem montado no host | Simples no local. | Não serve para várias réplicas sem storage compartilhado; backup e permissões manuais. |

**Recomendação:** **B**, com as seguintes regras:
- **Ciclo de vida:**
  1. o comando grava a intenção (metadado `Pending`, dono, tamanho e tipo declarados) e devolve uma URL pré-assinada de curta duração;
  2. o cliente envia o arquivo direto ao storage;
  3. um evento de confirmação (ou o próprio cliente) dispara verificação assíncrona pela Outbox: tamanho, tipo real por assinatura e antivírus;
  4. só então o arquivo passa a `Available`.
- **Proteções:**
  - bucket privado;
  - download sempre por URL pré-assinada emitida depois da policy do recurso;
  - auditoria só do metadado, nunca do conteúdo nem do nome original sem sanitização;
  - retenção e exclusão definidas pelo produto.
- **Ambiente local:** MinIO num profile próprio, fora do modo lite.

**Condição para decidir:**
- os tipos e tamanhos de arquivo do produto;
- a exigência de antivírus;
- a plataforma de storage disponível;
- a política de retenção (LGPD).

## ADR-013 — Cache

**Status:** Proposto, 2026-10-05.

**Contexto:**
- Não há cache: toda leitura vai ao PostgreSQL, com projeção e `TagWith`.
- Há várias réplicas, então o cache em memória de uma réplica não vê a escrita de outra.
- A autorização é por recurso e por usuário: um cache posicionado antes da policy vazaria dados.
- Dados pessoais em cache estendem o tempo de retenção.

**Opções:**

| Opção | Prós | Contras |
|---|---|---|
| A. Sem cache (padrão atual), com consulta otimizada e índices | Sem inconsistência nem invalidação; mais simples. | Toda leitura custa banco. |
| B. `HybridCache` do .NET só em memória (L1), para dados de referência não pessoais com TTL curto | Sem infraestrutura nova; proteção contra stampede embutida. | Réplicas divergem até o TTL vencer; invalidação só local. |
| C. `HybridCache` com L2 distribuído (Redis) | Coerência entre réplicas; invalidação central. | Infraestrutura nova (Redis com HA, TLS, credenciais); mais um ponto de falha; dado pessoal fora do banco. |
| D. Cache HTTP (`ETag`, `Cache-Control`) em leituras públicas ou do próprio usuário | Sem estado no servidor; o cliente economiza banda. | Só vale para respostas estáveis; com `private` e `Vary` errados, uma resposta autenticada vaza. |

**Recomendação:**
- **A** continua o padrão.
- Diante de **medida** (latência ou carga no banco) para dados de referência, adotar **B**:
  - chave com o nome do caso de uso;
  - cache aplicado **depois** da policy e nunca de decisão de autorização;
  - invalidação pelo evento de integração da escrita (consumidor com Inbox), mais TTL como rede de segurança.
- **C** só com várias réplicas e divergência inaceitável, em ADR próprio para o Redis.
- **D** para leituras anônimas como a validação de certificado.

**Condição para decidir:**
- uma métrica de latência ou de carga que justifique o cache;
- quais dados são de referência;
- a tolerância a dado defasado por caso de uso.

## ADR-014 — Chave de idempotência para requisições do cliente

**Status:** Proposto, 2026-10-05.

**Contexto:**
- O `CommandReceipt` (ADR-003) resolve o retry **interno** e o commit indeterminado. Ele não resolve o cliente que reenvia a mesma requisição depois de timeout, queda de conexão ou 503.
- Operações com efeito sensível (pagamento, inscrição paga, emissão de documento) precisam que o reenvio devolva o mesmo resultado sem repetir o efeito.
- Hoje, a proteção vem de restrições únicas de negócio (inscrição ativa única, certificado único).

**Opções:**

| Opção | Prós | Contras |
|---|---|---|
| A. Cabeçalho `Idempotency-Key` com tabela `(chave, usuário, rota, hash do request, estado, resultado)` gravada na mesma transação do comando | Padrão conhecido; resolve reenvio genérico; integra com a fronteira transacional (lock e receipt). | Tabela e retenção novas; precisa definir o que guardar da resposta sem guardar dado pessoal; o cliente precisa gerar e reutilizar a chave. |
| B. Chave natural de negócio com restrição única por operação | Sem infraestrutura genérica; a regra fica explícita no domínio. | Nem toda operação tem chave natural; o reenvio recebe 409 em vez do resultado original. |
| C. Id do recurso gerado pelo cliente (`PUT /recursos/{id}`) | Reenvio idempotente por construção. | Muda o contrato da API; o cliente passa a escolher ids; não serve para ações que não criam recurso. |

**Recomendação:** **A** como opt-in por endpoint, combinada com **B** onde existir chave natural.
- **Como funciona:**
  - um atributo `[Idempotent]` no caso de uso faz o `TransactionalUseCaseDecorator` gravar a chave no mesmo commit;
  - mesma chave com outro request: 422;
  - a mesma chave ainda em andamento: 409;
  - a mesma chave já concluída: devolve o status e o id do recurso gravados, sem o corpo com dados pessoais (o cliente relê o recurso, se precisar).
- **Retenção** curta e configurável, com escopo por usuário e rota.
- **Não implementar** antes de existir uma operação com efeito que justifique.

**Condição para decidir:**
- a primeira operação com efeito não reversível;
- o contrato com os clientes (quem gera a chave e por quanto tempo vale).

## ADR-015 — Multi-tenancy

**Status:** Proposto, 2026-10-05.

**Contexto:** a base é de uma organização só (`extending.md`). Atender vários clientes no mesmo sistema afeta quase tudo:
- entidades, contratos e índices únicos (passam a ser por tenant);
- policies;
- chaves de consistência (que precisam incluir o tenant);
- Outbox e auditoria;
- identidade (issuer ou claim de tenant);
- telemetria (tenant como rótulo explode a cardinalidade);
- backup e restauração por cliente;
- vizinho barulhento.

**Opções:**

| Opção | Prós | Contras |
|---|---|---|
| A. Implantação separada por cliente (silo: banco, IdP e API próprios) | Isolamento total; nenhuma mudança de código; backup e restore por cliente naturais. | Custo e operação crescem com o número de clientes; atualização e monitoramento multiplicados. |
| B. Banco por tenant, mesma aplicação | Isolamento forte de dados; restore por cliente; o código só resolve a conexão por tenant. | Migrações e pool de conexões por banco; o job `migrate` vira laço; o advisory lock é por banco (pode ser vantagem); o custo cresce com os tenants. |
| C. Schema por tenant | Isolamento médio. | **Conflita com o schema por módulo** (seriam tenants × módulos schemas); proteção do migrador e privilégios ficam complexos. Não recomendado aqui. |
| D. Schema compartilhado com `TenantId` em toda tabela, filtro global do EF e Row-Level Security do PostgreSQL como segunda barreira | Uma implantação, custo baixo por cliente; escala para muitos clientes pequenos. | Mudança em todos os módulos, contratos, índices, chaves de consistência e testes de isolamento; erro de filtro vira vazamento entre clientes; restore por cliente é difícil. |

**Recomendação:**
- **A** para poucos clientes grandes ou com exigência contratual de isolamento.
- **D**, com RLS obrigatória, se o produto precisar de muitos clientes pequenos. Isso é projeto próprio, com matriz de testes de isolamento e revisão de cada invariante (inclusive `ADR-004` e `ADR-009`).
- **B** como meio-termo, quando o isolamento de dados importar mais que o custo.
- **Não implementar sem decisão de produto:** o template continua de uma organização só.

**Condição para decidir:**
- o modelo comercial (quantos clientes e de que tamanho);
- as exigências contratuais de isolamento e residência de dados;
- a necessidade de restaurar um cliente sem afetar os outros.

## Referências de implementação

- [EF Core: resiliência e commit indeterminado](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency).
- [ASP.NET Core: validação JWT Bearer](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication).
- [Keycloak: endpoints OIDC](https://www.keycloak.org/securing-apps/oidc-layers).
- [OpenTelemetry Collector: configuração](https://opentelemetry.io/docs/collector/configuration/).
- [Tempo: modos de implantação](https://grafana.com/docs/tempo/latest/reference-tempo-architecture/deployment-modes/). O armazenamento local do laboratório não substitui backend produtivo dimensionado.
