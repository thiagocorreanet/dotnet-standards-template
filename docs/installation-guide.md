# Guia de instalação e reutilização da base .NET

Este guia cria um projeto independente a partir do **Modular API Template**. Os exemplos utilizam `BillingApi` como nome da solução e `billing-api` como pasta. Substitua esses nomes pelos do seu produto.

A base usa código e contratos em inglês; mensagens, comentários e documentação em pt-BR. A instalação descrita é local, para desenvolvimento. Não é um procedimento de deploy produtivo.

## 1. O que será criado

A geração padrão entrega `Host.Api`, os módulos `Module.Identity` e `Module.Audit`, bibliotecas `Shared.*`, testes, Scalar, PostgreSQL, Keycloak/OIDC, Outbox, auditoria, observabilidade, scripts e workflows de CI.

Os módulos de exemplo `Module.Venues`, `Module.People`, `Module.Events` e `Module.Talks` só acompanham o projeto quando você escolhe `--includeExample true`.

O novo projeto recebe uma **cópia do código**, sem referência de execução à pasta do template. A partir daí, suas alterações são independentes. Atualizar o template não atualiza automaticamente projetos já gerados; melhorias futuras precisam ser comparadas e incorporadas conscientemente.

Não são copiados `.env`, `.local/`, `.secrets/`, `.git/`, `bin/`, `obj/`, resultados de testes, artefatos, documentação histórica de origem nem `.template.config/`. Assim, o projeto novo não se torna automaticamente outro template instalável. Mantenha a base original como fonte das próximas gerações.

## 2. Pré-requisitos

Instale previamente:

- SDK .NET indicado em `api/global.json` da base: atualmente `10.0.401`, com `rollForward: latestPatch`.
- Docker Engine ou Docker Desktop, ativo e acessível pelo seu usuário, com Docker Compose v2.
- Node.js 22 ou superior, para os scripts `.mjs`.
- OpenSSL para validações locais de infraestrutura; Git, caso vá versionar o projeto.
- Acesso à internet para restaurar pacotes NuGet e baixar as imagens Docker.

Confira no terminal:

```bash
dotnet --list-sdks
docker version
docker compose version
node --version
openssl version
git --version
```

Os testes de integração e funcionais criam PostgreSQL isolado via Testcontainers. Eles precisam do Docker, mas não exigem que a stack Compose esteja ligada. Nenhum banco existente deve ser usado como banco de teste.

As portas padrão da stack são `5761`, `8080`, `55432`, `3000`, `9090` e `4317`. Garanta que estejam livres. Projetos simultâneos precisam de portas distintas e ajustes coerentes nos scripts/configurações; o nome Compose separado não resolve conflito de portas.

## 3. Instalar o template no .NET CLI

Na máquina onde a base foi entregue:

```bash
dotnet new install /home/thiago-botelho/Documentos/developer/thiago/modular-api-template
dotnet new list modular-api
dotnet new modular-api --help
```

Em outra máquina, primeiro copie ou clone o **repositório da base**, incluindo os arquivos ocultos de configuração, mas excluindo segredos e artefatos locais. Então execute `dotnet new install /caminho/da/base`.

Se já instalou uma versão anterior e precisa atualizar o registro:

```bash
dotnet new install /home/thiago-botelho/Documentos/developer/thiago/modular-api-template --force
```

Esse comando atualiza o template disponível para novas gerações. Não altera projetos que você já criou nem migra seus bancos.

## 4. Gerar um projeto novo

Use uma pasta de destino nova, vazia e separada da base. Não use `--force` para gerar por cima de um projeto existente.

### Opção recomendada: base genérica, sem domínio de eventos

```bash
cd /home/thiago-botelho/Documentos/developer/thiago
dotnet new modular-api -n BillingApi -o billing-api
cd billing-api
```

### Opção alternativa: incluir o exemplo completo

Em vez do comando anterior, execute:

