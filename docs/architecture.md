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

Roles são apenas as do client da API e de uma allowlist; claims internas recebidas são removidas. A API decide propriedade e regras de recurso. Um token autenticado sem vínculo local ativo não entra.

Conta desativada e tokens anteriores ao corte local são rejeitados no próximo request. Remoção de role somente no IdP pode levar até 300 s + 15 s de skew; alteração urgente exige também corte local. Logout do provedor não invalida instantaneamente JWT já emitido.

## ADR-003 — Reexecução completa e commit indeterminado

Cada tentativa de comando cria escopo/DbContext novos e reexecuta autorização, leituras, regras e gravações. Não habilitamos retries transparentes do EF/Npgsql sobre contexto já alterado.

O receipt, com operationId, é gravado na mesma transação. Se a confirmação falhar, uma conexão nova adquire a mesma trava e procura a prova de commit. Se a prova existir, retorna o resultado já construído; se a transação anterior terminou sem a prova, a operação pode ser refeita. Se não for possível verificar, retorna 503 de resultado indeterminado; não declara rollback nem sucesso.

O receipt resolve a tentativa interna, **não** é uma chave de idempotência de um cliente que reenviou HTTP após queda total do processo. Operações futuras de pagamento ou efeitos externos precisam de chave de negócio/idempotency key, persistência do resultado e contrato específico. Não coloque chamadas externas com efeito dentro de unidade reexecutável: grave intenção na Outbox.

Limites padrão: 3 tentativas, lock 10 s, comando SQL 30 s. A expiração não representa deadline global de uma sequência arbitrária de consultas.

## ADR-004 — Consistência explícita no exemplo

Todos os comandos do exemplo usam a mesma chave PostgreSQL `event-management-example` antes de ler os módulos. Isso coordena invariantes entre DbContexts e réplicas sem transação distribuída, desde que **todas as escritas** respeitem a fronteira. Leituras cross-module enxergam commits anteriores; o mesmo lock impede alteração concorrente durante a decisão.

Há custo deliberado: as escritas do exemplo são serializadas. Não prometemos alto throughput desse desenho. Uma evolução medida pode adotar locks por agregado/recurso, com ordem estável e matriz completa dos participantes.

Defesas adicionais: exclusão temporal PostgreSQL por sala/intervalo ativo, unicidade de certificado pessoa/palestra e de inscrições ativas. Períodos adjacentes são permitidos. Local referenciado não pode desaparecer; alterações de agenda com palestras são recusadas; capacidade não pode ficar abaixo de confirmados. Certificado conserva snapshot histórico e oculta titular na consulta anônima.

O esquema não possui FK cross-module intencionalmente. Scripts ad hoc ou novos comandos sem a coordenação podem quebrar invariantes. Use contratos e inclua testes concorrentes.

## ADR-005 — Outbox, pelo menos uma vez

Estado e evento são atômicos no schema produtor. Claim tem token e prazo; renovação e ack/nack verificam dono e lease ainda válida. Timeout limita a espera pelo handler, mas código que ignora CancellationToken pode continuar executando; fencing protege o estado da fila, não um efeito externo.

Entrega pode repetir e não preserva ordem global/por agregado entre réplicas. Auditoria usa chave do evento e INSERT ON CONFLICT, com efeito idempotente. Novos consumidores precisam de deduplicação própria por evento/consumidor ou idempotência no destino.

Cada mensagem recebe escopo DI isolado; handlers da mesma mensagem compartilham esse escopo e devem ser independentes. Falha de um handler faz a mensagem inteira ser repetida. Um evento obrigatório sem consumidor falha. Eventos demonstrativos sem consumidor são explicitamente observacionais.

Cada módulo tem uma sequência independente de entregas. `Outbox:MaxConcurrentDeliveries` limita as entregas ativas por processo (padrão 4); a capacidade é adquirida **antes** do claim. Não se deixa mensagem com lease esperando no semáforo. Consumidores devem respeitar cancelamento: o limite não consegue interromper código externo que o ignore. A sonda é outro BackgroundService, com sequência independente por módulo e timeout próprio. Snapshot só é renovado após leitura bem-sucedida; falha mantém a idade do último sucesso. Não se acrescentou outro processo, broker ou garantia de ordenação entre réplicas.

Nomes de evento `domain.fact.v1` são estáveis, independentes de nomes CLR. Remover contratos/consumidores antes de drenar mensagens antigas é mudança incompatível. A padronização para inglês constitui uma nova base para bancos novos, sem compatibilidade implícita com eventos persistidos na versão anterior. Dead letter é terminal explícito; replay individual exige administrador e reasonCode, registra ator e nunca é automático. Retenção apaga somente processados/receipts antigos em lotes; pendentes e terminais não são apagados.

## ADR-006 — Uma rota de telemetria

Serilog + OpenTelemetry → Collector → backend. Ambiente local: Prometheus, Tempo, Loki e Grafana com persistência. Produção: backend OTLP autenticado/TLS, escolhido pela operação. Azure pode ser esse destino através da plataforma de telemetria; não instalamos uma segunda instrumentação/exportação automática na aplicação.

Sem SQL com valores, payloads pessoais, tokens ou stack traces livres. Logs com Exception são substituídos, antes dos sinks configurados/DI/OTLP, por evento sanitizado: tipo, código, fingerprint técnico e contexto permitido, preservando nível/horário/trace/span. Mensagem, template original, Data, inner messages, stack bruto e propriedades arbitrárias não são exportados. Logs SQL automáticos sem exceção continuam suprimidos; o interceptor gera registro sanitizado. Mensagens normais continuam exigindo disciplina de minimização: um filtro não torna qualquer texto livre seguro. Métricas usam rótulos limitados, nunca pessoaId/e-mail/URL livre.

Collector expõe métricas internas em 8888 somente na rede privada. A coleta independente monitora disponibilidade, fila, exportações falhas e dados recusados. O smoke usa uma requisição OIDC nova, consulta seu trace e correlação no Loki, exige avanço do contador por rota e procura vazamento de sentinela de query. Métrica HTTP é agregada por rota, não rotulada com traceId.

Amostragem parent-based 10% em produção, 100% local. Outbox propaga traceparent; mensagens tardias/repetidas podem cair fora da janela consultada. Trace completo não é garantia de amostragem.

## ADR-007 — Implantação e responsabilidade operacional

Manifesto produtivo independente, TLS na borda, somente 443 publicada, proxies conhecidos, API não root/read-only, secrets por arquivo e usuários DDL/DML distintos. Migração é job explícito com lock; startup normal recusa migrações pendentes. Auditoria/replay têm SELECT/INSERT para runtime, sem UPDATE/DELETE.

Os dumps de demonstração comprovam restauração lógica local, não disaster recovery regional. Banco/IdP/telemetria de produção precisam de backup externo criptografado, alta disponibilidade conforme RTO, monitoramento e exercício com responsáveis. Privilégios de proprietário/DBA ainda podem alterar auditoria; imutabilidade forte exige cópia externa protegida.

## Referências de implementação

- [EF Core: resiliência e commit indeterminado](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency).
- [ASP.NET Core: validação JWT Bearer](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication).
- [Keycloak: endpoints OIDC](https://www.keycloak.org/securing-apps/oidc-layers).
- [OpenTelemetry Collector: configuração](https://opentelemetry.io/docs/collector/configuration/).
- [Tempo: modos de implantação](https://grafana.com/docs/tempo/latest/reference-tempo-architecture/deployment-modes/). O armazenamento local do laboratório não substitui backend produtivo dimensionado.
