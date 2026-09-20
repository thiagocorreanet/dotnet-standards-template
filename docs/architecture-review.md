# Análise de arquitetura — Gestão de Eventos (.NET)

**Data:** 20/09/2026  
**Projeto analisado:** `/home/thiago-botelho/Downloads/ties-gestao-eventos-main`  
**Foco:** arquitetura .NET, observabilidade, segurança, confiabilidade e adoção de Keycloak.  
**Método:** Architecture Council, com análise de requisitos, opiniões independentes de arquitetura .NET, segurança e integração/confiabilidade, uma rodada de objeções e consolidação.  
**Resultado:** arquitetura aprovada com alterações para evolução; este parecer não aprova o estado atual para produção.

### Navegação

- [Recomendação e prioridades](#1-recomendação-executiva)
- [Escopo e limites](#2-escopo-evidências-e-limites)
- [Requisitos e perguntas pendentes](#3-problema-fatos-e-direcionadores)
- [Arquitetura atual](#4-arquitetura-atual-e-o-que-preservar)
- [Segurança](#5-segurança-achados-e-melhorias)
- [Confiabilidade e consistência .NET](#6-confiabilidade-e-consistência-net)
- [Observabilidade](#7-observabilidade-o-que-existe-e-o-que-criar)
- [Integração e migração para Keycloak](#8-keycloak-desenho-proposto-para-este-projeto)
- [Alternativas e decisão do conselho](#9-alternativas-debate-e-decisão-do-conselho)
- [Plano de ação](#10-plano-de-ação-verificável)
- [Testes e validações](#11-validação-realizada-e-testes-ainda-necessários)
- [Riscos e gatilhos de revisão](#12-riscos-residuais-confiança-e-gatilhos)
- [Evidências no código](#13-catálogo-de-evidências-do-snapshot)

## 1. Recomendação executiva

Preservar o monolito modular em ASP.NET Core 10, os módulos de negócio, EF Core e PostgreSQL. A estrutura é proporcional ao contexto documentado. As melhorias mais importantes estão nas garantias de autorização, concorrência, entrega de eventos e operação. Não foi identificado um requisito que justifique dividir os módulos em microsserviços.

O projeto já tem uma base relevante de observabilidade: Serilog, OpenTelemetry, métricas por caso de uso, correlação, instrumentação de banco, Outbox e health checks. A evolução necessária é transformar essa instrumentação em operação verificável: armazenamento persistente, painéis, indicadores de atraso, alertas, SLOs e procedimentos de resposta.

Para a evolução com identidade centralizada, recomendo o mesmo monolito integrado ao Keycloak via OpenID Connect, inicialmente com React usando Authorization Code + PKCE. Keycloak assume autenticação, credenciais e sessões; a API continua responsável pelas permissões sobre pessoas, eventos, inscrições e certificados. Um BFF no próprio host é uma alternativa se a política de segurança exigir tokens fora do navegador. Não é necessário criar outro microsserviço para isso.

Essa recomendação de provedor pressupõe interesse em SSO, MFA e gestão centralizada. Se o objetivo permanecer exclusivamente didático ou uma aplicação isolada sem esses requisitos, endurecer o ASP.NET Core Identity atual é uma alternativa válida e operacionalmente mais simples. Keycloak não é uma dependência obrigatória do framework .NET.

### Prioridades para decisão

| Ordem | Entrega | Motivo |
|---|---|---|
| P0 | Corrigir configuração de produção, segredos demonstrativos e portas publicadas | Há defaults conhecidos e um erro reproduzido no exemplo de sobreposição do Compose. |
| P1 | Definir e aplicar autorização por recurso e campo | Qualquer autenticado pode consultar cadastro completo e operar inscrições/certificados de outras pessoas. |
| P1 | Corrigir bootstrap administrativo, revogação e auditoria de perfis | Reinício pode restaurar privilégio; tokens antigos mantêm roles; remoções de vínculos não ficam adequadamente auditadas. |
| P1 | Proteger inscrições, agenda, certificados e transições contra concorrência | Validações anteriores à gravação não preservam as invariantes sob requisições simultâneas. |
| P1 | Corrigir replay transacional e processamento da Outbox | Reexecução com estado já aceito e leases sem identificação de dono fragilizam as garantias. |
| P1 | Operacionalizar observabilidade | Uma Outbox parada pode não produzir novos sinais de erro/latência e continuar invisível no health atual. |
| P2 | Migrar para Keycloak com contrato de identidade e testes | Alterar apenas issuer/Authority quebraria vínculos locais e não resolveria autorização. |
| P2 | Fortalecer entrega, recuperação, retenção e documentação | Runbooks e testes existentes precisam cobrir falhas e configuração efetiva. |

P0 significa corrigir antes de expor um ambiente real com a configuração afetada. P1 significa tratar antes de uma entrada em produção com os respectivos fluxos habilitados. P2 significa evolução planejada; não é uma classificação universal de gravidade.

## 2. Escopo, evidências e limites

A revisão cobriu composição do host, projetos e dependências, configurações de segurança, identidade e frontend, persistência, Outbox, auditoria, casos de uso críticos, testes, documentação e runbooks. A pasta é um snapshot sem diretório Git; não há commit identificável para ancorar o parecer.

Foram executados restore e a suíte .NET, usando PostgreSQL descartável do Testcontainers. Também foi renderizada a configuração do exemplo de deploy com `docker compose config`; a stack de aplicação não foi implantada. Nenhum código funcional ou configuração foi corrigido nesta entrega. Os comandos de build/teste geraram os artefatos locais usuais de compilação.

As referências `E01` a `E34`, ao final, apontam para arquivos e linhas do snapshot. Distinguem-se:

- **Confirmado:** código, configuração, documentação ou saída de diagnóstico observada.
- **Inferido:** cenário de falha deduzido do fluxo, ainda sem reprodução específica.
- **Proposto:** desenho ou critério de aceite recomendado, ainda não implementado.
- **Não verificado:** infraestrutura externa, dados reais, exposição efetiva na internet, carga e políticas da organização.

Não foi feito pentest, teste de carga, auditoria jurídica nem inventário de vulnerabilidades de dependências. Não se conclui que um ambiente real esteja exposto ou comprometido. Os riscos condicionais são indicados como tais.

## 3. Problema, fatos e direcionadores

### 3.1 Objetivo funcional e contexto confirmado

O sistema gerencia pessoas, locais/salas, eventos/trilhas/inscrições, palestras, presenças, certificados, usuários e auditoria. Os atores documentados são Administrador, Organizador e Participante. O projeto se apresenta como material de referência de uma palestra sobre monolito modular; não há evidência de requisito atual de grande escala ou operação multiorganização.

| Aspecto | Estado observado | Consequência arquitetural |
|---|---|---|
| Backend | ASP.NET Core 10 e EF Core 10; versões centralizadas | Evoluir a estrutura existente e manter patches verificados. |
| Frontend | React/Vite, nginx, TanStack Query e bearer em localStorage | A revisão de identidade também envolve sessão, cache e proteção do HTML. |
| Topologia | Um `Host.Api`, seis módulos e bibliotecas compartilhadas | Deploy e recursos compartilhados; fronteiras internas por contratos. |
| Banco | PostgreSQL 17, schema/DbContext por módulo, mesma connection string | Separação lógica de ownership, sem isolamento de segurança entre módulos demonstrado. |
| Integração interna | Interfaces síncronas e eventos via Outbox em PostgreSQL | Chamadas locais evitam rede; Outbox oferece desacoplamento temporal para auditoria. |
| Identidade | ASP.NET Core Identity e JWT HS256 emitido pela própria aplicação | A aplicação opera o ciclo de credenciais; Keycloak ainda não está integrado. |
| Permissões | Grupos autenticados por padrão; políticas `Gestao` e `Administracao` | Bom ponto de partida, mas insuficiente para titularidade/escopo de recursos. |
| Observabilidade | Serilog, OpenTelemetry, OTLP, opção Azure Monitor e Aspire local | Instrumentação existente deve ser preservada e validada ponta a ponta. |
| Testes | Unitários, arquitetura, integração e funcionais | Já existem quatro projetos de teste; não é necessário criar essa base do zero. |

### 3.2 Direcionadores de arquitetura

| ID | Direcionador | Condição verificável |
|---|---|---|
| D1 | Manutenção proporcional | Evoluir módulos sem dependências diretas entre `Module.*`, preservando testes arquiteturais. |
| D2 | Controle de acesso e proteção de dados | Um usuário não acessa campos ou recursos de outro sem permissão explicitamente definida. |
| D3 | Integridade do negócio | Concorrência não gera certificados duplicados, agenda conflitante ou transições inválidas; tolerância de lotação deve ser definida. |
| D4 | Auditoria recuperável | Alteração confirmada produz trilha rastreável; reentrega não duplica o efeito e falha persistente é detectada. |
| D5 | Diagnóstico operacional | Um erro/atraso pode ser localizado por rota, módulo, versão e trace; backlog parado gera alerta. |
| D6 | Identidade centralizada, se adotada | Autenticação OIDC, MFA/SSO conforme política, claims e revogação com contrato testado. |
| D7 | Implantação e recuperação controladas | Configuração efetiva é validada; restauração e migrações são ensaiadas; segredos não têm defaults produtivos. |

D1 e capacidades funcionais vêm da documentação. D2–D5 e D7 são critérios propostos para evolução a produção. D6 é o objetivo de identidade avaliado a pedido do usuário, ainda dependente do contexto organizacional.

### 3.3 Premissas e perguntas pendentes

Para concluir a análise sem inventar requisitos, assumiu-se uma evolução para produção, com possível instalação nova de Keycloak, uma organização inicialmente e volume ainda não dimensionado. Essas são premissas de trabalho, não fatos sobre o negócio.

| Classificação | Questão | Decisão que pode mudar |
|---|---|---|
| Crítica | Participantes usarão o portal diretamente? Organizador administra todos os eventos ou apenas os seus? | Vínculo usuário–pessoa e autorização por evento/recurso. |
| Crítica | Haverá várias empresas com isolamento obrigatório? | Identificador de organização, filtros, índices, autorizações e eventual isolamento de dados. Realm não substitui isolamento na aplicação. |
| Crítica | Qual o prazo máximo para revogar acesso administrativo? | JWT local de curta duração, consulta de estado, introspecção ou sessão BFF com invalidação. |
| Crítica | Qual é a indisponibilidade e perda de dados tolerada, inclusive no dia do evento? | SLOs, disponibilidade do Keycloak, backups, RTO/RPO e operação offline de presença, se necessária. |
| Crítica | Lotação e agenda precisam ser estritas? Certificado deve preservar os dados do momento da emissão? | Coordenação transacional e snapshots históricos. |
| Importante | Já existe IdP corporativo e equipe para operá-lo? Quais picos de login/inscrição e limites de orçamento? | Reutilização do provedor, dimensionamento e custo operacional. |
| Importante | Qual retenção é necessária para auditoria, eventos, logs e dados pessoais? | Armazenamento, anonimização, purga e acesso aos históricos. |

## 4. Arquitetura atual e o que preservar

```text
React / nginx
      |
      v
Host.Api (.NET 10)
  |-- Identidade: credenciais, perfis, emissão JWT
  |-- Pessoas: cadastro de participantes/palestrantes
  |-- Locais: locais e salas
  |-- Eventos: eventos, trilhas e inscrições
  |-- Palestras: agenda, presenças e certificados
  |-- Auditoria: consumo de EntidadeAlterada
  |
  |-- Shared.Contracts: interfaces e eventos entre módulos
  |-- Shared.Data: EF, auditoria, soft delete e Outbox
  |-- Shared.Messaging: processador + handlers em processo
  |-- Shared.Http / WebHost: endpoints, validação, erros, políticas
  `-- Shared.Observability: logs, métricas e traces
      |
      v
PostgreSQL: um banco, schemas por módulo

API -- OTLP --> Aspire Dashboard no Compose
API -- opção de exportação --> Azure Monitor
```

Preservar as fronteiras por módulo, casos de uso verticais, host de composição, EF Core direto, consultas projetadas, `TagWith`, erros de negócio padronizados, autenticação por padrão e testes de dependência. A Outbox é justificada pelo requisito de persistir a intenção de auditar junto à alteração de negócio.

Dois limites precisam ficar explícitos na documentação: schema não é barreira de segurança quando todos usam a mesma credencial; chamadas síncronas entre módulos não compartilham automaticamente uma transação. Estar no mesmo processo reduz custo de comunicação, mas não elimina concorrência.

O pooling do DbContext e os interceptors singleton não foram classificados como defeito: `CurrentUser` consulta `IHttpContextAccessor` a cada acesso, e não foi identificado estado de usuário capturado permanentemente no interceptor. Proteger isso com teste alternando usuários e execução sem contexto HTTP. [E03, E13]

## 5. Segurança: achados e melhorias

### SEG-01 — Defaults de demonstração e deploy com portas herdadas

**Severidade:** crítica se um ambiente acessível for iniciado com os segredos demonstrativos; configuração e problema de merge confirmados. **Prioridade:** P0.

O Compose usa ambiente `Production`, mas oferece fallback conhecido para chave de assinatura e credencial administrativa. Também publica PostgreSQL, API, frontend e o Aspire Dashboard, este com acesso anônimo. Usar os valores de demonstração em ambiente real permite acesso com credenciais conhecidas; conhecer a chave simétrica permite produzir JWTs aceitos pela API. Os valores não são reproduzidos neste relatório. [E01]

O runbook VPS tenta remover publicações usando `ports: []`. Foi renderizado o Compose base junto ao bloco de override do documento. O resultado preservou as portas `5432`, `5761`, `5760`, `18888` e `4317`; para o dashboard, adicionou uma publicação em loopback sem remover a anterior. Isso comprova o erro no exemplo, não a exposição de um servidor real. [E02]

**Correção mínima proposta:** separar configuração local de produção; tornar segredos obrigatórios sem fallback; validar ausência de valores demonstrativos; retirar publicações internas. Usar `!reset []` ou `!override` quando suportados pela versão adotada, ou um arquivo produtivo independente. Validar sempre o resultado de `docker compose config`. A semântica de remoção/substituição é documentada pelo [Docker Compose](https://docs.docker.com/reference/compose-file/merge/).

**Aceite:** produção sem segredos válidos falha na inicialização; somente o ingress HTTPS esperado fica acessível de fora; banco/OTLP não são públicos e dashboard exige identidade. Se defaults já foram usados em ambiente real, rotacionar os segredos envolvidos.

### SEG-02 — Confiança irrestrita em headers de proxy

**Severidade:** alta. **Prioridade:** P1. `KnownNetworks` e `KnownProxies` são limpos. Com acesso direto à API, o cliente pode influenciar o IP percebido por `X-Forwarded-For`, afetando logs e proteção por IP. [E03, linhas 70–75]

**Correção:** permitir somente proxies/redes conhecidos; ingress deve sobrescrever headers não confiáveis; restringir o acesso direto à API. Revisar também a cadeia TLS → nginx → API para preservar o protocolo externo correto.

**Aceite:** um header falso vindo de origem não confiável não altera o IP usado para segurança; tráfego pelo proxy autorizado preserva a origem esperada.

### SEG-03 — Cadastro completo acessível a qualquer autenticado

**Severidade:** alta para portal com participantes. **Prioridade:** P1. `ObterPessoa` explicitamente retorna CPF sem máscara, contato e cadastro completo a qualquer autenticado. Isso segue a documentação atual; a política é que precisa ser refinada para uso real. [E04]

**Correção:** definir matriz ator × ação × campo; restringir visão administrativa, criar projeção mínima para consultas gerais e visão própria para participante com vínculo verificado. Não basta ocultar campos na tela.

**Aceite:** participante A não recebe documento/contato de B em detalhe, listagem ou outros contratos. Gestor recebe apenas os campos necessários ao seu escopo.

### SEG-04 — Falta de autorização sobre inscrições e certificados

**Severidade:** alta quando há autosserviço. **Prioridade:** P1. Inscrever, cancelar inscrição e emitir certificado exigem autenticação, mas os IDs recebidos não são confrontados com a identidade do solicitante. Usuário e Pessoa não têm vínculo obrigatório. Uma conta pode atuar sobre outra pessoa se conhecer os IDs. [E05, E06, E07]

**Correção:** até definir titularidade, restringir esses fluxos à gestão autorizada. Para autosserviço, criar vínculo explícito e aplicar autorização sobre o recurso no backend; a role Organizador deve ser combinada com o escopo do evento, caso esse seja o modelo de negócio. Isso pode usar `IAuthorizationService`/handlers do ASP.NET Core, sem serviço separado. [Autorização por recurso — Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based?view=aspnetcore-10.0).

**Aceite:** usuário A não cancela nem emite por B; o titular e o gestor autorizado conseguem; conhecer um UUID não concede permissão. Testar também acesso a eventos em rascunho e listagens de inscrições/presenças conforme a matriz aprovada.

### SEG-05 — Tokens longos e retirada de privilégio sem efeito imediato

**Severidade:** alta para administração. **Prioridade:** P1. O prazo padrão é 480 minutos. A API valida assinatura, issuer, audience e expiração, mas não verifica o estado atual da conta ou das permissões a cada uso. Remover uma role no banco não altera tokens já emitidos; logout local não invalida uma cópia do bearer. [E03, E08, E09]

**Correção:** definir prazo máximo de revogação; reduzir duração dos access tokens; para operações sensíveis, escolher consulta de estado/versão de autorização, sessão invalidável ou introspecção conforme o requisito. Cada opção acrescenta estado ou dependência de rede. Keycloak com JWT validado localmente mantém essa mesma questão.

**Aceite:** retirada de Administração e bloqueio de conta tornam a operação proibida no prazo acordado, inclusive com cópia do token antigo. Não prometer revogação instantânea apenas porque existe logout no provedor.

### SEG-06 — Reinício pode restaurar Administrador

**Severidade:** alta. **Prioridade:** P1. Com email e senha de bootstrap configurados, o seed encontra o usuário existente e reatribui Administrador caso ausente, fora do bloco que cria uma conta nova. Revogação seguida de restart pode restaurar o privilégio. [E10, linhas 63–95]

**Correção:** bootstrap explícito e único; não promover novamente contas existentes de forma automática; retirar configuração inicial após provisionamento.

**Aceite:** remover a role do administrador inicial e reiniciar não a restaura. Provisionamento inicial continua idempotente e auditável.

### SEG-07 — Auditoria incompleta de alteração de perfis

**Severidade:** alta para rastreabilidade administrativa. **Prioridade:** P1. O interceptor considera inclusões/alterações, mas exclusão física de `UsuarioPerfil` permanece `Deleted` e é ignorada. Alterar `ConcurrencyStamp` registra uma alteração no usuário, sem preservar qual perfil foi removido. A identificação de entidades com chave composta também usa apenas o primeiro componente. [E09, E11]

**Correção:** evento explícito de permissões com ator, alvo, perfis anteriores/novos e correlação, gravado na transação; cobrir exclusão física e chave composta no mecanismo genérico quando aplicável. O histórico deve usar identificador estável do ator, não depender apenas de nome/email.

**Aceite:** reconstruir uma concessão e revogação de múltiplos perfis, identificando autor, alvo, instante e estado anterior/posterior.

### SEG-08 — Sessão no navegador e cache entre identidades

**Severidade:** média/alta conforme uso de navegador compartilhado. **Prioridade:** P1 para limpar cache; P2 para evolução OIDC. O bearer fica em localStorage. O `QueryClient` é único, suas chaves não incluem identidade e logout não limpa o cache de consultas. Rotas verificam autenticação, sem exigir role. A reapresentação de dados da sessão anterior é uma inferência estática, ainda sem teste de navegador. Não foi demonstrada uma vulnerabilidade XSS explorável. [E12]

**Correção:** cancelar requisições pendentes, limpar cache na troca de usuário/logout e impedir respostas antigas de repovoá-lo; aplicar guardas de perfil para UX, mantendo autorização no backend. Na SPA OIDC, tokens em memória. Adicionar CSP e headers ao HTML servido pelo nginx; headers apenas na API não protegem o documento React.

**Aceite:** Admin → consultar auditoria → logout → Participante não exibe dados anteriores, mesmo offline/rede lenta. Tokens não ficam persistidos no storage; política CSP é testada com login/redirecionamentos.

### SEG-09 — Rate limiting antes da autenticação

**Severidade:** média. **Prioridade:** P1. `UseRateLimiter` precede `UseAuthentication`, embora a partição tente usar `User.Identity.Name`. No fluxo JWT normal, a limitação cai no IP. Pessoas atrás do mesmo NAT compartilham cota; réplicas possuem limites locais independentes. [E03, linhas 100–103 e 168–171]

**Correção:** autenticar antes do limitador que usa identidade; chave estável derivada de identidade validada, não nome exibido. Manter proteção anônima por IP e limite específico de login. Limite agregado no ingress só se houver requisito de controle entre réplicas. Padronizar `Retry-After`, corpo 429 e correlação quando aplicável.

**Aceite:** usuários diferentes no mesmo IP têm cotas próprias; chamadas anônimas continuam limitadas; o comportamento em múltiplas réplicas está documentado.

### SEG-10 — Privacidade, retenção e superfície administrativa

**Severidade:** média, com impacto dependente dos dados reais. **Prioridade:** P2. O snapshot de auditoria copia propriedades pessoais não marcadas como sensíveis. Soft delete não elimina cópias em Outbox/auditoria. Swagger/OpenAPI permanecem anônimos e o Swagger persiste autorização. O login diferencia conta inexistente, inativa e bloqueada, contrariando a intenção documental de resposta genérica. [E03, E11, E14, E15]

**Correção:** minimizar snapshots, definir retenção por classe de dado, restringir leitura da auditoria, remover ou proteger documentação administrativa em produção e desabilitar persistência de autorização nesse ambiente. Uniformizar resposta pública de falha de login e registrar o motivo apenas no canal de segurança. Definir recuperação de conta e MFA para privilegiados.

A marcação sensível já protege hash de senha e SecurityStamp, o que deve permanecer. `UsuarioToken.Value` não possui a mesma marcação: antes de usar essa tabela para segredos, testar sua exclusão da trilha. Não foi demonstrado que os fluxos atuais persistam esses segredos nela. [E14]

**Aceite:** um registro sintético é rastreável por todas as cópias e sua política de retenção é executável; senha/token/hash não aparecem em logs, traces ou auditoria; não há acesso anônimo ao painel operacional. Esta recomendação é técnica e não constitui declaração de conformidade legal.

## 6. Confiabilidade e consistência .NET

### REL-01 — Reexecução transacional precisa reconstruir a operação

**Severidade:** alta. **Prioridade:** P1. A estratégia de retry executa a operação no mesmo DbContext; casos de uso alteram entidades antes desse bloco. `SaveChangesAsync` aceita o estado antes de `CommitAsync`, e o interceptor limpa eventos antes da confirmação. [E11, E16]

**Cenário inferido:** SaveChanges termina, commit falha transitoriamente e é revertido. O contexto pode estar com entidades `Unchanged`; a repetição confirma sem reaplicar atualização/Outbox. Se a resposta do commit se perde, também há resultado indeterminado. Esses cenários não foram reproduzidos nesta revisão.

**Correção proposta:** reconstruir toda a unidade reexecutável com contexto novo, IDs estáveis e verificação de resultado indeterminado; ou desenho controlado com `SaveChanges(false)`, confirmação e `AcceptAllChanges`, tornando também o interceptor compatível com retry. Nenhuma opção se resume a mudar uma chamada. A [documentação de resiliência do EF Core](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency) trata explicitamente reexecução e falhas durante commit.

**Aceite:** injetar falhas antes da persistência, entre Save/Commit e durante confirmação; verificar negócio, Outbox e auditoria sem perda, duplicação ou resposta de sucesso vazia.

### REL-02 — Lotação não é garantida sob concorrência

**Severidade:** alta se capacidade for estrita. **Prioridade:** P1. A leitura da situação/contagem ocorre antes da transação. O índice único protege duplicidade da pessoa no evento, não o total de vagas. N solicitações disputando a última vaga podem causar excesso de N−1. A documentação aceita excesso de uma unidade, mas o código não limita a corrida a uma unidade. [E05, E17]

**Correção:** decidir a tolerância de negócio; se estrita, coordenar leitura, decisão e gravação com lock transacional por evento, contador condicional ou isolamento adequado. Todos os comandos que mudam capacidade/estado precisam participar. Lock em memória não protege várias réplicas, e token de versão do pai não protege inserção do filho sem atualização correspondente do pai.

**Aceite:** vinte solicitações simultâneas para uma vaga resultam em uma confirmação, se essa for a regra aprovada; nenhuma alteração de capacidade/cancelamento concorrente passa com estado obsoleto.

### REL-03 — Dupla reserva de sala

**Severidade:** alta. **Prioridade:** P1. Criar/atualizar palestra consulta sobreposição antes da transação; o índice de sala/início não impede intervalos concorrentes. [E18]

**Correção:** lock transacional por sala cobrindo toda a decisão ou constraint PostgreSQL de exclusão por sala/intervalo, considerando atividade, soft delete e sala opcional. Ao trocar sala, coordenar origem/destino em ordem estável quando usar locks. Tratar conflito conhecido com resposta de negócio.

**Aceite:** criar/criar e criar/alterar simultaneamente não produzem sobreposição; períodos adjacentes permanecem válidos. A opção de constraint usa capacidade nativa de [ranges/exclusion constraints do PostgreSQL](https://www.postgresql.org/docs/current/rangetypes.html).

### REL-04 — Certificado duplicado para a mesma pessoa/palestra

**Severidade:** alta. **Prioridade:** P1. O caso de uso verifica certificado carregado antes de gravar. O índice `(PalestraId, PessoaId)` não é único; apenas o código é. Duas requisições podem criar dois certificados com códigos distintos, apesar do contrato de emissão idempotente. [E07, E19]

**Correção:** unicidade no banco para a identidade de negócio do certificado, após definir reemissão/soft delete; tratar especificamente essa colisão, descartar estado falho e devolver o existente. Não traduzir qualquer violação de unicidade como sucesso.

**Aceite:** emissão concorrente gera uma linha, um evento de emissão e respostas coerentes com o mesmo certificado; colisão de código tem tratamento próprio.

### REL-05 — Transições e invariantes de coleções

**Severidade:** alta. **Prioridade:** P1. Transições leem o agregado antes de gravar sem token de concorrência correspondente; remoções paralelas podem retirar os últimos filhos de um agregado. [E20]

**Correção:** versão de concorrência nas alterações e revalidação de conflitos; em invariantes sobre filhos, modificar a versão do pai ou coordenar por lock transacional. A política de retry não pode reaplicar uma decisão antiga sem reler o estado.

**Aceite:** cancelar/iniciar simultaneamente preserva transição válida e evento correspondente; excluir as duas últimas trilhas/salas em paralelo não viola a quantidade mínima.

### REL-06 — Consistência entre módulos e histórico

**Severidade:** média/alta conforme regra. **Prioridade:** P2, ou P1 se afetar lotação estrita. As interfaces síncronas consultam outros DbContexts; isso não torna as verificações parte de uma transação comum. Alterar datas/local do evento pode invalidar palestras já existentes. Na inscrição, local referenciado ausente pode produzir capacidade nula, tratada como ilimitada. [E05, E21]

**Correção:** classificar cada regra como válida no momento da aceitação, continuamente ou apenas para histórico. Coordenar comandos e concorrência quando a regra for estrita; usar snapshot/reconciliação quando eventual for aceitável. Local obrigatório ausente deve ter resposta explícita, sem cair silenciosamente em ilimitado.

Certificado hoje resolve dados mutáveis na validação. Se precisar representar a emissão original, armazenar snapshot mínimo de nome/título/datas relevantes e definir sua retenção. Essa duplicação tem custo de privacidade e deve ser uma exceção consciente ao princípio de não copiar dados pessoais. [E22]

### REL-07 — Outbox tem reentrega e lease sem identificação de dono

**Severidade:** alta. **Prioridade:** P1. O lote é reservado por `LockedUntil`, processado sequencialmente e confirmado apenas por ID. Não há claim token, renovação ou condição de ownership no ack/nack. Quando a lease expira, outra instância pode retomar a mensagem; uma execução antiga ainda pode alterar seu estado. [E23, E24]

**Correção:** claim token/owner, ack/nack condicionais, timeout e estratégia de renovação/dimensionamento por mensagem; tratamento de shutdown; consumidores idempotentes. `FOR UPDATE SKIP LOCKED` pode otimizar o claim, mas não resolve sozinho queda entre efeito e confirmação.

A garantia adequada é entrega com possibilidade de repetição e efeito idempotente. Não há garantia geral de exatamente uma execução. O handler de auditoria já usa ID do evento como PK, mas `Any` seguido de insert pode disputar: tornar a inserção atomicamente idempotente ou tratar a constraint específica. Uma Inbox genérica pode ser desnecessária nesse consumidor; para outros efeitos, avaliar deduplicação por consumidor/evento. [E25]

**Aceite:** duas instâncias processam lote mais lento que a lease, um ack antigo é rejeitado, e queda após persistir efeito/antes do ack não duplica o resultado.

### REL-08 — Falha terminal, replay e contratos de eventos

**Severidade:** média/alta conforme obrigação de auditoria. **Prioridade:** P1/P2. Ao esgotar tentativas, o processador agenda a mensagem para 365 dias depois; não existe estado terminal explícito. Eventos sem handler retornam sucesso no publicador. Tipos persistidos dependem do nome CLR completo. A ordenação por data no claim não garante ordem por agregado entre réplicas/retries. [E24, E26]

**Correção:** status terminal, `NextAttemptAt`, erro classificado, jitter, replay seletivo autorizado/auditado e retenção. Definir quais eventos exigem consumidor; sem handler pode ser aceitável para eventos demonstrativos sem efeito contratado, mas não para auditoria obrigatória. Adotar nomes/versionamento de contrato estáveis quando eventos precisarem sobreviver a deploys/refatorações. Se ordem for requisito, definir sequência por agregado e controle no consumidor.

**Aceite:** falha persistente gera alerta e estado inspecionável; replay de IDs selecionados tem responsável e resultado; versão nova lê mensagens antigas suportadas; consumidor obrigatório ausente impede operação silenciosamente incompleta.

### REL-09 — Migrações, privilégios, recuperação e manutenção

**Severidade:** média. **Prioridade:** P2. Há migrador com lock e runbooks, o que é positivo. Não foi localizada automação CI/CD no snapshot equivalente às etapas descritas nos documentos. Scripts de backup não comprovam restauração nem cópia externa. [E27, E28]

**Correção:** executar migrações como etapa/job controlado em produção, com credencial própria; aplicação com DML mínimo; ensaiar upgrade de versão anterior e compatibilidade durante rollout. Testar restore de banco e, futuramente, Keycloak. Definir RPO/RTO antes de escolher replicação e retenção. Evitar promessa de rollback apenas por imagem se a migração já rompeu compatibilidade.

Manter SDK, pacotes e imagens fixados/revisados, scan de dependências/containers e atualização de patches. Há pacotes de instrumentação com sufixos beta/rc: avaliar suporte e comportamento na versão fixada, sem assumir vulnerabilidade pela nomenclatura. Não foi realizada varredura de CVEs. [.NET — ciclo de suporte](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support).

## 7. Observabilidade: o que existe e o que criar

### 7.1 Inventário confirmado

| Recurso existente | Evidência | Evolução necessária |
|---|---|---|
| Logs estruturados Serilog e enriquecimento de request | E29 | Retenção, acesso, custo, saneamento de exceções e verificação do destino. |
| `ActivitySource`/`Meter` por módulo | E30 | Convenções estáveis e painéis por resultado, sem cardinalidade por usuário. |
| Instrumentação ASP.NET Core, HttpClient, EF, Npgsql, runtime/processo | E29 | Validar spans redundantes EF/Npgsql e instrumentações adicionadas pelo Azure Monitor; não assumir duplicação sem medir. |
| `usecase.executions`, `usecase.failures`, `usecase.duration` | E30 | Separar rejeição de negócio, erro técnico e cancelamento na análise. |
| `outbox.messages.processed`, `outbox.messages.failed`, `outbox.delivery.latency` | E24 | Adicionar backlog, idade, estado terminal, lease e progresso. |
| `traceparent` persistido na Outbox | E11, E24 | Testar continuidade após retry/restart e estratégia para processamento tardio. |
| `X-Correlation-Id` recebido/devolvido | E31 | Validar formato/tamanho e preservar correlação customizada no processamento assíncrono quando necessária. |
| `/health/live` e `/health/ready` | E03 | Readiness verifica PostgreSQL; não demonstra funcionamento da Outbox nem do login. |
| Aspire Dashboard local e configuração opcional Azure Monitor | E01, E29 | Escolher destino produtivo persistente e provar chegada dos três sinais. |

### OBS-01 — Backlog e ausência de progresso

**Prioridade:** P1. A latência da Outbox só é medida quando uma entrega termina com sucesso. Processador parado pode deixar a métrica sem amostras; isso não significa latência zero. `CountPendingAsync` existe, mas não está ligado à coleta. [E23, E24]

Criar instrumentos com nomes abaixo como proposta do projeto, não como métricas já existentes:

| Métrica proposta | Tipo/unidade | Dimensões permitidas | Uso |
|---|---|---|---|
| `outbox.pending` | Gauge / mensagens | módulo, estado | Quantidade aguardando/retry, separada das terminais. |
| `outbox.oldest_pending_age` | Gauge / segundos | módulo | Atraso da mensagem não terminal mais antiga. |
| `outbox.deadletter` | Gauge / mensagens | módulo, classe de erro | Trabalho que exige intervenção. |
| `outbox.last_successful_cycle_age` | Gauge / segundos | instância | Tempo desde ciclo de consulta concluído; identifica processador travado. |
| `outbox.last_progress_age` | Gauge / segundos | módulo | Tempo sem avanço; interpretar junto com backlog. |
| `outbox.handler.duration` | Histograma / segundos | módulo, handler estável, resultado | Dimensionar timeout/lease e identificar consumidor lento. |
| `outbox.lease_expired` | Counter | módulo | Recuperações por lease expirada. |
| `outbox.stale_ack_rejected` | Counter | módulo | Execuções antigas tentando confirmar trabalho retomado. |

Consultar o banco de forma periódica e controlada; callbacks síncronos de métricas não devem executar consultas bloqueantes por coleta. Se cada réplica observar o mesmo backlog global, definir agregação por máximo/observador único para evitar contagem multiplicada. Separar métricas globais das locais da instância.

### OBS-02 — Painéis que respondem perguntas operacionais

| Painel | Conteúdo mínimo | Pergunta respondida |
|---|---|---|
| API e experiência | Tráfego, disponibilidade, p50/p95/p99 por rota normalizada, 5xx, 401/403/429, versão implantada | O usuário consegue concluir as operações e o problema começou após qual deploy? |
| Negócio | Inscrições aceitas/rejeitadas por motivo, presença registrada, certificados emitidos, conflitos de agenda/capacidade | A regra está rejeitando corretamente ou houve mudança anormal de comportamento? |
| Outbox e auditoria | Backlog, idade máxima, entregas, retries, terminais, leases, duração de handlers | Alterações confirmadas estão chegando à auditoria dentro do prazo? |
| Plataforma .NET/PostgreSQL | CPU, memória, GC, thread pool, conexões, espera de pool, locks/deadlocks, consultas lentas, disco | Qual recurso limita o serviço e qual módulo consome esse recurso? |
| Identidade | Sucesso/falha de login, MFA, bloqueios, refresh, erros de discovery/JWKS e ações administrativas | O provedor ou a aplicação está recusando acesso? Existe abuso ou configuração incorreta? |
| Pipeline de telemetria | Dados recebidos/exportados, filas, erros, descartes, disponibilidade e orçamento | Estamos sem incidentes ou sem coleta? |

As métricas de negócio devem representar resultado confirmado, não apenas entrada no caso de uso; evitar duplicá-las por retry. Não usar pessoa, email, CPF, token, ID de evento, mensagem ou URL completa como label de métrica. Identificadores de alta cardinalidade ficam em traces/logs restritos quando necessários.

### OBS-03 — SLIs, SLOs e alertas acionáveis

Não há SLO fornecido pelo negócio. Os valores seguintes são exemplos iniciais para validação com medição e responsáveis; não são requisitos confirmados nem dimensionamento de produção.

| Objetivo proposto | Como medir | Exemplo de alerta e ação |
|---|---|---|
| Disponibilidade da API de 99,9% em 30 dias | Requests elegíveis sem falha atribuível ao serviço / total elegível; incluir 5xx/timeouts e definir tratamento de sobrecarga/429 | Alerta por consumo acelerado do orçamento de erro em janela curta e longa; responsável de operação verifica rollout, banco e dependências. |
| p95 de leitura abaixo de 500 ms e escrita abaixo de 1 s | Histograma HTTP por rota/categoria em tráfego representativo | Latência sustentada acima do objetivo com volume mínimo; investigar spans e pool/locks. Não avaliar por média. |
| 99% da auditoria entregue em até 30 s | Histograma de entrega mais gauge de idade/backlog | Aviso com pendência >60 s; crítico com >5 min ou terminal, calibrados ao negócio. |
| Recuperação do login dentro do objetivo operacional | Probe sintético dedicado e eventos do IdP/API | Distinguir credencial inválida de falha técnica de login/refresh; não paginar por cada senha errada. |
| Telemetria contínua | Heartbeat/exportação e chegada ao destino | Ausência de coleta/descartes sustentados dispara alerta independente do tráfego. |

Excluir rejeições esperadas de negócio do indicador de disponibilidade técnica, mantendo-as visíveis em painel próprio. Isso não deve esconder falhas reais de autorização ou indisponibilidade mascaradas como 4xx. Definir tratamento de cancelamento do cliente separadamente.

Cada alerta precisa ter responsável, gravidade, janela, volume mínimo, link para painel/consulta e runbook. Evitar alertas por evento isolado sem ação operacional. Testar o acionamento e a recuperação; possuir um dashboard não comprova que alguém será avisado.

### OBS-04 — Logs, traces e correlação de ponta a ponta

Padronizar contexto de serviço, ambiente, versão/commit do build, módulo, caso de uso, rota, resultado, `TraceId`/`SpanId` e código estável de erro. Hoje a versão vem do assembly da biblioteca de observabilidade: garantir que represente o build implantado, e não um valor constante entre releases. [E29]

Validar `X-Correlation-Id` recebido: limite de tamanho, formato permitido e valor único por header; gerar identificador seguro quando inválido. É entrada controlada pelo cliente, útil para suporte, mas não prova de identidade. Seu valor customizado não é persistido junto à Outbox; hoje o elo assíncrono é o `TraceParent`. Se a busca operacional depender do ID customizado, adicioná-lo ao envelope. [E31, E24]

Testar uma jornada HTTP → caso de uso → SQL → Outbox → auditoria. Para processamento tardio, decidir entre continuidade do trace e um novo trace com `ActivityLink`, considerando retenção e sampling. Não enviar bearer nem dados pessoais em baggage. No navegador, começar com captura sanitizada de falhas/latência e correlação; adicionar tracing completo do frontend apenas se ajudar a localizar problemas reais.

O decorator já limita metadados por convenção; fortalecer a política por allowlist explícita onde a heurística por nome de propriedade for insuficiente. `WithExceptionDetails`, SQL, mensagens de erro e `Outbox.Error` também precisam de revisão: marcar um campo como sensível na auditoria não o remove de todas essas superfícies. [E11, E29, E30]

### OBS-05 — Backend de telemetria e custo

O Aspire do Compose atende desenvolvimento/demonstração. A produção precisa de histórico persistente, controle de acesso, retenção e alertas. Escolher um destino principal:

- **Azure existente:** aproveitar Application Insights/Azure Monitor e o mecanismo de coleta da plataforma. Validar explicitamente logs, métricas e traces; a coexistência de Serilog e providers não prova que todos chegaram.
- **VPS ou preferência por portabilidade:** OTLP para Collector/serviço gerenciado e backend persistente. Grafana com componentes de logs/traces/métricas é uma opção, mas não é obrigatório operar toda essa stack internamente.

Collector se justifica quando é necessário centralizar credenciais, filtrar dados, controlar filas/sampling ou encaminhar destinos. Para um único destino e baixa complexidade, exportação direta pode ser suficiente. Quando houver Collector, configurar recursos limitados, batch, retry/fila e persistência de fila se a perda tolerada exigir, monitorando saturação. Isso adiciona operação e não garante perda zero. [OpenTelemetry — resiliência do Collector](https://opentelemetry.io/docs/collector/resiliency/).

Definir sampling por ambiente e retenção separada para cada sinal. Não prometer guardar todos os traces de erro com head sampling baixo; essa necessidade pode exigir tail sampling ou outra estratégia que preserve os dados necessários. Auditoria de negócio deve continuar no armazenamento próprio e não ser amostrada como trace.

### OBS-06 — Health e degradação

Manter liveness simples para indicar que o processo responde. Readiness deve refletir dependências necessárias para servir aquela instância, com timeout. Criar sinal separado de saúde do processador/atraso, evitando retirar todas as APIs de circulação por uma pendência isolada da auditoria sem política de negócio que exija isso.

Com Keycloak, não fazer cada probe da API depender de uma chamada ao IdP: JWT válido e chaves já disponíveis podem permitir operação durante falha temporária do provedor. Medir login/refresh/discovery separadamente. Cache frio ou chave desconhecida durante indisponibilidade deve falhar de forma fechada; não aceitar token sem validação.

## 8. Keycloak: desenho proposto para este projeto

### 8.1 Divisão de responsabilidades

| Responsabilidade | Proprietário proposto |
|---|---|
| Login, senha, MFA, recuperação, sessão SSO e federação | Keycloak ou IdP corporativo conectado a ele. |
| Roles globais desta aplicação | Client roles de `gestao-eventos-api` no Keycloak, caso essa seja a fonte aprovada. |
| Identificador interno, vínculo com Pessoa, conta habilitada na aplicação | Módulo Identidade local adaptado. |
| Organizador autorizado para determinado evento e titularidade de inscrição/certificado | Domínio/API, com políticas sobre recursos. |
| Auditoria de concessão/retirada de roles no provedor | Eventos administrativos do Keycloak, exportados com retenção/acesso definidos. |
| Auditoria de decisões e operações de negócio | Módulo Auditoria da aplicação, com identidade interna estável. |

Manter uma única fonte autoritativa por permissão. Se roles globais forem geridas no Keycloak, as tabelas locais antigas não podem continuar concorrendo com elas. Permissões específicas de evento podem permanecer locais sem duplicar a administração do provedor.

### 8.2 Fluxo recomendado: React + Authorization Code + PKCE

```text
1. React inicia login e redireciona o navegador ao Keycloak.
2. Keycloak autentica e aplica MFA conforme a política.
3. Navegador retorna com authorization code; cliente usa PKCE S256.
4. React mantém tokens em memória e envia access token à API.
5. API valida token com metadata/chaves do issuer confiável.
6. API resolve identidade local e autoriza a operação sobre o recurso.
7. Negócio + Outbox são gravados; auditoria é entregue em seguida.
```

O cliente do navegador deve ser público, sem client secret. Configurar redirects e web origins específicos; usar biblioteca OIDC mantida, como `keycloak-js`, e não implementar o protocolo manualmente. O adaptador documenta Authorization Code, PKCE S256 e tokens em memória. [Keycloak — JavaScript adapter](https://www.keycloak.org/securing-apps/javascript-adapter).

Proposta de configuração para esta aplicação:

| Item | Proposta |
|---|---|
| Realm | Realm da aplicação/organização, separado do `master`; ambientes com configurações e credenciais distintas. |
| Cliente web | `gestao-eventos-web`, público, Standard Flow, PKCE S256, redirects/logout redirects exatos. |
| Recurso/API | `gestao-eventos-api`, audiência identificável; não precisa obter tokens para apenas validá-los. |
| Audiência | Access token destinado à API deve conter `gestao-eventos-api` em `aud`; rejeitar token de outro recurso. |
| Roles iniciais | `Administrador`, `Organizador`, `Participante`, limitadas ao cliente da API; revisar se os nomes representam realmente permissões necessárias. |
| Fluxos desabilitados por padrão | Implicit e Direct Access Grants; service account apenas para integração backend que precise atuar como máquina. |
| MFA | Obrigatório para privilegiados conforme política; recuperação e contas de emergência testadas. |
| Duração | Proposta inicial de access token curto, por exemplo 5–10 minutos; ajustar ao prazo de revogação, carga e experiência exigidos. |

Configurar explicitamente audience e roles; não assumir que o token emitido para o frontend já é aceito pela API. Keycloak representa roles de cliente em `resource_access` e permite configurar audiência por mappers. [Keycloak — administração de roles e audiência](https://www.keycloak.org/docs/latest/server_admin/index.html#_audience).

Não adaptar `POST /identidade/sessoes` para receber senha e repassá-la ao token endpoint. A recomendação é redirecionamento OIDC. O fluxo de senha/Direct Grant tem limitações de MFA/federação e é desaconselhado pela orientação atual do provedor. [Keycloak — fluxos OIDC](https://www.keycloak.org/securing-apps/oidc-layers).

### 8.3 Integração no ASP.NET Core

Hoje `AdicionarSeguranca` exige `Jwt:SigningKey` e valida HS256 local. A mudança deve substituir esse modo por validação do issuer externo; não manter a exigência da chave simétrica quando o modo Keycloak estiver ativo. A API não precisa do segredo do cliente web para validar JWT por chaves públicas. [E03]

Exemplo conceitual, a adaptar ao projeto; não é implementação entregue nem configuração completa:

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = configuration["Authentication:Authority"];
        options.Audience = "gestao-eventos-api";
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.NameClaimType = "preferred_username";
        options.TokenValidationParameters.RoleClaimType = "app_role";
    });
```

`Authority` deve ser uma URL fixa e confiável do realm em HTTPS. A configuração deve ser validada na inicialização, incluindo audiência e modo de autenticação. Metadata/JWKS permitem localizar chaves públicas e acompanhar rotação; validar issuer, audience, assinatura e validade é obrigatório. Aceitar **access token**, não ID token. [Microsoft — JWT Bearer no ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0).

A claim `app_role` do exemplo **precisa ser produzida** por mapper restrito do Keycloak ou transformação local após validação criptográfica. Se usar transformação local, ler somente `resource_access["gestao-eventos-api"].roles`, aplicar allowlist e evitar duplicação em reexecuções. Configurar `RoleClaimType` com o nome de um objeto JSON aninhado não faz o ASP.NET extrair automaticamente suas roles.

Atualizar `CurrentUser`, que hoje consulta `ClaimTypes.Role`, para o contrato adotado. Não aproveitar indiscriminadamente roles de `realm-management`, `account` ou outro cliente. Manter políticas `Gestao`/`Administracao` para autorização ampla e acrescentar handlers por recurso para as regras de titularidade/escopo.

### 8.4 Identidade externa e preservação do histórico

O código atual converte `sub` diretamente em `Guid` e consulta `/usuarios/me` pela PK local. A identidade OIDC deve ser tratada como a combinação de issuer e subject, sem supor que subject seja o mesmo GUID histórico da aplicação. [E32; OpenID Connect — identificadores de subject](https://openid.net/specs/openid-connect-core-1_0.html#SubjectIDTypes).

Modelo lógico proposto:

```text
IdentidadeExterna
  Issuer            texto, comparado segundo o contrato do issuer
  Subject           texto, preservado exatamente
  UsuarioId         Guid interno já usado no domínio/auditoria
  UNIQUE (Issuer, Subject)

UsuarioAplicacao
  Id                Guid interno estável
  PessoaId          vínculo opcional e verificado para autosserviço
  EstaAtivo         autorização de uso desta aplicação
  ...               somente dados locais necessários
```

Resolver o vínculo após validar o token. Não promover usuário nem vincular contas automaticamente apenas por email; colisão ou alteração de email não comprova identidade. Um subject novo deve resultar em provisionamento controlado ou recusa clara, sem perder o ator da auditoria por conversão malsucedida.

O ator histórico deve continuar reconhecível após migração, troca de nome/email e exclusão da conta no provedor, respeitada a retenção definida. Keycloak não deve hospedar cadastros de Pessoas, inscrições ou certificados.

### 8.5 Mudanças concretas no repositório

| Área/arquivo atual | Alteração proposta | Motivo |
|---|---|---|
| `Shared.WebHost/ModularWebHostExtensions.cs` | Configuração OIDC/JWT externo, claims e ordem do pipeline | Validar provedor e aplicar políticas corretamente. |
| `Shared.WebHost/Security/CurrentUser.cs` e `ICurrentUser` | Contrato para identidade externa e ID interno resolvido | Preservar histórico e autorização sem depender de `Guid.Parse(sub)`. |
| `Module.Identidade` | Adaptar para conta local/vínculo e autorização da aplicação | Credenciais e sessão passam ao provedor. |
| `CriarSessao` e `TokenService` | Descontinuar após migração validada | Evitar emissores locais paralelos indefinidos. |
| `RegistrarUsuario` e `AtualizarPerfisUsuario` | Descontinuar ou adaptar conforme fonte autoritativa escolhida | Não realizar dupla escrita sem tratamento de falha parcial. |
| `ObterUsuarioAtual` | Retornar identidade local, permissões efetivas e contexto autorizado | Frontend não deve reconstruir autorização a partir de storage antigo. |
| Seed de administrador | Bootstrap explícito fora do fluxo normal | Evitar reelevação e senhas iniciais permanentes. |
| Front: auth-store/auth-context/login/http | Biblioteca OIDC, renovação, logout, limpeza de cache e tratamento de falhas | Substituir sessão local atual e melhorar comportamento entre identidades. |
| Testes de integração/contrato | Matriz de tokens, roles, vínculos e acesso a recursos | Impedir permissões cruzadas e regressões de migração. |
| Deploy/runbooks | Configuração versionada, segredos, backup, upgrades e monitoramento do IdP | Keycloak vira uma dependência operacional com dono. |

Se houver administração de usuários pelo produto, encapsular a Admin API em um adapter de Identidade com `HttpClient` tipado, credencial backend de privilégio mínimo, timeout e resultados normalizados. Criar usuário no provedor e vincular no banco local não é uma transação única: preferir administração inicial pelo console ou desenhar estado pendente e reconciliação idempotente. Não introduzir saga/broker apenas para ocultar esse problema.

### 8.6 SPA versus BFF

| Dimensão | SPA + PKCE — baseline proposta | BFF no Host.Api — alternativa |
|---|---|---|
| Token | Em memória no navegador | Armazenado no servidor; navegador usa identificador de sessão em cookie. |
| Complexidade | Menor mudança na topologia atual | Sessão, callback OIDC, CSRF, logout e armazenamento de tokens/sessão. |
| Ameaça principal | XSS pode agir como usuário e capturar tokens em memória | XSS ainda pode executar ações; cookie HttpOnly reduz extração do token, mas não elimina XSS. |
| Réplicas | API valida bearer localmente | Chaves Data Protection e estado de sessão compatíveis entre réplicas quando necessários. |
| Revogação | Exige política para token já emitido | Pode invalidar sessão local se o mecanismo for implementado; não é automático. |
| Quando escolher | Risco do browser aceito e requisitos de sessão atendidos | Política proíbe bearer no browser ou contas privilegiadas justificam sessão controlada. |

No BFF, não apenas serializar todos os tokens no cookie. Usar cookie `HttpOnly`, `Secure`, política `SameSite` compatível com o fluxo, defesa CSRF e armazenamento servidor protegido. Compartilhar segredos/chaves e sessão quando exigido pela topologia; isso não implica adotar Redis automaticamente.

### 8.7 Falhas do provedor e comportamento esperado

| Falha | Comportamento proposto | Controle/teste |
|---|---|---|
| Keycloak indisponível | Login e refresh podem falhar; token válido com chave já disponível pode continuar funcionando até expirar | Comunicar indisponibilidade; probe de login; não criar bypass de autenticação. |
| API reinicia sem metadata/chaves disponíveis | Token não validável é recusado | Teste com cache frio; distinguir falha de dependência nos sinais operacionais. |
| Rotação de chaves | Novos tokens usam nova chave; tokens antigos precisam do período de verificação compatível | Ensaio de rotação e atualização de JWKS; tratar chave desconhecida sem aceitar assinatura não verificada. |
| Role removida ou usuário bloqueado | Claims de token localmente válido não mudam sozinhas | Testar prazo de revogação; consulta adicional/sessão invalidável se necessário. |
| Provisionamento remoto parcialmente concluído | Estado local pendente/reconciliável; retry não cria contas duplicadas | Identidade externa única, correlação e recuperação explícita. |
| Refresh falha em várias chamadas simultâneas | Uma renovação coordenada, seguida de reautenticação se necessário | Evitar tempestade de renovação e loops de 401. |
| Admin API timeout/429 | Falha controlada, retry limitado apenas quando seguro | Respeitar quotas; verificar criação anterior antes de repetir escrita não idempotente. |

O provedor oferece discovery, JWKS, introspecção e logout OIDC; escolher o uso conforme a garantia necessária, em vez de consultar o Keycloak por rede em toda requisição sem um requisito. [Keycloak — endpoints OIDC](https://www.keycloak.org/securing-apps/oidc-layers).

### 8.8 Operação do Keycloak

Se já existir instalação corporativa, preferir integração com ela após verificar requisitos e ownership. Para instalação nova, a proposta mínima é imagem/versionamento controlados, modo de produção, hostname/TLS corretos, banco lógico e credencial próprios, backup/restore, administração restrita, configuração de realm/client sem segredos versionados e rotina de atualização. Pode compartilhar inicialmente o servidor PostgreSQL, aceitando falha/capacidade compartilhadas; não compartilhar tabelas do domínio. [Keycloak — produção](https://www.keycloak.org/server/configuration-production), [containers](https://www.keycloak.org/server/containers).

Publicar somente os endpoints necessários. Restringir administração e não expor a interface de gerenciamento/health/métricas à internet. Configurar proxies confiáveis e hostname de forma compatível com o issuer que a API valida. [Keycloak — reverse proxy](https://www.keycloak.org/server/reverseproxy).

Habilitar e coletar health/métricas, proteger o acesso e incluir alertas de disponibilidade, erros HTTP, latência de login, banco, CPU/memória/GC e eventos de autenticação/administração. Os nomes dos instrumentos e opções precisam ser conferidos na versão fixada. Métricas e eventos de segurança são sinais diferentes. [Keycloak — health](https://www.keycloak.org/observability/health), [métricas](https://www.keycloak.org/observability/configuration-metrics).

Alta disponibilidade deve ser escolhida com base no SLO de login e na capacidade operacional: duas réplicas sem banco, proxy, backup e configuração de cluster coerentes não resolvem disponibilidade. Não escalar o IdP a zero se a operação exigir login imediato. Testar restauração de identidades, roles e configuração, além dos dados do negócio.

### 8.9 Plano de migração de identidade

1. Aprovar matriz de acesso, titularidade, fonte de roles e prazo de revogação. Corrigir os controles de domínio independentemente do IdP.
2. Fixar versão do Keycloak e configuração de realm/client em ambiente de teste; validar audience, claims, MFA e redirects.
3. Criar vínculo externo → ID interno, preservando contas e autores históricos; ensaiar colisões e conta externa sem vínculo.
4. Definir estratégia de credenciais. Não presumir que hashes ASP.NET Identity possam ser copiados diretamente; preferir redefinição controlada ou migração/federação previamente testada, conforme experiência e continuidade exigidas.
5. Integrar frontend e API em homologação, com testes de login/refresh/logout, roles, recurso, rotação de chaves e indisponibilidade.
6. Se coexistência temporária for indispensável, usar schemes/issuers/audiences restritos, observabilidade por emissor e data de encerramento. Não aceitar indiscriminadamente ambos os tokens.
7. Migrar grupo piloto, reconciliar vínculos/roles, ensaiar recuperação e definir janela de corte. Rollback deve considerar contas alteradas no IdP e tokens ainda válidos, não apenas imagem anterior.
8. Encerrar emissão local, retirar chaves/segredos de bootstrap obsoletos, encerrar sessões antigas conforme plano e atualizar OpenAPI/testes/runbooks. Não excluir histórico sem a política de retenção aprovada.

## 9. Alternativas, debate e decisão do conselho

### 9.1 Alternativas materiais

| Critério | A — Monolito endurecido + Identity atual | B — Monolito endurecido + Keycloak/SPA PKCE | C — Monolito endurecido + Keycloak/BFF |
|---|---|---|---|
| Drivers atendidos | D1–D5/D7; D6 exige construir capacidades locais | D1–D7, sob requisitos de identidade propostos | D1–D7, com redução da exposição de tokens no browser |
| Lacuna principal | Operar MFA/recuperação/SSO e ciclo de tokens | Política de bearer no browser e revogação precisam ser aceitas | Estado de sessão, CSRF e réplicas precisam ser operados |
| Complexidade de desenvolvimento | Menor migração; maior responsabilidade sobre identidade própria | Migração de claims/vínculos/fluxos e autorização | Tudo de B, mais camada de sessão e callbacks |
| Operação/custo | API, banco e observabilidade | Acrescenta IdP, banco lógico, backup e suporte | Acrescenta também manutenção da sessão; pode ficar no host existente |
| Consistência | Transação local; auditoria eventual | Igual; provisionamento remoto não é atômico com banco local | Igual a B; sessão é outro estado a gerenciar |
| Falhas características | Chave simétrica/credencial própria e token antigo válido | Login/refresh/JWKS indisponíveis; claims/vínculos inconsistentes | Falhas de sessão/chaves compartilhadas e logout parcial |
| Segurança | Controles precisam ser implementados na aplicação | Credenciais no IdP; acesso ao domínio continua local | Tokens no servidor; cookies exigem defesa CSRF; XSS ainda importa |
| Reversibilidade | Evolução gradual para B/C possível | Preservar IDs internos facilita migração futura de IdP | Voltar para SPA muda contrato de sessão e frontend |

**Decisão proposta:** B para a evolução corporativa com Keycloak solicitada na análise, após os controles P0/P1 e confirmação dos requisitos. A é a baseline mais simples se centralização de identidade não for necessária. C é o gatilho de evolução quando o risco/requisito de sessão justificar. Nenhuma alternativa dispensa corrigir autorização por recurso, concorrência e auditoria.

Para processamento, manter a Outbox no `Host.Api` inicialmente. A alternativa API + Worker ganha sentido se for necessário processar com API parada/escala zero, se jobs competirem por recursos ou se medições mostrarem necessidade de escala/implantação independentes. Extrair Worker demanda compor produtores/stores, consumidores e dependências; registrar apenas módulos com handlers não garante leitura de todas as Outboxes.

### 9.2 Objeções independentes e respostas

| Especialidade | Decisão questionada / gravidade | Objeção | Resposta incorporada |
|---|---|---|---|
| Segurança | “Keycloak resolve segurança” / alta | Autenticação externa mantém acesso indevido a recursos e tokens antigos com roles | Corrigir domínio antes/junto da migração; prazo de revogação explícito; fonte única de permissões. |
| Segurança | SPA como baseline / média/alta conforme risco | Contas administrativas e dados pessoais podem justificar tokens no servidor | SPA PKCE sob premissa explícita; BFF co-localizado quando política/risco exigir, com custo de sessão e CSRF registrado. |
| .NET | “Mesmo processo significa uma transação” / alta | DbContexts e verificações separadas permitem corridas | Documentar limites, definir invariantes e coordenar leitura/decisão/gravação quando necessário. |
| .NET | “Adicionar lock ou versão resolve” / alta | Lock tardio/local à memória ou token do pai sem atualização não protege a regra | Locks transacionais abrangentes, participação de comandos relacionados e testes com múltiplas conexões/réplicas. |
| .NET | “DbContext novo resolve retry” / alta | Não resolve sozinho commit indeterminado e duplicação de eventos | IDs estáveis, verificação de sucesso e interceptor compatível com replay fazem parte da correção. |
| Integração | “Claim evita duplicação de entrega” / alta | Lease expira e queda pode acontecer após efeito, antes do ack | Ownership/fencing no claim e efeito idempotente; assumir reentrega. |
| Integração | “SKIP LOCKED/Worker resolve Outbox” / alta | Nenhum elimina a janela efeito → ack | Corrigir garantias antes da extração; sem broker obrigatório. |
| Observabilidade | “Health e latência mostram tudo” / alta | Processador parado deixa de gerar amostras e banco continua saudável | Backlog/idade/heartbeat, sinal de ausência de dados e alerta separado. |
| Revisão final | “Runbook é configuração pronta” / alta | O merge efetivo preserva portas que o exemplo pretende remover | Validação executável de configuração produtiva e teste de acessibilidade externa. |

### 9.3 Topologia final recomendada

```text
                       Keycloak / IdP
                      (login, MFA, SSO)
                         |         |
                Code+PKCE|         | metadata / JWKS
                         v         v
Navegador React -- HTTPS/ingress --> Host.Api (.NET 10)
                                    |-- valida JWT e resolve identidade local
                                    |-- autoriza recurso/campo/escopo
                                    |-- módulos e contratos existentes
                                    |-- Outbox BackgroundService
                                    `-- auditoria idempotente
                                             |
                                  PostgreSQL da aplicação
                                  schemas por módulo

Keycloak ------------------------> banco lógico próprio do IdP

API + IdP + infraestrutura ------> telemetria persistente
                                   painéis + alertas + runbooks

Administração, bancos, OTLP e gerenciamento: acesso restrito.
Opcional futuro: BFF no host e/ou Worker independente por driver medido.
```

A separação física dos bancos/hosts e quantidade de réplicas depende de disponibilidade, carga, isolamento e orçamento. O diagrama representa responsabilidades e fronteiras de confiança, não um dimensionamento fechado.

### 9.4 Rastreabilidade das decisões

| Decisão | Drivers | Alternativa considerada | Trade-off | Confiança / gatilho de revisão |
|---|---|---|---|---|
| Manter monolito modular | D1, D3, D7 | Microsserviços | Recursos e deploy compartilhados | Alta; revisar com ownership/ciclo de deploy ou escala independente comprovados. |
| Preservar Outbox no banco | D4 | Chamar auditoria apenas depois do commit sem persistir intenção | Storage, polling, reentrega e retenção | Alta; revisar com consumidores externos/burst que exijam outro transporte. |
| Fortalecer concorrência no banco/unidade transacional | D3 | Validação apenas em memória | Locks/conflitos e menor paralelismo no mesmo recurso | Alta quanto à necessidade; mecanismo depende da regra/carga. |
| Autorizar por recurso e minimizar campos | D2 | Apenas roles amplas | Consultas de vínculo/escopo e testes adicionais | Alta; revisar modelo com participantes/organizações confirmados. |
| Keycloak + OIDC | D6 | Identity endurecido | Serviço e migração adicionais | Média, condicionada a requisitos de SSO/MFA e capacidade de operação. |
| Vínculo `(issuer, subject)` para ID interno | D2, D4, D6 | Reutilizar email ou converter subject em GUID local | Tabela e resolução de identidade | Alta; revisar apenas por contrato corporativo diferente devidamente demonstrado. |
| SPA PKCE inicialmente | D1, D6 | BFF | Tokens continuam no contexto do browser | Média; revisar se política proibir token no browser ou sessão privilegiada exigir controle adicional. |
| Telemetria persistente e alertas | D5, D7 | Apenas Aspire de desenvolvimento | Custo de coleta, retenção e operação | Alta; backend/sampling dependem de orçamento e plataforma existente. |
| Migrações/restore ensaiados | D7 | Migrar sempre na subida e confiar no backup gerado | Etapa de entrega e rotina operacional | Alta; automação proporcional à criticidade. |

### 9.5 Componentes rejeitados ou adiados

| Componente/padrão | Decisão e motivo | Fato que justificaria reconsiderar |
|---|---|---|
| Microsserviços por módulo | Rejeitar agora: multiplicam falhas de rede e consistência sem resolver os achados | Times/lifecycles, escala ou isolamento operacional independentes. |
| Kafka/RabbitMQ | Adiar: Outbox + consumidor local atende à auditoria atual | Integrações externas, fan-out relevante ou volume medido que exija transporte dedicado. |
| Worker independente | Adiar: operação conjunta é mais simples | Competição de CPU/pool, tarefas longas, escala zero da API ou SLA próprio. |
| Redis | Não introduzir por antecipação | Cache medido, sessão compartilhada ou coordenação distribuída com requisito concreto. |
| Saga/distributed transaction | Rejeitar para os fluxos locais atuais | Processo de negócio realmente distribuído, com compensações e múltiplos donos de dados. |
| MediatR/CQRS/repositório genérico | Não adicionar: casos de uso e EF direto já expressam as responsabilidades | Complexidade demonstrada que a abstração reduza, com custo justificado. |
| Kubernetes/service mesh/mTLS entre módulos | Rejeitar como resposta aos problemas atuais | Plataforma organizacional ou requisitos de operação/rede que os demandem. |
| WAF/API gateway obrigatório | Não presumir necessidade | Ameaça de borda, gestão de múltiplas APIs ou controle global que o ingress existente não atenda. |
| Keycloak Authorization Services para toda regra | Adiar: ownership e vínculo de domínio já residem na API | Política centralizada entre vários sistemas com governança definida. |
| Inbox universal | Não exigir para Auditoria se PK + inserção atômica bastarem | Consumidores com múltiplos efeitos ou integração externa que precisem de deduplicação própria. |
| Vários backends de telemetria simultâneos | Evitar como padrão | Necessidade de migração, auditoria operacional ou consumidores com requisitos distintos. |

## 10. Plano de ação verificável

Estimativas de esforço são relativas: P = mudança localizada; M = envolve componentes/testes; G = envolve domínio, migração ou operação. Não representam prazo ou compromisso de equipe.

| Etapa / ID | Entrega | Dependências | Responsável sugerido | Esforço | Aceite principal |
|---|---|---|---|---|---|
| 0 / A01 | Configuração produtiva sem defaults e merge seguro | Nenhuma | Plataforma + backend | P/M | Config renderizada só publica ingress; startup recusa ausência de segredo. |
| 0 / A02 | Proxies confiáveis e rate limiting coerente | A01 | Backend + plataforma | M | IP não falsificável; quotas por identidade validada. |
| 0 / A03 | Bootstrap administrativo único | Nenhuma | Backend | P | Reinício não restaura role removida. |
| 1 / A04 | Matriz de acesso, vínculo usuário–pessoa e políticas por recurso | Decisões de atores/escopo | Produto + backend + segurança | G | Testes cruzados A/B e projeções de dados aprovados. |
| 1 / A05 | Cache/sessão isolados no frontend | Contrato de identidade | Frontend | M | Troca de conta não mostra dados da anterior nem preserva requests ativos. |
| 1 / A06 | Revogação e auditoria de permissões | Prazo de revogação | Backend + segurança | M/G | Token antigo perde acesso no prazo; trilha registra antes/depois/ator. |
| 1 / A07 | Retry transacional correto | Nenhuma | Backend .NET | G | Fault injection mantém negócio/Outbox/auditoria coerentes. |
| 1 / A08 | Concorrência de inscrição, agenda, certificado e agregados | Regras de capacidade/histórico; A07 | Backend + produto | G | Testes simultâneos preservam invariantes e contrato HTTP. |
| 1 / A09 | Claim/lease e consumidor idempotente | A07 | Backend | M/G | Crash/reentrega/ack tardio não duplicam efeito. |
| 1 / A10 | Métricas de backlog, idade e heartbeat | A09 pode evoluir em paralelo | Backend + operação | M | Processador parado com pendências dispara alerta. |
| 1 / A11 | Destino persistente, painéis e alertas com runbooks | Escolha da plataforma | Operação + backend | M | Três sinais chegam; alerta simulado aciona responsável. |
| 2 / A12 | Contrato de identidade e piloto Keycloak | A04/A06; decisão de IdP | Backend + frontend + segurança | G | JWT/roles/vínculos/SSO/MFA e indisponibilidade testados. |
| 2 / A13 | Migração e corte do emissor local | A12; ensaio de rollback | Equipe de produto + operação | G | Histórico preservado; emissores antigos encerrados no prazo. |
| 2 / A14 | Retenção, minimização e ciclo dos históricos | Política de dados | Produto + segurança + backend | G | Registro sintético tratado em banco, Outbox, auditoria e cópias conforme política. |
| 2 / A15 | Migrações, restore, supply chain e contratos no pipeline | Ambiente de entrega | Plataforma + backend | M/G | Upgrade/restore comprovados; checks bloqueiam incompatibilidades conhecidas. |

A troca do provedor pode ser desenvolvida em paralelo a alguns itens, mas não deve adiar a correção de acesso indevido ou transformar falhas existentes em responsabilidade atribuída ao Keycloak.

## 11. Validação realizada e testes ainda necessários

### 11.1 Resultado observado nesta análise

Comandos executados a partir de `api/`:

```bash
dotnet restore GestaoEventos.slnx --verbosity quiet
dotnet test GestaoEventos.slnx --no-restore --verbosity quiet -p:WarningLevel=0
```

| Projeto | Aprovados | Falhas | Ignorados |
|---|---:|---:|---:|
| Tests.Unit | 288 | 0 | 0 |
| Tests.Architecture | 42 | 0 | 0 |
| Tests.Integration | 16 | 0 | 0 |
| Tests.Functional | 39 | 0 | 0 |
| **Total** | **385** | **0** | **0** |

SDK utilizado: 10.0.401; runtime disponível 10.0.12. A primeira tentativa sem restore encontrou assets ausentes nos projetos Integration/Functional. Depois do restore, a execução completa terminou com código 0. O build inicial exibiu warnings; a execução final silenciou sua apresentação com `WarningLevel=0`. Portanto, este resultado não significa build livre de warnings.

O teste de configuração do runbook VPS foi somente renderização, sem `up`/deploy. A saída foi filtrada para portas e indicadores, sem incluir valores de segredos no relatório.

### 11.2 Suíte prioritária a acrescentar

| Grupo | Cenários obrigatórios propostos |
|---|---|
| Autorização | Participante A/B; organizador dentro/fora do escopo; cadastro completo/projeção; 401 para token inválido e 403 para autenticado sem permissão. |
| JWT/Keycloak | Issuer/audience errados, token expirado, assinatura inválida, ID token apresentado como access token, role de outro cliente, subject textual, conta sem vínculo, conta desativada. |
| Sessão | Logout, refresh concorrente, retirada de role, bloqueio, prazo de revogação, cache entre identidades e requisição tardia de sessão anterior. |
| Administração | Bootstrap não reeleva; grant/revoke produz trilha completa; token/segredo não entra em log/auditoria. |
| Concorrência | Última vaga, reserva de sala, criação/alteração paralelas, certificado, transição de estado e remoção dos últimos filhos. |
| Resiliência | Falha entre Save/Commit, commit indeterminado, queda após handler antes do ack, expiração de lease, duas réplicas e fan-out parcialmente concluído. |
| Observabilidade | Correlação HTTP/SQL/Outbox; chegada dos três sinais; processador parado; backend de coleta fora; ausência de telemetria; labels sem PII. |
| Operação | Config final sem portas internas, restore, migração da versão anterior, rollback compatível, rotação de chaves, Keycloak com cache frio/JWKS indisponível. |
| Contratos | OpenAPI com schemas/tipos/segurança/respostas e compatibilidade de payload de eventos entre versões. |

Os 385 testes atuais não substituem esses cenários. Não foram escritos novos testes nesta entrega, porque o pedido é análise e documentação. Testes arquiteturais já existem e passaram; a melhoria é ampliá-los quando necessário e garantir execução na entrega. O teste OpenAPI atual compara operações, não toda a compatibilidade dos contratos. [E33]

## 12. Riscos residuais, confiança e gatilhos

Mesmo após as correções propostas, o monolito continuará compartilhando processo, recursos e ciclo de deploy. Um PostgreSQL físico compartilhado com o IdP compartilha também capacidade e parte dos cenários de indisponibilidade. A Outbox mantém auditoria eventual, sujeita a atraso; precisa de prazo e mecanismo de intervenção. SPA mantém risco de ações maliciosas em caso de XSS; BFF reduz exposição de tokens, mas não elimina esse risco.

**Confiança alta:** leitura do código, identificação dos defaults e do merge de portas, ausência de unicidade do certificado, ordem do pipeline, lacunas de autorização, bootstrap e resultado dos testes. **Confiança média:** manifestação e impacto exato das falhas transacionais/concorrentes sem reprodução direcionada; escolha SPA/BFF; dimensionamento e operação do provedor. **Não avaliado:** estado da produção real e requisitos legais/organizacionais finais.

Reabrir as decisões quando houver:

- Múltiplas organizações com isolamento: revisar modelo de autorização/dados antes de apenas adicionar claim de tenant.
- Prazo de revogação menor que a vida do access token: acrescentar mecanismo online/estado de sessão apropriado.
- Proibição de token no navegador: escolher BFF e planejar sessões/CSRF/chaves.
- Keycloak corporativo disponível: preferir integração e rever responsabilidade de operação.
- Backlog ou consumo da Outbox prejudicando a API: medir e considerar Worker independente.
- Necessidade de consumidores externos, grandes bursts ou retenção/replay fora do banco: reavaliar broker/transporte.
- Escala/ciclo de entrega/isolamento independentes por domínio: avaliar extração de módulo com custo de contratos, dados e consistência.
- Certificados com valor histórico formal: aprovar snapshot e retenção; não depender exclusivamente de cadastros mutáveis.
- SLO/RPO/RTO incompatível com um único host/banco: rever disponibilidade e recuperação com ensaios e orçamento.

## 13. Catálogo de evidências do snapshot

Os links são relativos a este arquivo em Downloads. Os números de linha se referem ao snapshot analisado e podem mudar se o código for editado. Os valores de segredos demonstrativos não foram transcritos.

| Ref. | Arquivo / trecho relevante |
|---|---|
| E01 | [docker-compose.yml](ties-gestao-eventos-main/docker-compose.yml): publicações, dashboard anônimo e configuração da API; linhas 17–58. |
| E02 | [Runbook VPS](ties-gestao-eventos-main/docs/runbooks/deploy-vps.md): bloco de override, linhas 30–64; merge confirmado por `docker compose config`. |
| E03 | [ModularWebHostExtensions.cs](ties-gestao-eventos-main/api/src/shared/Shared.WebHost/ModularWebHostExtensions.cs): proxies 70–75; pipeline 89–118; JWT/políticas 128–158; limiter 161–182; health 196–200. |
| E04 | [ObterPessoaEndpoint.cs](ties-gestao-eventos-main/api/src/modules/Module.Pessoas/UseCases/ObterPessoa/ObterPessoaEndpoint.cs): linhas 12–18; [Response](ties-gestao-eventos-main/api/src/modules/Module.Pessoas/UseCases/ObterPessoa/ObterPessoaResponse.cs): campos expostos. |
| E05 | [InscreverParticipanteUseCase.cs](ties-gestao-eventos-main/api/src/modules/Module.Eventos/UseCases/InscreverParticipante/InscreverParticipanteUseCase.cs): leitura/contagem/decisão 27–78; persistência 88–100; [Endpoint](ties-gestao-eventos-main/api/src/modules/Module.Eventos/UseCases/InscreverParticipante/InscreverParticipanteEndpoint.cs): autenticação e contrato. |
| E06 | [CancelarInscricaoEndpoint.cs](ties-gestao-eventos-main/api/src/modules/Module.Eventos/UseCases/CancelarInscricao/CancelarInscricaoEndpoint.cs): linhas 12–23; [UseCase](ties-gestao-eventos-main/api/src/modules/Module.Eventos/UseCases/CancelarInscricao/CancelarInscricaoUseCase.cs): operação por IDs sem titularidade. |
| E07 | [EmitirCertificadoUseCase.cs](ties-gestao-eventos-main/api/src/modules/Module.Palestras/UseCases/EmitirCertificado/EmitirCertificadoUseCase.cs): linhas 18–46; [Endpoint](ties-gestao-eventos-main/api/src/modules/Module.Palestras/UseCases/EmitirCertificado/EmitirCertificadoEndpoint.cs): contrato 200/201 e acesso, linhas 13–31. |
| E08 | [JwtOptions.cs](ties-gestao-eventos-main/api/src/shared/Shared.WebHost/Security/JwtOptions.cs): linha 10; [TokenService.cs](ties-gestao-eventos-main/api/src/modules/Module.Identidade/Shared/Seguranca/TokenService.cs): claims e emissão. |
| E09 | [AtualizarPerfisUsuarioUseCase.cs](ties-gestao-eventos-main/api/src/modules/Module.Identidade/UseCases/AtualizarPerfisUsuario/AtualizarPerfisUsuarioUseCase.cs): diferenças de vínculos, ConcurrencyStamp e persistência, linhas 49–80. |
| E10 | [IdentidadeSeedHostedService.cs](ties-gestao-eventos-main/api/src/modules/Module.Identidade/Shared/Seed/IdentidadeSeedHostedService.cs): linhas 63–95. |
| E11 | [AuditoriaSaveChangesInterceptor.cs](ties-gestao-eventos-main/api/src/shared/Shared.Data/Interceptors/AuditoriaSaveChangesInterceptor.cs): estados 65–83; chave 117; snapshots/marcação sensível 120–146. |
| E12 | [auth-store.ts](ties-gestao-eventos-main/front/src/shared/auth/auth-store.ts): linhas 97–114; [App.tsx](ties-gestao-eventos-main/front/src/App.tsx): linhas 33–41; [query-keys.ts](ties-gestao-eventos-main/front/src/shared/api/query-keys.ts); [nginx.conf](ties-gestao-eventos-main/front/nginx.conf). |
| E13 | [DataServiceCollectionExtensions.cs](ties-gestao-eventos-main/api/src/shared/Shared.Data/DataServiceCollectionExtensions.cs): lifetimes, pooling, retry e registro de stores, linhas 19–54. |
| E14 | [PessoaConfiguration.cs](ties-gestao-eventos-main/api/src/modules/Module.Pessoas/Shared/Configuracoes/PessoaConfiguration.cs): linhas 13–22; [UsuarioConfiguration.cs](ties-gestao-eventos-main/api/src/modules/Module.Identidade/Shared/Configuracoes/UsuarioConfiguration.cs): linhas 21–23; [IdentityTabelasConfiguration.cs](ties-gestao-eventos-main/api/src/modules/Module.Identidade/Shared/Configuracoes/IdentityTabelasConfiguration.cs): linhas 34–41. |
| E15 | [CriarSessaoUseCase.cs](ties-gestao-eventos-main/api/src/modules/Module.Identidade/UseCases/CriarSessao/CriarSessaoUseCase.cs): respostas 31–55; [regras de Identidade](ties-gestao-eventos-main/docs/business-rules/identidade.md): usuário/pessoa e RN-IDT-006. |
| E16 | [DbContextTransactionExtensions.cs](ties-gestao-eventos-main/api/src/shared/Shared.Data/Extensions/DbContextTransactionExtensions.cs): linhas 13–34; [AtualizarEventoUseCase.cs](ties-gestao-eventos-main/api/src/modules/Module.Eventos/UseCases/AtualizarEvento/AtualizarEventoUseCase.cs): mutação e transação. |
| E17 | [Regras de Eventos](ties-gestao-eventos-main/docs/business-rules/eventos.md): RN-EVT-013/014 e linha 75; [InscricaoConfiguration.cs](ties-gestao-eventos-main/api/src/modules/Module.Eventos/Shared/Configuracoes/InscricaoConfiguration.cs): índice pessoa/evento. |
| E18 | [CriarPalestraUseCase.cs](ties-gestao-eventos-main/api/src/modules/Module.Palestras/UseCases/CriarPalestra/CriarPalestraUseCase.cs): consulta 30–40 e transação 70–75; [AtualizarPalestraUseCase.cs](ties-gestao-eventos-main/api/src/modules/Module.Palestras/UseCases/AtualizarPalestra/AtualizarPalestraUseCase.cs); [PalestraConfiguration.cs](ties-gestao-eventos-main/api/src/modules/Module.Palestras/Shared/Configuracoes/PalestraConfiguration.cs). |
| E19 | [CertificadoConfiguration.cs](ties-gestao-eventos-main/api/src/modules/Module.Palestras/Shared/Configuracoes/CertificadoConfiguration.cs): linhas 14–15; [Regras de Palestras](ties-gestao-eventos-main/docs/business-rules/palestras.md): RN-PAL-021 e unicidade. |
| E20 | [AlterarSituacaoEventoUseCase.cs](ties-gestao-eventos-main/api/src/modules/Module.Eventos/UseCases/AlterarSituacaoEvento/AlterarSituacaoEventoUseCase.cs); [ExcluirSalaUseCase.cs](ties-gestao-eventos-main/api/src/modules/Module.Locais/UseCases/ExcluirSala/ExcluirSalaUseCase.cs); [ExcluirTrilhaUseCase.cs](ties-gestao-eventos-main/api/src/modules/Module.Eventos/UseCases/ExcluirTrilha/ExcluirTrilhaUseCase.cs); [EntidadeBase.cs](ties-gestao-eventos-main/api/src/shared/Shared.Data/Entidades/EntidadeBase.cs). |
| E21 | [AgendaPalestraVerificador.cs](ties-gestao-eventos-main/api/src/modules/Module.Palestras/Shared/AgendaPalestraVerificador.cs): consultas entre módulos, linhas 24–76; E05/E16 para capacidade e alterações de evento. |
| E22 | [ValidarCertificadoUseCase.cs](ties-gestao-eventos-main/api/src/modules/Module.Palestras/UseCases/ValidarCertificado/ValidarCertificadoUseCase.cs): resolução dos dados atuais; [PessoasModuleApi.cs](ties-gestao-eventos-main/api/src/modules/Module.Pessoas/Shared/PessoasModuleApi.cs): filtro ativo/não excluído. |
| E23 | [OutboxStore.cs](ties-gestao-eventos-main/api/src/shared/Shared.Data/Outbox/OutboxStore.cs): claim 15–47; ack/nack 50–76; contagem 79–85. |
| E24 | [OutboxProcessor.cs](ties-gestao-eventos-main/api/src/shared/Shared.Messaging/OutboxProcessor.cs): métricas 26–28; processamento 69–98; [OutboxOptions.cs](ties-gestao-eventos-main/api/src/shared/Shared.Messaging/OutboxOptions.cs); [OutboxMessage.cs](ties-gestao-eventos-main/api/src/shared/Shared.Data/Outbox/OutboxMessage.cs). |
| E25 | [EntidadeAlteradaHandler.cs](ties-gestao-eventos-main/api/src/modules/Module.Auditoria/Shared/Handlers/EntidadeAlteradaHandler.cs): linhas 19–37; [RegistroAuditoria.cs](ties-gestao-eventos-main/api/src/modules/Module.Auditoria/Domain/RegistroAuditoria.cs): ID do evento. |
| E26 | [InProcessIntegrationEventPublisher.cs](ties-gestao-eventos-main/api/src/shared/Shared.Messaging/InProcessIntegrationEventPublisher.cs): sem handler 24–29 e fan-out; [IntegrationEventTypeRegistry.cs](ties-gestao-eventos-main/api/src/shared/Shared.Messaging/IntegrationEventTypeRegistry.cs): nomes CLR. |
| E27 | [DatabaseMigrationHostedService.cs](ties-gestao-eventos-main/api/src/shared/Shared.Data/Migracao/DatabaseMigrationHostedService.cs); [runbook de migrações](ties-gestao-eventos-main/docs/runbooks/migracoes-banco.md); [deploy Azure](ties-gestao-eventos-main/docs/runbooks/deploy-azure.md). |
| E28 | [Runbook VPS](ties-gestao-eventos-main/docs/runbooks/deploy-vps.md): backup/restore e operação; [Directory.Build.props](ties-gestao-eventos-main/api/Directory.Build.props); [Directory.Packages.props](ties-gestao-eventos-main/api/Directory.Packages.props); [global.json](ties-gestao-eventos-main/api/global.json). |
| E29 | [ObservabilityExtensions.cs](ties-gestao-eventos-main/api/src/shared/Shared.Observability/ObservabilityExtensions.cs): versão/resource 30–60; instrumentação/exportação 61–94; logs HTTP 100–119. |
| E30 | [ModuleTelemetry.cs](ties-gestao-eventos-main/api/src/shared/Shared.Observability/Telemetria/ModuleTelemetry.cs); [TelemetryUseCaseDecorator.cs](ties-gestao-eventos-main/api/src/shared/Shared.Http/Endpoints/TelemetryUseCaseDecorator.cs); [UseCaseLogContext.cs](ties-gestao-eventos-main/api/src/shared/Shared.Http/Endpoints/UseCaseLogContext.cs). |
| E31 | [CorrelationIdMiddleware.cs](ties-gestao-eventos-main/api/src/shared/Shared.Observability/Middleware/CorrelationIdMiddleware.cs): linhas 14–26; [http.ts do frontend](ties-gestao-eventos-main/front/src/shared/api/http.ts): cabeçalhos e falhas de sessão. |
| E32 | [CurrentUser.cs](ties-gestao-eventos-main/api/src/shared/Shared.WebHost/Security/CurrentUser.cs): linhas 12–17; [ICurrentUser.cs](ties-gestao-eventos-main/api/src/shared/Shared.Contracts/Common/ICurrentUser.cs); [ObterUsuarioAtualUseCase.cs](ties-gestao-eventos-main/api/src/modules/Module.Identidade/UseCases/ObterUsuarioAtual/ObterUsuarioAtualUseCase.cs): lookup local 14–24. |
| E33 | [ModuleBoundaryTests.cs](ties-gestao-eventos-main/api/tests/Tests.Architecture/ModuleBoundaryTests.cs); [OpenApiContractTests.cs](ties-gestao-eventos-main/api/tests/Tests.Integration/Contratos/OpenApiContractTests.cs); [ApiFactory.cs](ties-gestao-eventos-main/api/tests/Tests.Integration/Infra/ApiFactory.cs); [GestaoEventos.slnx](ties-gestao-eventos-main/api/GestaoEventos.slnx). |
| E34 | [README](ties-gestao-eventos-main/README.md); [AGENTS.md](ties-gestao-eventos-main/AGENTS.md); [arquitetura](ties-gestao-eventos-main/docs/spec/arquitetura.md); [segurança](ties-gestao-eventos-main/docs/spec/seguranca.md); [observabilidade](ties-gestao-eventos-main/docs/spec/observabilidade.md); [ADR do monolito](ties-gestao-eventos-main/docs/adr/0001-monolito-modular.md). |

As referências externas foram consultadas em 20/09/2026 e estão junto das recomendações que sustentam. Nomes/opções do Keycloak e dos exporters devem ser validados na versão escolhida para a implementação. O roadmap descreve trabalho futuro; esta entrega contém somente análise e documentação.
