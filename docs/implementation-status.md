# Matriz de rastreabilidade — entrega do template

Referência: análise original em `docs/architecture-review.md` no repositório de origem. Este registro descreve a base entregue em 20/09/2026; não certifica futuras adaptações ou ambientes produtivos. O projeto original não foi alterado.

**Implementado/verificado** significa evidência local de código/teste/configuração. **Parcial/externo** significa que o aceite completo depende de produto, plataforma ou sistema que não foi incluído. Não declaramos todas as lacunas encerradas apenas por existir um scaffold.

## Decisões confirmadas

Monolito modular .NET 10, casos de uso verticais, EF Core direto, PostgreSQL e Outbox. Keycloak exclusivo como IdP. Núcleo genérico com Identidade/Auditoria; exemplo de eventos opt-in. API sem frontend, uma organização, ownership seguro no exemplo. Sem migração automática da autenticação antiga.

| ID | Implementação/evidência | Estado e condição restante |
|---|---|---|
| D1 / reutilização | dotnet new, referência entre módulos proibida, geração sem 4 módulos/contratos de exemplo | Implementado; script test-template executa as quatro suítes nos dois modos |
| SEG-01 | Compose produtivo independente, somente 443, segredos por arquivo, imagens por digest; validate-production | Implementado; promover exige valores/infra reais |
| SEG-02 | KnownProxies explícitos, ForwardLimit=1, limite por usuário resiste XFF forjado | Implementado; validar topologia real do proxy |
| SEG-03 | PeopleAccessPolicy restringe dados pessoais ao titular/admin; binding interno | Implementado; teste IDOR e cenários funcionais |
| SEG-04 | Policies de inscrição, cancelamento, certificado, proprietário de evento/palestra | Implementado; ConsistencyAndOwnershipTests |
| SEG-05 | Access token até 300 s, corte/desativação local por request, revogação documentada | Implementado no contrato; role no IdP isoladamente tem janela de token |
| SEG-06 | Remoção de seed de credenciais/perfis; bootstrap só em tabela vazia | Implementado; teste de não reelevação |
| SEG-07 | Auditoria CRUD/remoção física/chave composta, ator interno, append-only DML; Keycloak admin events | Parcial: exportar eventos IdP e diffs sanitizados antes/depois de roles |
| SEG-08 | Client PKCE, refresh rotation e roteiro de segurança de frontend | Não implementado no navegador: pedido atual é API; falta sessão/cache/CSP real |
| SEG-09 | Limitador IP pré-JWT + usuário pós-autenticação; body/headers limitados | Implementado; limite global multi-réplica é da borda |
| SEG-10 | Minimização, mascaramento default, sem exceções/SQL brutos, acesso administrativo, consulta pública reduzida | Parcial: ciclo completo de anonimização/eliminação/retenção legal/backups depende da política |
| REL-01 | Retry com contexto novo, lock antes das leituras, receipt e verificação de commit | Implementado; TransactionTests antes da persistência, entre Save/commit e ACK perdido, uma gravação/evento |
| REL-02 | Lock compartilhado entre escritores, capacidade efetiva fail-closed | Implementado; 20 solicitações para 1 vaga geram 1 confirmação |
| REL-03 | Lock + exclusion constraint PostgreSQL para intervalos ativos | Implementado; concorrência, adjacência e violação direta no banco |
| REL-04 | Unicidade pessoa/palestra + idempotência sob lock | Implementado; 12 emissões geram 1 linha/evento e mesmo código |
| REL-05 | Releitura sob lock em transições e invariantes de coleções | Implementado; remoções paralelas preservam último filho, cancelar/iniciar coerentes |
| REL-06 | Escritores coordenados, referências não desaparecem, alterações incompatíveis recusadas, snapshot do certificado | Implementado no exemplo; tráfego de escrita serializado e SQL ad hoc fora do contrato |
| REL-07 | Claim token/renovação/fencing/timeout, consumidor de auditoria atom. idempotente | Implementado; múltiplos claimers, lease vencida, ACK antigo e 16 entregas duplicadas |
| REL-08 | DLQ/next-attempt/replay auditado, contratos v1, consumidor obrigatório e retenção | Implementado; contratos desconhecidos/handler ausente falham; sem ordenação garantida |
| REL-09 | Migração CLI, runtime DML, schema check, restore de 2 bancos, lockfiles, CI/scans/SBOM | Parcial: backup externo/PITR, upgrade de release anterior e RPO/RTO produtivos |
| OBS-01 | Pending/DLQ/oldest/probe-age, sondas fora do callback das métricas | Implementado; regras exercitadas por promtool |
| OBS-02 | Painel Grafana provisionado, HTTP/casos de uso/Outbox/CPU | Implementado e séries locais verificadas; capacidade produtiva ainda deve ser medida |
| OBS-03 | SLIs, SLOs candidatos, alertas com janelas e runbooks | Parcial: aprovar objetivos, configurar contatos/plantão e comprovar notificação |
| OBS-04 | Traceparent Outbox, correlationId validado, logs/traces sanitizados, route templates | Implementado; três sinais chegaram ao stack; novo domínio exige reteste de PII |
| OBS-05 | Backend local persistente; Collector/WAL e exportação TLS autenticada em produção | Parcial: backend, quota, custo e retenção reais não provisionados |
| OBS-06 | Live/ready/processing separados; Keycloak monitorado em rede interna | Implementado local; acrescentar login sintético/HA no ambiente real |
| A12 / Keycloak | PKCE real, issuer/aud/roles/subject, JWKS rotation, indisponibilidade warm/cold | Parcial: MFA/SSO entre aplicações e recuperação do IdP real |
| A13 / corte legado | Novo emissor exclusivo; nenhuma aceitação do JWT antigo | Não há migração de dados do legado; requer exportação/mapeamento/corte/rollback aprovados |
| A14 / históricos | Snapshot reduzido, máscara default, retenção técnica de Outbox/telemetria | Parcial: tratamento ponta a ponta conforme política do produto |
| A15 / entrega | Workflow testes/contratos/template/scans e restore local | Parcial: habilitar branch/environment protection, assinatura/promoção, upgrade real e restore externo |