```bash
cd /home/thiago-botelho/Documentos/developer/thiago
dotnet new modular-api -n BillingApi -o billing-api --includeExample true
cd billing-api
```

Escolha apenas uma modalidade para a mesma pasta. Para comparar as duas, gere nomes e destinos diferentes.

A solução será `api/BillingApi.slnx`; o serviço será `BillingApi.Api` e a chave da conexão será `ConnectionStrings:BillingApi`. Nomes genéricos como `Host.Api`, `Module.Identity` e `Shared.Data` permanecem. Realm e clients OIDC não são renomeados automaticamente com `-n`.

## 5. Restaurar, compilar e executar os testes

Na raiz do projeto novo:

```bash
cd api
dotnet --version
dotnet tool restore
dotnet restore BillingApi.slnx
dotnet build BillingApi.slnx --no-restore
dotnet test BillingApi.slnx --no-restore
cd ..
```

O `global.json` deve selecionar o SDK esperado. O restore inicial pode atualizar lockfiles para o projeto renomeado; versione os `packages.lock.json` resultantes. Depois disso, o CI usa `dotnet restore api/BillingApi.slnx --locked-mode` para detectar divergências.

As quatro suítes são `Tests.Unit`, `Tests.Architecture`, `Tests.Integration` e `Tests.Functional`. A modalidade genérica tem menos testes porque não contém o domínio opcional.

## 6. Preparar credenciais e infraestrutura local

Execute uma única vez na raiz do projeto novo:

```bash
node scripts/init-local.mjs
```

O script cria `.env` com permissão restrita e senhas aleatórias, arquivos privados em `.local/` e uma configuração local do realm. Para `BillingApi`, o nome Compose será `billingapi-local`. Não compartilhe esse nome com outra cópia do mesmo projeto que já use volumes próprios.

Não copie `.env`, `.local/` nem volumes da base. Não inclua esses arquivos em Git, tickets, mensagens ou imagens Docker. O script recusa sobrescrever `.env`; isso protege as credenciais ligadas aos volumes existentes.

Suba a stack com observabilidade:

```bash
docker compose -f compose.local.yaml --profile observability up --build -d
node scripts/wait-local.mjs
node scripts/bootstrap-local.mjs
docker compose -f compose.local.yaml --profile observability ps -a
```

O job `migrate` aplica os schemas e concede permissões limitadas à identidade runtime. `migrate` e `telemetry-init` encerrados com código `0` são esperados: são jobs, não serviços permanentes.

O bootstrap cria o vínculo interno do usuário de desenvolvimento após a importação do realm. Ele só funciona com a tabela de usuários da API vazia; não serve para recuperar acesso de um administrador existente.

Se a inicialização exceder o prazo, examine o estado dos serviços e aguarde a importação. Repita readiness/bootstrap somente quando a etapa correspondente ainda não tiver concluído. Não regenere senhas nem apague volumes como primeira tentativa de correção.

## 7. Acessar os serviços

| Serviço | Endereço | Acesso local |
|---|---|---|
| Scalar | `http://localhost:5761/scalar` | UI de desenvolvimento |
| OpenAPI JSON | `http://localhost:5761/openapi/v1.json` | Contrato gerado pela API |
| Readiness | `http://localhost:5761/health/ready` | Deve responder `200` |
| Keycloak | `http://identity.localhost:8080` | Administrador `bootstrap-admin` |
| Grafana | `http://localhost:3000` | Usuário `operator` |
| Prometheus | `http://localhost:9090` | Interface local |
| PostgreSQL | `127.0.0.1:55432` | Somente desenvolvimento |

Consulte as credenciais no arquivo privado `.env`: `KEYCLOAK_ADMIN_PASSWORD`, `GRAFANA_PASSWORD` e `DEV_ADMIN_PASSWORD`. Abra-o localmente com cuidado; não publique o conteúdo. O usuário de demonstração autenticado na API é `developer`.

