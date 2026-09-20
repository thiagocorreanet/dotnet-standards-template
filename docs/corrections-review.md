# Correções da reavaliação — API .NET

Data: 20/09/2026. Projeto: modular-api-template.

Registro da base de origem; não certifica alterações futuras em projetos gerados.

## 1. CONTEXTO ANALISADO

Implementadas as correções dos achados reproduzidos R1/R2 e melhorias locais de R4/R5. Mantidos monolito modular .NET 10, módulos de exemplo opcionais, EF Core, PostgreSQL e Keycloak. Nenhum schema ou contrato HTTP/evento foi alterado. As skills de arquitetura .NET e revisão técnica orientaram uma correção localizada, com regressões, sem criar novos serviços distribuídos.

## 2. VEREDITO

**APROVADO COM RESSALVAS** para integrar estas correções. Não é aprovação irrestrita do ambiente produtivo nem encerramento de todos os itens da reavaliação.

## 3. BLOQUEANTES

Nenhum bloqueante identificado nas mudanças locais verificadas. A liberação produtiva continua dependendo de notificações efetivas, segurança/identidade operacional, carga, recuperação e objetivos aprovados. O destino dos alertas foi solicitado; nenhuma mensagem externa foi enviada.

## 4. PROBLEMAS ENCONTRADOS E TRATADOS

| Achado | Tratamento | Estado |
|---|---|---|
| R1 — Outbox bloqueava outros módulos e a sonda | Sequência por módulo, limite global de entregas adquirido antes do claim e OutboxProbe independente por módulo, com timeout | Corrigido e testado |
| R2 — logs com Exception eram descartados | Evento sanitizado preserva tipo, fingerprint, código/contexto permitido, nível, horário e trace/span; remove objeto Exception, mensagem/template/payload originais | Corrigido e testado |
| R3 — alerta sem destinatário comprovado | Mantido explícito o requisito de destino/plantão/escalonamento | Pendente externo |
| R4 — caminho da telemetria insuficientemente verificado | Métricas internas do Collector; alertas de indisponibilidade/fila/falha/recusa; smoke com nova requisição OIDC, correlação e sentinela | Verificado local; configuração operacional produtiva ainda pendente |
| R5 — cobertura e gates incompletos | Coverlet + consolidação por linha/branch + pisos no CI; workflow operacional e auditorias semanais | Parcial: carga/soak, mutation testing, compatibilidade entre releases e execução remota continuam pendentes |
| R6 — restore local não comprovava DR | Restore lógico integrado ao workflow e repetido localmente | Parcial: não comprova restore funcional completo, upgrade anterior, backup externo ou RPO/RTO |

## 5. CLEAN CODE

Separadas responsabilidades de entrega, sondagem e sanitização. Mantidas as convenções existentes. Não houve refatoração geral nem supressão nova de warnings para fazer a entrega passar. O build ainda apresenta warnings preexistentes de análise; não foi declarado “zero warnings”.

## 6. .NET

BackgroundServices no mesmo host, cancelamento propagado, SemaphoreSlim para capacidade antes do claim e configuração validada. Defaults novos: MaxConcurrentDeliveries=4, ProbeIntervalSeconds=5 e ProbeTimeoutSeconds=5. Sem pacote de produção adicional; Coverlet 10.0.1 é privado aos projetos de testes, com lockfiles atualizados.

## 7. ARQUITETURA

Fluxo de escrita/transação permanece inalterado. Entrega: capacidade disponível → claim → handler com escopo DI → ACK condicionado ao token/lease. Sonda: consulta periódica independente → snapshot em memória → métricas/health. A coleta de métricas continua sem consultar o banco no callback.

Uma entrega por módulo e concorrência global limitada por processo; não há nova garantia de ordem entre réplicas. Broker, microserviços, novas camadas e migração de framework foram rejeitados por não resolverem a necessidade desta correção. Se futuramente houver necessidade de isolamento de recursos, a separação em worker poderá ser avaliada com medições.

## 8. SEGURANÇA

Testes comprovam presença do diagnóstico e ausência de dados sensíveis na exceção, inner exception, Data, template e propriedades arbitrárias. Logs normais mantêm a política de minimização e não permitem texto livre com PII por convenção. A correlação não recebe rótulo de métrica de alta cardinalidade.

O smoke autenticado real verificou PKCE, autorização da API, password grant negado e logout. A sentinela de query não apareceu no trace/log da requisição. Isso não substitui MFA/SSO/administração/rotação/pentest do ambiente real. A auditoria NuGet desta rodada não reportou vulnerabilidades diretas/transitivas na fonte consultada; não foi repetido um scan completo da imagem nesta rodada.

## 9. DADOS