## Evidências reproduzíveis

- `dotnet test api/ModularApi.slnx`: Unit, Architecture, Integration e Functional.
- `node scripts/test-template.mjs`: CoreProof sem domínio e ExampleProof com domínio, em hive/diretórios temporários, sem segredos nem artefatos exportados.
- `node scripts/smoke-oidc.mjs`: Keycloak 26.7.4 real, Authorization Code + PKCE, acesso à API, autorização, password grant negado e logout.
- `JwksLifecycleTests`: troca de chave e falha de discovery/JWKS com cache quente/frio.
- `DatabasePrivilegesTests`: runtime não tem CREATE nem UPDATE/DELETE na auditoria/replay.
- `DeliveryContractTests`: modelo/migrações consistentes; evento desconhecido vira DLQ no processador; consumidor obrigatório ausente não passa silenciosamente.
- `node scripts/smoke-observability.mjs`: logs no Loki, traces no Tempo, gauges no Prometheus pelo Grafana autenticado.
- `node scripts/validate-infra.mjs`: regras, Collector produtivo e TLS do Caddy com fixture.
- `node scripts/restore-drill.mjs`: restauração isolada de app e identity_provider, conferência de tabelas/contagens e SHA-256.
- `node scripts/check-dependencies.mjs`: auditoria NuGet direta/transitiva.
- Trivy 0.74.0 fixado por digest: imagem Ubuntu/.NET sem HIGH/CRITICAL reportados na consulta local; SBOM em artifacts/security. Isso não equivale a ausência de toda vulnerabilidade ou pentest.

As contagens da entrega inicial estão em `technical-review.md`; a rodada de correções está em `corrections-review.md`. Logs TRX e relatórios locais ficam em api/**/TestResults e artifacts; são excluídos do template.

## Correções da reavaliação de qualidade — 20/09/2026

O relatório histórico não deve ser interpretado como aceite irrestrito. A atualização técnica está em `corrections-review.md`.