Se `identity.localhost` não resolver no seu navegador, configure esse nome para `127.0.0.1` na resolução local. Não troque somente a URL do login: o issuer configurado na API e o emitido pelo Keycloak devem coincidir.

A API não emite tokens nem oferece login/senha próprios. No Scalar, use um access token obtido pelo fluxo OIDC Authorization Code + PKCE. Informe apenas o token no esquema Bearer; não adicione outro prefixo `Bearer`. O Scalar não mantém a autenticação persistida. UI e contrato são desabilitados em `Production`.

## 8. Verificar autenticação e observabilidade

Com a stack pronta e o bootstrap concluído:

```bash
node scripts/smoke-oidc.mjs
node scripts/smoke-observability.mjs
node scripts/smoke-production-host.mjs
```

O primeiro verifica PKCE real, acesso autenticado, role do client, negação de password grant e logout. O segundo gera uma nova requisição e verifica trace no Tempo, correlação no Loki, métricas no Prometheus e ausência de uma sentinela privada. O terceiro sobe um container temporário com a mesma imagem em `Production` e verifica que a documentação não está exposta; não implanta nada em produção.

Os scripts não imprimem nem persistem tokens. No Grafana, consulte o painel provisionado **Modular API — operação**. A existência de dashboards e regras não comprova entrega de alertas: contatos, plantão e canais produtivos precisam ser configurados separadamente.

## 9. Configurar a identidade do seu produto

Para a primeira instalação, você pode manter os valores da fixture: realm `modular-api`, client de recurso/audience `modular-api` e client navegador `modular-web`. Eles são independentes do nome da solução.

Quando adaptar esses valores, revise conjuntamente:

- `infra/keycloak/realm.json`: realm, clients, audience mapper, roles, redirects e origens.
- `compose.local.yaml`, `compose.production.yaml` e `appsettings*.json`: issuer, audience e configuração do host.
- `scripts/init-local.mjs`, `bootstrap-local.mjs`, `wait-local.mjs`, `smoke-oidc.mjs` e demais verificações: os valores da fixture são explícitos nesses arquivos.
- `DefaultRoles`, `Policies`, `Oidc:AllowedRoles` e testes: padrão `Administrator`, `Organizer`, `Participant`.

Faça essas escolhas antes de criar um ambiente definitivo. A importação de `realm.json` ocorre na criação inicial; editar o arquivo não atualiza automaticamente um realm já existente. Uma troca de issuer também exige revisar os vínculos `(issuer, subject)` da API. Não apague banco/volume para aplicar mudanças de roles.

## 10. Iniciar seu domínio e versionar

Siga `docs/extending.md` para criar, por exemplo, `Module.Billing`, com `Domain/`, `UseCases/<Name>/` e `Shared/`. O módulo depende apenas de `Shared.*`; comunicação entre módulos usa contratos explícitos. Mantenha policies, fronteira transacional, auditoria, Outbox e testes.

Não transforme `Shared.*` em código específico do negócio. A base genérica não inclui regras de cobrança, clientes ou pedidos: elas precisam ser modeladas no seu projeto.

Para iniciar um repositório, se ainda não houver um:

```bash
git init
git status --short --untracked-files=all
```

Revise os arquivos antes do primeiro commit. Confirme que `.env`, `.local/`, `.secrets/`, backups e resultados locais estão ignorados. Esta etapa não exige publicar o repositório nem enviar código a um serviço externo.

## 11. Rotina de desenvolvimento e qualidade

Para repetir os testes com cobertura, use um diretório de resultados novo para cada execução:

```bash
dotnet test api/BillingApi.slnx --no-restore --collect:"XPlat Code Coverage" --settings api/coverage.runsettings --results-directory artifacts/coverage/first-check
node scripts/check-coverage.mjs artifacts/coverage/first-check
node scripts/check-dependencies.mjs
node scripts/validate-production.mjs --fixture
node scripts/validate-infra.mjs
```

