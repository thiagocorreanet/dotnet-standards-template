# Revisão técnica da entrega .NET

Data: 20/09/2026. Revisão do template de origem; resultados não certificam alterações futuras de projetos gerados.

Registro histórico da entrega inicial. As correções da reavaliação (incluindo substituição do descarte de exceções por sanitização) e os resultados mais recentes estão em [corrections-review.md](corrections-review.md).

## 1. CONTEXTO ANALISADO

Implementação solicitada a partir da análise arquitetural existente, com exigência adicional de reutilização. .NET 10, ASP.NET Core, EF Core/Npgsql, Keycloak, Outbox, auditoria e OpenTelemetry. Foram revisados host, contratos, módulos, migrations, decorators, consumidor, configurações, scripts e testes. A revisão seguiu a skill dotnet-tech-lead-reviewer.

## 2. VEREDITO

**APROVADO COM RESSALVAS** para integração e uso como base de desenvolvimento. Isso **não aprova implantação produtiva** sem os aceites externos registrados na matriz.

## 3. BLOQUEANTES

Não restou falha funcional bloqueante identificada nos cenários locais executados. Uma release produtiva permanece condicionada a configuração real do IdP, trilha administrativa, retenção, proteção do ambiente, backup externo, carga e recebimento de alertas.

## 4. PROBLEMAS ENCONTRADOS

Correções incorporadas durante implementação/revisão:

| Severidade original | Local | Problema/consequência | Correção/evidência |
|---|---|---|---|
| HIGH | Transações e casos de uso | Retry sobre contexto já modificado poderia perder escrita/Outbox | Escopo novo, unidade completa e receipt; falhas Save/commit/ACK exercitadas |
| HIGH | Policies dos módulos | Papel global não prova titularidade | Autorização de recurso dentro do comando e testes entre identidades |
| HIGH | OutboxStore/Processor | Consumidor antigo poderia confirmar claim de outro | Token, renovação, fencing, DLQ; testes de concorrência |
| HIGH | Program healthcheck / produção | Host header localhost não era permitido com domínio produtivo | Healthcheck usa AllowedHosts; imagem em Production testada |
| HIGH | Realm de demonstração | Ausência do scope basic impedia subject utilizável no access token real | Scope corrigido e smoke OIDC real aprovado |
| MEDIUM | Observabilidade | SQL/Exception/RequestPath poderiam sair por sinks automáticos | Redação antes dos sinks, exclusão de exceptions/EF bruto, Collector sanitizador |
| MEDIUM | Painel/alertas | Health afetaria SLIs de API e CPU usava instrumento removido | Filtro de rotas API e instrumento estável do runtime |

Ressalvas objetivas atuais:

- MEDIUM — `[Command("event-management-example")]`: a trava ampla reduz throughput de escrita. Aceita para exemplo de correção; medir antes de subdividir. Não bloqueia uso do núcleo genérico.
- MEDIUM — `ModuleDiscovery`: descoberta de DLLs exige publicação limpa ao remover módulo. Procedimento registrado; geração sem exemplo exclui os módulos fisicamente e foi testada.
- MEDIUM — `InProcessIntegrationEventPublisher`: entrega pelo menos uma vez, handlers de uma mensagem compartilham escopo, não há ordem por agregado. Consumidores futuros não podem assumir execução única.
- LOW — analisadores de estilo/recomendações ainda emitem avisos no conjunto herdado/adaptado. Warnings de vulnerabilidade NuGet bloqueiam; não se declarou build sem warnings.

## 5. CLEAN CODE

Fronteiras HTTP/caso de uso/domínio mantidas. Sem repositório genérico ou camadas repetidas sem necessidade. Respostas e contratos explícitos. Há verbosidade de logging em módulos demonstrativos que pode ser reduzida sem alterar semântica.

## 6. .NET

Async/CancellationToken em I/O, validação centralizada, ProblemDetails sem detalhes internos, opções validadas, DbContext scoped. SDK e versões diretas fixadas, lockfiles e audit de dependências transitivas.