| Achado | Mudança | Limite do aceite |
|---|---|---|
| R1 — sonda/entrega acopladas | OutboxProbe independente, uma sequência por módulo, capacidade antes do claim; regressões de atraso/timeout/cancelamento e duas instâncias com PostgreSQL real | Corrigido no código; não garante ordenação global, exatamente uma entrega ou interrupção de handler não cooperativo |
| R2 — descarte de logs com Exception | Evento sanitizado antes dos destinos, fingerprint e contexto permitido; testes de presença do diagnóstico e ausência de payload/segredos | Corrigido; mensagens normais e novas instrumentações continuam sujeitas à política de privacidade |
| R3 — notificação | Destino de alertas solicitado ao responsável | Pendente externo; não foram inventados destinatários nem enviadas mensagens |
| R4 — caminho de telemetria | Coleta interna do Collector, quatro alertas adicionais com casos positivos/negativos; smoke de nova requisição OIDC correlacionada | Verificado local; monitor e regras produtivas/entrega da notificação dependem do backend real |
| R5 — evidência de qualidade | Coverlet, relatório por assembly/arquivo, gates de cobertura total e arquivos críticos; workflow operacional separado, scans semanais | Cobertura mensurada; carga/soak, mutation testing, compatibilidade entre releases e CI remoto ainda não comprovados |
| R6 — recuperação real | Restore lógico incluído no workflow operacional | Não equivale a backup externo, restore funcional completo, upgrade anterior nem RPO/RTO produtivos |

O fluxo de cobertura usa `api/coverage.runsettings`: exclui testes, migrações e código gerado em obj, mantendo host, Shared e módulos de aplicação. Os percentuais não incluem os processos Docker dos smokes. Os gates são pisos iniciais explícitos, não uma certificação de qualidade total; não reduzi-los para acomodar regressões.

## Scalar como documentação padrão — 20/09/2026

- `Scalar.AspNetCore` 2.17.6 substitui `Swashbuckle.AspNetCore.SwaggerUI`; versão centralizada e lockfiles atualizados. O gerador OpenAPI nativo e os contratos JSON/YAML foram preservados.
- `/scalar` é a interface padrão, `/` redireciona para ela e o perfil de execução abre essa rota. A configuração fica em `Shared.WebHost` e acompanha as duas modalidades do template.
- Interface, assets e contratos só são mapeados em `Development` com `OpenApi:Enabled=true`. O Caddy produtivo também bloqueia `/scalar*`; o smoke do host produtivo agora verifica suas rotas e assets.
- Esquema Bearer existente, sem segredo/token pré-preenchido e sem persistência de autenticação. Bundle servido localmente; fontes externas, telemetria do fornecedor e Agent desabilitados. A telemetria da API foi preservada; traces de navegação em `/scalar` e `/openapi` são filtrados.
- Sete casos novos em `ScalarDocumentationTests` verificam redirecionamento, interface/configuração, assets locais e flag desligada. `OpenApiContractTests` verifica o esquema Bearer e a exclusão da UI do contrato; `OidcSecurityTests` verifica a ausência da documentação em produção.
- Validação da solução: **436 testes aprovados** (298 Unit, 42 Architecture, 60 Integration, 36 Functional), sem falhas ou ignorados; TRX em `artifacts/tests/scalar/full`. Restore em locked mode e auditoria NuGet de `Shared.WebHost` aprovados, sem vulnerabilidades reportadas pelas fontes consultadas.
- `node scripts/test-template.mjs`: geração genérica com **122 testes aprovados**, cobertura de **83,84% de linhas / 62,93% de branches**; geração com exemplo com **436 testes aprovados**, cobertura de **79,44% / 61,63%**. Gates atendidos nas duas modalidades. Evidências desta rodada em `/tmp/modular-api-template-test-ZuPEGe` e `/tmp/modular-api-scalar-template.log` (temporários locais).
- Configuração produtiva e infraestrutura validadas localmente com fixtures. Esta alteração não executou deploy, login interativo pelo Scalar nem o smoke Docker completo do host produtivo; esses limites não são substituídos pelos testes HTTP em TestServer.

## Padronização para inglês e guia de instalação — 20/09/2026