Troque `first-check` na execução seguinte; o agregador espera exatamente quatro relatórios da mesma rodada. Os gates estão em `api/coverage-policy.json`. A validação com `--fixture` usa valores sintéticos; não valida seus segredos/DNS/serviços produtivos.

`node scripts/test-template.mjs` é exclusivo do repositório da base, pois exige `.template.config/`. Ele gera e testa os dois modos em diretórios isolados. No projeto gerado, execute as quatro suítes da própria solução; o workflow já ignora o teste de geração quando o metadata do template não existe.

Os workflows ficam em `.github/workflows/`. Eles acompanham a cópia, mas só executarão remotamente após publicação e habilitação no seu provedor. Não constituem um deploy automático.

## 12. Parar e retomar sem perder dados

Para parar a stack deste projeto preservando os volumes:

```bash
docker compose -f compose.local.yaml --profile observability down
```

Para retomá-la:

```bash
docker compose -f compose.local.yaml --profile observability up --build -d
node scripts/wait-local.mjs
```

Não execute `init-local` nem o bootstrap novamente se já foram concluídos. **Não use `down -v`** em um ambiente cujos dados deseja preservar.

## 13. Problemas comuns

| Sintoma | Verificação segura |
|---|---|
| Template não encontrado | Rode `dotnet new list modular-api`; instale a pasta da base que contém `.template.config/template.json` |
| SDK incompatível | Execute `dotnet --version` dentro de `api/` e compare com `global.json` |
| Testcontainers falha | Confirme `docker version`, daemon ativo e permissão do usuário |
| Porta ocupada | Identifique a aplicação que usa a porta; não pare stacks alheias. Adapte portas e referências coerentemente |
| `.env` já existe | Preserve o arquivo; confirme que está na pasta correta. Não gere novas senhas para volumes antigos |
| `Banco incompatível` | Pare. Há histórico EF em schema não registrado. Use banco novo ou planeje migração explícita; não apague o histórico |
| API retorna `401` | Verifique issuer, audience, token válido e vínculo local ativo; não compartilhe o token para diagnosticar |
| API retorna `403` | Confira role do client correto, allowlist e propriedade do recurso; role no realm não basta |
| Bootstrap informa base já provisionada | Não repita o provisionamento; gerencie o usuário existente pelo fluxo administrativo |
| Scalar não aparece em produção | Esperado por segurança. Use-o apenas em desenvolvimento |
| Smoke de telemetria falha | Confira o profile `observability`, readiness e tempo de exportação; não desative os controles de privacidade |

## 14. Banco legado e produção

A versão em inglês é uma **nova base para projetos/bancos novos**. Não é migração automática de uma instalação anterior com schemas, roles, rotas ou eventos em português. Bancos e volumes antigos devem ser preservados; a estratégia de conversão, corte e rollback é uma entrega separada. Consulte `docs/language-conventions.md`.

Para produção, siga `docs/security.md`, `docs/runbooks.md` e `docs/quality-gates.md`: Keycloak externo com HTTPS/MFA, usuários DDL/DML separados, segredos gerenciados, TLS, imagens fixadas, backup/restore, monitoramento e entrega real de alertas. Use exclusivamente `compose.production.yaml` com configuração própria e aprovação operacional. Não publique a fixture local como ambiente produtivo.

## Checklist de instalação concluída

- Projeto criado em pasta nova, com nome próprio e modalidade correta.
- Restore, build e quatro suítes de testes aprovados.
- Credenciais novas, privadas e excluídas do Git; volumes separados.
- Job de migração concluído e readiness respondendo `200`.
- Keycloak importado e vínculo inicial criado uma única vez.
- Scalar acessível em desenvolvimento e smokes aprovados.
- Logs, métricas e traces disponíveis; limites produtivos compreendidos.
- Convenções de idioma e próximos módulos do produto documentados.