## 7. ARQUITETURA

Testes arquiteturais aprovados; não há referência direta Module→Module. O núcleo compilou e executou sem Locais/Pessoas/Eventos/Palestras. Regras de negócio do exemplo não foram movidas para infraestrutura compartilhada.

## 8. SEGURANÇA

Validadores JWT reais, subject opaco, roles de client, proteção de claims internas, vínculo ativo e revogação local. IDOR, issuer/audience/assinatura/tipo/expiração, CORS, rate limit e OpenAPI produtivo testados. PKCE real e logout testados no Keycloak local. Testes de rotação JWKS e indisponibilidade warm/cold aprovados.

MFA produtivo, SSO entre aplicações, frontend e exportação/diff de alterações de roles permanecem aceites externos; não há afirmação de pentest ou conformidade.

## 9. DADOS

Migrações aplicadas e modelo sem alterações pendentes; runtime DML sem DDL nem mutação da auditoria. Constraints e coordenação protegem invariantes do exemplo. Restore lógico de app e identity_provider conferiu tabelas/contagens/checksums em container isolado. Não foi ensaiado upgrade do antigo banco Identity nem DR externo.

## 10. PERFORMANCE

Consultas paginadas/AsNoTracking nas leituras, índices de unicidade/fila e coordenação no banco. Sem promessa de capacidade baseada em testes funcionais. Lock amplo, múltiplos DbContexts e sondas devem ser medidos com carga real; não se acrescentou cache de autorização que atrasaria desativação local.

## 11. TESTES

| Conjunto | Unit | Architecture | Integration | Functional | Total |
|---|---:|---:|---:|---:|---:|
| Template completo | 264 | 42 | 52 | 36 | 394 |
| Projeto gerado CoreProof, sem exemplo | 27 | 14 | 32 | 7 | 80 |
| Projeto gerado ExampleProof, com exemplo | 264 | 42 | 52 | 36 | 394 |

Sem falhas/skip nas execuções registradas. PostgreSQL real via Testcontainers. Falhas foram injetadas antes de persistir, entre Save/commit e após commit confirmado sem ACK ao chamador.

Um projeto CoreProof também foi iniciado do zero em Docker, com banco e realm novos: migração, grants, bootstrap consultando o subject efetivo e PKCE real passaram. Isso verifica a reutilização além da compilação.

Também aprovados: PKCE Keycloak, três sinais de telemetria, host de imagem Production, promtool, validação Collector/Caddy, restore isolado, restore NuGet locked e auditoria NuGet. Scan Trivy do runtime final: nenhuma HIGH/CRITICAL reportada na base consultada; SBOM gerado. Scan de segredos do projeto genérico gerado sem achados. Scans são datados e não garantem ausência absoluta de vulnerabilidades.

Artefatos: api/**/TestResults/*.trx e artifacts/security/*.json. O pipeline foi criado e seus comandos centrais executados localmente; **não foi disparado em um repositório GitHub remoto**.

## 12. REFATORAÇÃO RECOMENDADA

Somente após driver concreto: particionar locks do domínio, separar consumidor pesado em worker e fortalecer descoberta explicitamente declarada se houver hot removal de módulos. Para integração com efeito externo, adicionar idempotency key/inbox específica. Não são abstrações obrigatórias para toda API.

## 13. RISCOS

Dependências externas/infra produtiva não provisionadas; retenção legal ainda não definida; browser não implementado; auditoria do IdP não é a auditoria do banco da API; local single-host não entrega HA; backup lógico não substitui PITR e cópia externa; warnings de estilo remanescentes; catálogo de CVEs muda.

## 14. DECISÃO FINAL

Base reutilizável apta a receber um novo domínio, com implementação e evidências locais dos controles técnicos descritos. Aprovação produtiva deve usar os itens parciais/externos de implementation-status.md e o checklist de runbooks.md. Não declarar “todas as lacunas fechadas” enquanto esses aceites não existirem.
