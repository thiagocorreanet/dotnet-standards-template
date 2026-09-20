# Operação e recuperação

## Verificação local

`smoke-oidc.mjs` executa login real Authorization Code + PKCE, valida acesso à API, role do client, password grant desabilitado e logout. Não imprime nem grava token. Requer conta local provisionada.

`smoke-observability.mjs` executa o fluxo OIDC local e uma nova requisição autenticada, com traceparent/correlationId únicos. Pelo Grafana autenticado, exige aquele trace no Tempo, correlação em metadados do Loki, avanço do contador HTTP da rota, sondas recentes e coleta interna do Collector. Verifica ausência da sentinela sintética de query. Aguarda até 120 s para exportação/scrape; dados antigos sozinhos não bastam. Não imprime nem grava tokens.

`wait-local.mjs` aguarda readiness de API/Keycloak/Grafana/Prometheus. O workflow `operational-release-checks` executa esses smokes, smoke do host em Production e restore lógico em runner isolado, por disparo manual, tag v* ou semanalmente. Não realiza deploy nem envia notificações externas. Não subir essa fixture de desenvolvimento sobre infraestrutura produtiva.

`validate-infra.mjs` exercita alertas com promtool e valida Collector produtivo/Caddy com segredos e certificado sintéticos. Isso não verifica certificado/DNS/backend de produção.

## Health e alertas

- /health/live: processo responde; não consulta dependências.
- /health/ready: PostgreSQL disponível. Migrações pendentes impedem startup.
- /health/processing: administrador; degrada quando há DLQ, faltam sondas ou alguma sonda passa de 30 s.
- Métricas do Keycloak usam a porta de gerenciamento **interna**. Produção deve coletar também login/token sintético, expiração TLS, DB e capacidade do provedor.

As regras locais contemplam erro HTTP, latência, backlog antigo, DLQ, sonda obsoleta, telemetria ausente, indisponibilidade Keycloak e disponibilidade/fila/falhas/recusas do Collector. Os SLIs HTTP filtram rotas de API, excluindo health/docs. A porta 8888 é privada; em produção configure sua coleta por monitor independente e importe as regras no backend escolhido.

SLOs **candidatos**, ainda não aprovados: sucesso HTTP sem 5xx ≥99,9%/30 dias; p95 abaixo de 500 ms; processamento Outbox em até 60 s sob carga nominal. 4xx de autenticação/validação não são falha técnica, mas devem ser acompanhados separadamente. Defina população elegível, janelas, tráfego mínimo e tratamento de ausência de dados com o produto.

O painel `Modular API — operação` contém tráfego, p95, 5xx, Outbox pendente/DLQ/idade/sonda, entrega p95, casos de uso, CPU e coleta. Em múltiplas réplicas, gauges de backlog consultam a mesma fila: não some esses valores como se fossem filas distintas; agrupe/deduplique por serviço/módulo.

**Roteamento ainda é externo:** configure Alertmanager ou regras/contatos na plataforma escolhida, troque runbook_url por URL acessível à equipe, defina plantão/escalonamento e provoque um alerta de teste. Promtool provou a regra; não prova que alguém recebeu uma notificação.

## Erros HTTP

1. Correlacione horário, rota, status, versão, traceId e operationId.
2. Consulte taxa 5xx e latência; separe erro de banco, timeout de lock e resultado indeterminado.
3. Para commit indeterminado, consulte o recurso e o receipt pela operação com acesso operacional autorizado. Não reenvie indiscriminadamente um comando com efeito externo.
4. Confira última implantação e migrações. Rollback de imagem só é seguro se o schema continuar compatível.
5. Não habilite Include Error Detail, parâmetros SQL nem log de token para diagnosticar. Reproduza com dados sintéticos.

## Latência

Verifique saturação CPU/memória, conexões PostgreSQL, tempo de lock, consultas e destinos de telemetria. O exemplo serializa escritas por uma chave ampla; trate contenção observada antes de acrescentar réplicas.