Nenhuma migração nova, remoção de dados da aplicação ou alteração de privilégios. Fencing/renewal, transação, receipt e idempotência da auditoria permanecem. O teste novo usa duas instâncias de host contra PostgreSQL real e comprova renovação da mesma concessão, uma tentativa/entrega no cenário e snapshots recentes enquanto o handler aguarda.

O teste não transforma at-least-once em exactly-once. Reenvio HTTP por cliente ainda exige idempotência específica nas operações que precisem dessa garantia.

## 10. PERFORMANCE

A lentidão de um módulo não ocupa a única sequência de todos os demais. Existe limite global configurável; todas as vagas ocupadas continuam impondo espera deliberada. Handler que ignora cancelamento pode continuar executando após o timeout, portanto o limite não interrompe código não cooperativo. Carga/soak e dimensionamento continuam pendentes; testes de concorrência comprovam cenários de correção, não capacidade produtiva.

## 11. TESTES E EVIDÊNCIAS

| Execução | Unitários | Arquitetura | Integração | Funcionais | Total |
|---|---:|---:|---:|---:|---:|
| Projeto completo | 298 | 42 | 53 | 36 | 429 |
| Template gerado sem exemplo | 61 | 14 | 33 | 7 | 115 |
| Template gerado com exemplo | 298 | 42 | 53 | 36 | 429 |

Zero falhas e zero testes ignorados nas três execuções. São 35 testes novos: sete de isolamento/timeout/cancelamento/logging, um com dois hosts e PostgreSQL real e 27 de contrato HTTP, validação e configuração OIDC. O núcleo precisou destes testes próprios para satisfazer o gate sem depender do exemplo; os pisos não foram reduzidos.

O diagnóstico isolado original também foi repetido: com handler ainda aguardando aos 32 s, snapshot com 3,1 s de idade (antes: 33,1 s), 32 consultas ao outro módulo (antes: zero), lease renovada e condição de health sem 503 por sonda antiga. O log com exceção chegou sanitizado ao sink. É um teste controlado, não um benchmark.

Cobertura consolidada da solução completa: **79,42% das linhas e 61,41% das branches**. No projeto genérico gerado sem exemplo: **83,79% e 62,64%**, respectivamente. Excluídos testes, migrações e fontes geradas em obj; host, bibliotecas e módulos de aplicação permanecem no denominador. Processos dos smokes Docker não entram nessa medição.

| Arquivo crítico | Linhas | Branches |
|---|---:|---:|
| OutboxProcessor | 86,79% | 89,47% |
| OutboxProbe | 100% | 75% |
| PrivacyLogSink | 100% | 71,79% |
| OidcAuthenticationExtensions | 98,59% | 90,54% |

Gates explícitos em api/coverage-policy.json: total 70%/60% e mínimos específicos por arquivo crítico. São pisos iniciais, não promessa de cobertura integral. Relatórios estão em artifacts/coverage/corrections-verified, incluindo summary.json, Cobertura, JSON e TRX. O script test-template.mjs também exige cobertura nos dois modos gerados.

Também passaram: validação de regras Prometheus com cenários positivos/negativos novos, configurações Collector local/produtivo e Caddy, validação produtiva estática, imagem final com ambiente Production, smoke OIDC/telemetria nova e restore lógico de aplicação/Keycloak. A nomenclatura das métricas de fila foi conferida no Collector em execução. Os workflows foram configurados e seu YAML validado; não foram executados remotamente no GitHub nesta sessão.

## 12. REFATORAÇÃO RECOMENDADA

Não é necessária outra refatoração estrutural para aceitar estas correções. Próximas entregas devem priorizar destino real de alertas, matriz completa de identidade, testes de carga/estabilidade, recuperação funcional e compatibilidade de releases. Consultar docs/quality-gates.md e docs/runbooks.md para reprodução e operação.

## 13. RISCOS

Amostragem, custos e retenção de telemetria exigem validação do backend produtivo. Os níveis/overrides do logger de entrada são carregados no startup. Novos módulos/consumidores precisam respeitar cancelamento, idempotência e privacidade. Duas instâncias de teste não equivalem a duas máquinas ou partição de rede. Snapshot fresco comprova a sonda; backlog/DLQ continuam sendo sinais separados de sucesso da entrega.

Os backups locais são privados e não criptografados; não foram enviados externamente. Restore lógico compara contagens e checksum do dump, não valida integralmente login pós-restore ou objetivos de desastre. Não foi alterado o projeto original ties-gestao-eventos-main.

## 14. DECISÃO FINAL

As duas falhas reproduzidas foram corrigidas e receberam regressões. A base genérica continua reutilizável; observabilidade e entrega ganharam verificações adicionais. Os itens externos e de qualificação operacional permanecem explicitamente parciais, sem declarar todas as lacunas encerradas.
