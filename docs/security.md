# Segurança, identidade e privacidade

## Contrato Keycloak

`infra/keycloak/realm.json` é referência de configuração, não um realm pronto para qualquer organização.

- Client de recurso: modular-api; audience obrigatória nesse valor.
- Client navegador: modular-web, público, Authorization Code + PKCE S256. Sem implicit flow, password grant ou service account.
- Scopes básicos incluem **basic**, profile, email e roles. Sem basic, versões recentes do Keycloak podem não emitir o subject esperado no access token.
- Redirect e webOrigin exatos. Substitua localhost por URLs HTTPS antes de provisionar produção; nunca aceite wildcard.
- Access token 300 s, refresh rotation, proteção contra força bruta e eventos administrativos habilitados.
- Realm de referência exige verificação de e-mail e cadastro TOTP. O gerador **local** remove essas exigências somente para o smoke automatizado. Configuração de SMTP, política de senha, MFA do fluxo/browser e MFA administrativo devem ser exercitadas no IdP real.
- A API aceita exclusivamente issuer configurado, valida audience/assinatura/exp/iat/tipo e consulta vínculo local ativo a cada requisição.
- Roles válidas: resource_access[audience].roles ∩ allowlist. Realm roles e claims app_role/app_user_id vindas do token não concedem privilégios.
- Os valores técnicos padrão são `Administrator`, `Organizer` e `Participant`, com essa capitalização. Os rótulos em português neste documento descrevem seus significados; não são aliases aceitos no token. Um realm antigo precisa de migração administrativa explícita, sem exclusão de volumes.

Não guarde tokens em logs, autenticação persistente do Scalar, localStorage, scripts de shell ou arquivos. O Scalar só é exposto em desenvolvimento, com persistência de autenticação desabilitada e sem tokens pré-preenchidos. O teste OIDC os mantém apenas em memória. A API não oferece endpoint de login ou senha.

## Provisionamento e emergência

O primeiro administrador requer **duas ações separadas**: criar usuário/role no provedor e criar vínculo local via CLI `bootstrap-identity` com Bootstrap:Subject/Name/Email. Esse comando só funciona em tabela de usuários vazia e não concede role no Keycloak.

No laboratório, `bootstrap-local.mjs` consulta o ID efetivamente criado no banco do **Keycloak local** antes de chamar a CLI. Esse acesso é fixture de desenvolvimento; em produção obtenha o subject pela administração oficial do IdP. Não leia o banco de um IdP externo para integrar a aplicação.

Depois do bootstrap, um administrador pode provisionar `POST /api/v1/identity/users` informando subject verificado, nome e e-mail. Não há vinculação automática por e-mail.

`PUT /api/v1/identity/users/{id}/access` controla ativação e corte de tokens. Para retirada urgente de privilégio: remova role no IdP, revogue sessões/refresh e aplique corte local (ou desative a conta). Documente aprovação e preserve evidência administrativa. O sistema não reativa nem reatribui Administrador no restart.

## Autorização de exemplo

| Recurso/ação | Participante | Organizador | Administrador |
|---|---|---|---|
| Catálogo de eventos/locais/palestras | Ler autenticado | Ler autenticado | Gerenciar |
| Perfil Pessoa | Criar/ler/alterar o próprio vínculo | Próprio vínculo | Todos |
| Evento/palestras/conteúdos | Sem escrita administrativa | Somente evento de sua propriedade | Todos |
| Inscrição/cancelamento | Somente própria pessoa | Própria pessoa ou evento próprio | Todos |
| Lista de inscrições/presenças | Negado | Evento próprio | Todos |
| Certificado | Somente titular | Titular ou evento próprio | Todos |
| Auditoria, usuários, replay | Negado | Negado | Permitido, rede administrativa em produção |

Consulta pública de certificado revela autenticidade e metadados do evento, mas não o nome do titular. O código não concede autorização de escrita. O exemplo permite catálogo autenticado de palestras; reveja dados públicos conforme produto.

## Auditoria do provedor versus aplicação

A API registra mudanças de entidades e operações de replay com ator interno, inclusive remoções físicas e chaves compostas. Outbox/receipts/replay não geram auditoria recursiva. Usuário runtime não pode atualizar/apagar registros de auditoria.

Mudanças de roles no Keycloak acontecem fora da API. Admin events estão habilitados; details=false evita registrar representações potencialmente sensíveis. Assim, **não há garantia de histórico completo de perfis antes/depois na auditoria local**. Para esse aceite, configure exportação durável dos eventos administrativos e workflow/IaC com diff sanitizado, ator, aprovação e teste de alteração de role. Não habilite representação completa indiscriminadamente.

## Retenção e tratamento de dados

| Cópia | Comportamento entregue | Decisão produtiva necessária |
|---|---|---|
| Dados de negócio/identidade | Schema próprio, minimização, soft delete onde aplicável | Base legal, prazo, anonimização/eliminação e responsável |
| Certificado | Snapshot mínimo histórico, nome protegido na consulta anônima | Prazo, retificação e revogação do histórico |
| Outbox/receipt processados | Prune em lotes; padrão 7 dias | Prazo maior que horizonte operacional/reprocessamento |
| Outbox pendente/DLQ | Nunca removida por retenção automática | Resolver causa e aprovação de tratamento |
| Auditoria/replay | Append-only para runtime, valores mascarados por padrão | Retenção, archive imutável e processo privilegiado de expurgo |
| Logs/traces locais | Loki/Tempo 7 dias | Backend produtivo, acesso, região e custo |
| Métricas locais | Prometheus 15 dias | Janela de SLO e agregação de longo prazo |
| Keycloak events | 7 dias na configuração de referência | Exportação, prazo e revisão de dados |
| Backups | Ensaio local privado | Criptografia, cópia externa, expiração e tratamento pós-restore |

Não implementamos exclusão genérica automática de dados de negócio/auditoria: seria destrutiva sem a política aprovada. Soft delete não é anonimização. O aceite de um registro sintético rastreado por **todas** as cópias, inclusive backups externos, continua obrigatório antes da produção; esta base não declara conformidade LGPD.

## Ameaças e controles

BOLA/IDOR: policy por recurso e testes entre identidades. Falsificação de JWT/roles: validação criptográfica e allowlist. Replay de mensagem: fencing e consumidor idempotente. Abuso HTTP: limites por IP antes do JWT e por usuário após autenticação, corpo 1 MiB, timeouts. Os limitadores são por réplica; limite global exige controle na borda.

Headers de proxy só são considerados de proxies conhecidos. HTTPS do cliente termina na borda confiável; API não publica porta produtiva. Rede interna e host Docker permanecem fronteiras confiáveis. Administrador de host/banco ou imagem comprometida ainda pode acessar dados; use mínimo privilégio, atualizações e isolamento operacional.

Frontend não foi criado. Sua integração deve testar state/nonce/PKCE, refresh rotation, logout, limpeza de cache por subject/sessão e proteção XSS/CSP. Se política proibir tokens no browser, escolha BFF com cookie HttpOnly/Secure/SameSite e proteção CSRF; isso é evolução deliberada, não recurso existente.