Teste de lotação concorrente é evidência de correção, não benchmark de capacidade. Antes de aprovar SLO, execute carga representativa, mistura de leituras/escritas, volume de banco e múltiplas réplicas; registre taxa, p95/p99, erros e saturação. Estabeleça limites por tenant/cliente quando o produto os exigir.

## Outbox

1. Consulte GET /api/v1/operations/outbox em rede administrativa.
2. Inspecione GET /api/v1/operations/outbox/{module}/dead-letters. A resposta omite payload e contém erro classificado.
3. Corrija contrato, consumidor, configuração ou dependência. Evento obrigatório sem consumidor e contrato desconhecido não são sucesso.
4. Confirme idempotência antes de replay. Envie POST /api/v1/operations/outbox/{module}/{id}/replay com `{"reasonCode":"INC-123"}` e token de administrador.
5. 204 indica reabertura; não indica efeito já entregue. Acompanhe processamento/erro, receipt de replay e contagem de auditoria. 409 indica estado não elegível/concorrrência.
6. Não atualize lease/Attempts direto no banco para forçar entrega. ACK antigo deve continuar sendo rejeitado.

Padrões: polling 1 s, até 20 mensagens por ciclo/módulo, uma entrega sequencial por módulo e até 4 entregas simultâneas por processo (`MaxConcurrentDeliveries`, 1–32). Capacidade vem antes do claim. Lease 60 s, renovação periódica, timeout de handler 120 s, até 10 tentativas, backoff com jitter. Sonda independente por módulo: intervalo 5 s e timeout 5 s (`ProbeIntervalSeconds`/`ProbeTimeoutSeconds`, ambos 1–10). Falha não atualiza o timestamp do snapshot. Retenção horária remove até 1000 processadas/receipts antigos por módulo/ciclo; mínimo 7 dias. Backlog de retenção em alto volume exige ajuste medido.

Não desligue a API por uma falha transitória de processamento: health de processamento é separado de readiness/liveness. Se auditoria obrigatória acumular sem resolução, a operação deve decidir pausar escritas; esse bloqueio global automático não está implementado.

## Keycloak

- Com JWKS em cache, tokens conhecidos válidos podem continuar sendo aceitos durante indisponibilidade do provedor; vínculo ativo local e expiração continuam obrigatórios.
- Sem discovery/JWKS inicial ou com chave desconhecida indisponível, a autenticação é negada; não há modo bypass.
- Rotação: publique chave nova com sobreposição da antiga por pelo menos validade de tokens + skew/cache; teste token com novo kid antes de remover antiga.
- MFA, SSO entre aplicações, logout federado, SMTP, certificados, backup e HA devem ser exercitados no realm produtivo. Testes automatizados de JWT não substituem esse ensaio.
- Erro 401: conferir issuer/audience/exp/iat/typ/sub e vínculo, sem colar token em ferramenta pública. Uma troca de realm modifica issuer e requer migração explícita de vínculos.

## Telemetria

Verifique conexão API→Collector, limite de memória, rejeições, tamanho da fila persistente e backend. O Collector mantém WAL e retry limitado; isso não é armazenamento infinito. Sob indisponibilidade longa ou disco cheio, pode haver perda de telemetria.

Os sinks removem campos conhecidos. Eventos com exceção permanecem, sanitizados com `ExceptionType`, `ErrorCode`, `ErrorFingerprint` e contexto técnico permitido. O objeto Exception e o template/payload originais não chegam aos destinos. Use fingerprint + versão + trace para agrupar falhas; reproduza com dados sintéticos para obter detalhes adicionais. Níveis/overrides do logger de entrada são carregados no startup; reinicie a aplicação após sua alteração. Não use mensagens normais livres contendo dados pessoais. Acrescentar uma nova instrumentação exige teste de privacidade novamente; a sentinela do smoke não certifica todos os novos campos.