- Projetos/módulos, namespaces, classes, arquivos, identificadores, rotas, JSON, enums, roles/policies, schemas/tabelas/colunas, tags SQL e contratos de integração padronizados em inglês. Núcleo: `Identity`/`Audit`; exemplo: `Venues`/`People`/`Events`/`Talks`.
- Mensagens da aplicação, descrições do OpenAPI, comentários/XML e documentos atuais preservados em pt-BR. FluentValidation usa `pt-BR` explicitamente, sem alterar formatos de números/datas. Propriedades estruturadas e códigos continuam técnicos em inglês. Arquivos históricos de origem estão sinalizados como não vigentes.
- A revisão técnica tratou a mudança de nomes persistidos como incompatível: base nova, sem aliases implícitos para clientes/tokens/eventos antigos. `MigrationBaselineGuard` bloqueia históricos EF em schemas não registrados antes de aplicar migrações, sob o lock do migrador. Não constitui migração de dados legados nem protege SQL/EF executado fora do migrador. `.env`, `.local`, volumes e bancos já existentes foram preservados.
- Nove novos casos: sete em `LanguageConventionTests` (JSON, paginação, roles, Problem Details, idioma sob cultura `en-US`, OpenAPI e mensagens de middleware) e dois em `MigrationBaselineTests` (bloqueio antes de criar schemas e migração idempotente de base nova). Mantidas as suítes de autorização, concorrência, privacidade e Outbox.
- Solução atual: **445 testes aprovados**, sem falhas/ignorados: Unit 298, Architecture 42, Integration 69, Functional 36. TRX da rodada final em `artifacts/tests/english`, execuções de 11:06:57, 11:07:05 e 11:07:24; resultados de iterações anteriores também permanecem nesse diretório.
- Geração genérica: **131 testes aprovados** (61/14/49/7), cobertura **83,60% de linhas / 61,93% de branches**. Geração com exemplo: **445 testes aprovados**, cobertura **79,30% / 61,01%**. Gates atendidos sem redução dos pisos. Resultados completos em `/tmp/modular-api-template-test-1WTTvR`, log `/tmp/modular-api-english-OABm7T/template-validation.log`.
- Restore em locked mode, auditoria NuGet direta/transitiva e validações de infraestrutura/manifesto produtivo com fixtures aprovados. A auditoria não reportou vulnerabilidades nas fontes consultadas. Build/publish continuam sujeitos aos avisos dos analisadores e à chamada obsoleta de configuração explícita do Scalar; não foi declarado build sem warnings nem novo scan de imagem nesta rodada.
- Ensaio adicional em projeto genérico recém-gerado `LanguageProofOABm7T`, diretório `/tmp/modular-api-english-OABm7T/LanguageProof`, nome Compose exclusivo `languageproofoabm7t-local`: imagem compilada, migração/bootstrap novos, readiness, login Keycloak real com PKCE/role `Administrator`, nova requisição correlacionada em Tempo/Loki/Prometheus e smoke da imagem em `Production` aprovados. Não reutilizou credenciais nem volumes existentes. O container de bootstrap emitiu aviso de biblioteca Kerberos ausente, mas conexão por senha e provisionamento concluíram; autenticação Kerberos não foi testada. Logs de resultado em `/tmp/modular-api-english-OABm7T/*-validation.log`.
- Ao terminar, somente a stack temporária desse ensaio foi encerrada com `down`, sem `-v`: containers/rede de teste removidos, seis volumes preservados para inspeção. As stacks e os volumes anteriores do usuário não foram alterados.
- Guia completo em `docs/installation-guide.md`, com cópia de conveniência em `/home/thiago-botelho/Downloads/guia-instalacao-modular-api-template.md`. Convenções e limites de compatibilidade em `docs/language-conventions.md`; instruções futuras também registradas em `AGENTS.md`.
- Não houve execução remota do CI, alteração de IdP produtivo, migração de banco legado nem deploy produtivo. Os aceites externos abaixo permanecem abertos.

## Guias de engenharia e build sem avisos — 20/09/2026

- `CLAUDE.md` na raiz passa a ser o contrato de trabalho lido em toda sessão: mapa do repositório, invariantes, anatomia de um caso de uso, armadilhas da base e checklist. Dois guias novos o apoiam: `docs/dotnet-practices.md` (código do dia a dia) e `docs/architecture-practices.md` (onde uma regra mora e quando um padrão se justifica). Ambos ligados no `README.md` e no `AGENTS.md` e exportados pelo template.
- `Module.Identity/UseCases/CreateSessao` e `UseCases/UpdateRolesUser` removidos: diretórios vazios, um deles com nome remanescente em português, sem referência em código, solução ou documentação.
- `NoWarn` de `api/Directory.Build.props` reduzido de 37 para 10 códigos, agrupados com o motivo declarado ao lado. Conferido com `dotnet build ModularApi.slnx -t:Rebuild -p:NoWarn=`: os 28 códigos removidos não produziam nenhum aviso. `CA1873` foi acrescentado deliberadamente junto de `CA1848`, por ser a mesma escolha de log estruturado legível.
- Regras de estilo em migrações geradas pelo EF Core (`IDE0005`, `IDE0161`, 59 avisos) silenciadas por escopo em `api/.editorconfig` (`**/Migrations/*.cs`), não globalmente.
- Avisos reais corrigidos no código: 38 `using` desnecessários via `dotnet format analyzers --diagnostics IDE0005`, 4 `CS8604`, 2 `CA1725`, 2 `CA1869`, 1 `xUnit1031`, 1 `CA1859` e 1 `CA1512`.
- Duas supressões locais, com justificativa no ponto de uso: `EF1002` em `RuntimeDatabasePrivileges` (GRANT/REVOKE não aceitam parâmetro para identificador; schema, tabela e role passam por `QuoteIdentifier`) e `CS0618` na composição do Scalar (o substituto `EnablePersistentAuthentication()` só sabe ligar, e `ScalarDocumentationTests` exige `"persistAuth":false` no HTML publicado). Isso encerra a pendência registrada na seção anterior sobre avisos de analisadores e chamada obsoleta do Scalar.
- Defeito corrigido em `DeleteVenueUseCase`: `ITalksModuleApi` estava injetado e não utilizado. Como `IsVenueInUseAsync` considera apenas eventos ativos e a exclusão de um evento não desativa suas palestras, um local cuja sala ainda era ocupada por palestra ativa de evento excluído podia ser removido junto de suas salas. O caso de uso passa a verificar cada sala com `IsRoomInUseAsync` e devolve `409 Venues.ResourceInUse`; o endpoint passa a documentar a resposta 409.
- Regressão coberta por `Venue_cannot_be_removed_while_a_talk_of_a_deleted_event_still_uses_its_room`, verificada nos dois sentidos: reprova com a checagem desativada e aprova com ela.
- Build da solução: **0 avisos e 0 erros** em `dotnet build ModularApi.slnx -t:Rebuild`, contra 315 avisos antes desta rodada. Solução: **446 testes aprovados** (298 Unit, 42 Architecture, 70 Integration, 36 Functional), sem falhas ou ignorados.
- `node scripts/test-template.mjs`: geração genérica com **131 testes aprovados**, cobertura **83,60% de linhas / 62,28% de branches**; geração com exemplo com **446 testes aprovados**, cobertura **79,61% / 61,35%**. Gates atendidos nas duas modalidades. Diretório isolado da rodada: `/tmp/modular-api-template-test-xNtVaJ`.
- `node scripts/check-dependencies.mjs` e `node scripts/validate-infra.mjs` aprovados. Não foram executados CI remoto, deploy, smoke Docker do host produtivo, ensaio de restore nem alteração de IdP. Os aceites externos abaixo permanecem abertos.

## Aceites que não devem ser “marcados como feitos” automaticamente

Antes de produção: modelo de organização/roles aprovado; frontend seguro se existir; MFA e administração IdP; trilha de alteração de perfis; retenção/tratamento de dados; migração de identidade legada se necessária; backup externo/restore medido; certificado/segredos rotacionáveis; carga/SLO; contato real de alertas; segurança e aprovação de ambiente.

Criar automação destrutiva de retenção, usuários de um IdP real ou publicação em cloud sem essas informações seria ultrapassar o escopo. A base técnica está preparada para esses passos, mas o aceite externo permanece explícito.