Loki/Tempo/Prometheus locais persistem em volumes. Produção usa backend externo autenticado/TLS e diretório de WAL persistente; defina quota, retenção, orçamento e acesso por equipe.

## Backup e restore

Ensaio **apenas do ambiente local**:

```bash
# Evite novas escritas enquanto o ensaio compara contagens.
node scripts/restore-drill.mjs
```

Cria dumps de app/identity_provider, restaura em container PostgreSQL isolado sem rede, compara tabelas/contagens e produz manifest com checksums. Apaga somente o container temporário criado pelo próprio script. Não altera bancos nem apaga volumes de origem. Se ocorrer escrita concorrente, a comparação pode falhar; repita em janela sem escrita.

Dumps permanecem em artifacts/backups, diretório privado e arquivos 0600, **sem criptografia**. São locais, excluídos de Git/template. Trate o backup de Keycloak como segredo de alta sensibilidade.

Para produção, além do ensaio lógico: definir RPO/RTO, PITR/WAL se necessário, criptografia/KMS, cópia externa, papéis/ACL, chaves de assinatura e configuração IdP, certificados, objetos externos, retenção e observabilidade. Restaurar em ambiente segregado, testar login, autorização, referências entre módulos, receipts, Outbox e auditoria; medir recuperação. Não religar consumidores contra destinos reais sem verificar efeitos já aplicados. Dumps separados dos dois bancos não constituem snapshot distribuído.

## Produção: sequência de aceite

1. Crie banco **novo**, usuário migrador dono dos schemas e usuário runtime sem CREATE. Prepare PostgreSQL suportado com btree_gist se incluir exemplo. Não use credencial superuser na API.
2. Provisione Keycloak externo com HTTPS e banco próprio, realize MFA/SSO/rotação e exportação de eventos administrativos. Configure audience/roles/origens do produto.
3. Gere e escaneie imagem, fixe digest de API/Caddy/Collector. Exemplo de Caddy validado: 2.11.2-alpine; Collector contrib 0.161.0. Revise atualizações e advisories a cada promoção.
4. Copie infra/production/environment.example para arquivo privado .env.production e substitua **todos** os placeholders. Forneça arquivos de segredo por gerenciador; runtime e migrador distintos, SSL Mode=VerifyFull no PostgreSQL externo.
5. Garanta leitura dos secrets pelo UID do container, mantendo a pasta de origem restrita. API usa UID 1654, Collector 10001 e Caddy 1000. No Compose standalone, chmod/uid declarados para secrets podem não substituir permissões do bind mount: teste sem imprimir conteúdo.
6. Crie diretório WAL do Collector persistente, proprietário 10001. Forneça certificado/chave TLS válidos para API_DOMAIN; configure renovação e alerta de expiração. A configuração Caddy usa TLS fornecido, não ACME automático.
7. Execute `node scripts/validate-production.mjs`. O modo --fixture não lê secrets nem valida ambiente real.
8. Execute exclusivamente `docker compose --env-file .env.production -f compose.production.yaml run --rm migrate` em janela controlada. Guarde versão/resultado; não rode automaticamente antes de uma aprovação de mudança de schema.
9. Depois, inicie `docker compose --env-file .env.production -f compose.production.yaml up -d`. A dependência pode executar novamente o job migrador, que é idempotente. Não combine com compose.local.yaml. Provisione o primeiro vínculo pela CLI em rede privada, sem publicar endpoints operacionais na borda.
10. Verifique TLS, somente 443 publicada, usuário não root, health, CORS, headers, negação de API sem token, propriedade, rotação de segredo, telemetria e entrega de alerta.
11. Execute ensaio de upgrade/rollback compatível, restore, carga e segurança com dados sintéticos. Aprove política de dados e seus procedimentos de tratamento.
12. Restrinja permissões de deploy, retenha SBOM/resultados, defina responsável e janela de atualização.

Este manifesto descreve uma implantação de API em host único usando dependências externas. Não entrega HA do host, cluster Keycloak/PostgreSQL ou gestão de certificados/segredos como serviço.
