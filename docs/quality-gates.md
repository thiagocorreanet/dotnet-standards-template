# Verificações de qualidade

## PR e execução local

```bash
dotnet restore api/ModularApi.slnx --locked-mode
dotnet test api/ModularApi.slnx --no-restore --logger trx --collect:"XPlat Code Coverage" --settings api/coverage.runsettings --results-directory artifacts/coverage/minha-execucao
node scripts/check-coverage.mjs artifacts/coverage/minha-execucao
node scripts/validate-infra.mjs
node scripts/validate-production.mjs --fixture
```

Use um diretório novo por execução. O verificador exige quatro relatórios, ignora cópias dos anexos TRX e unifica hits por linha/branch, sem somar porcentagens de suítes. Produz `summary.json` por assembly e arquivo. Coverlet usa VSTest; não trocar para Microsoft.Testing.Platform sem adaptar o coletor. Pacotes estão fixados centralmente e nos lockfiles.

Pisos em `api/coverage-policy.json`: total 70% de linhas e 60% de branches; gates específicos para processador/sonda, sanitização de logs e autenticação OIDC. O total inclui exemplo quando presente; o núcleo gerado possui menos código/testes. Migrações e código gerado em obj são exclusões declaradas; migrações continuam testadas em PostgreSQL. Cobertura é evidência de execução, não de correção de toda asserção.

`node scripts/test-template.mjs` gera os dois modos em diretórios novos, executa as quatro suítes com cobertura e exige os gates em ambos. Dessa forma, a aprovação do núcleo genérico não depende dos testes dos módulos de exemplo.

## Release e verificações periódicas

O workflow `codeql` analisa C# e workflows em push, pull request e semanalmente; os alertas ficam na aba Security do repositório e não bloqueiam o merge por si. O workflow `dependency-review` roda só em pull request e falha em severidade alta introduzida pelo diff. O `.github/dependabot.yml` abre PR semanal para NuGet, Docker e actions; um PR de NuGet que não atualizar os `packages.lock.json` falha no `--locked-mode` do `verify`, e a correção é rodar `dotnet restore --force-evaluate` no branch do PR.

O workflow `operational-release-checks` provisiona uma fixture local nova no runner, verifica OIDC real/telemetria, imagem em Production e restore lógico. Executa manualmente, em tags v* e semanalmente. O workflow `verify` testa também semanalmente para reconsultar vulnerabilidades sem depender de alterações de código.

O ambiente local precisa dos serviços de `compose.local.yaml --profile observability`. `init-local.mjs` só serve para uma fixture nova: não sobrescreve credenciais. Em ambiente já provisionado, preserve .env/.local e volumes.

Os workflows não realizam deploy. Configurar proteções/regras de promoção no provedor Git, executar o workflow remoto e exigir seu resultado são etapas de administração ainda externas. Não publicar .env, dumps, secrets ou logs brutos como artifacts; o job operacional retém somente manifestos de restore.

## Aceite do produto/ambiente

Antes de produção, ainda definir e comprovar: carga/p95/p99/SLO, contato e escalonamento de alertas, MFA/administração/recuperação IdP, rotação de segredos/certificados, backup externo/RPO/RTO, upgrade compatível, políticas de dados e testes de segurança proporcionais à exposição. Testes com duas instâncias WebApplicationFactory compartilham PostgreSQL real, mas não simulam duas máquinas, partição de rede ou failover de datacenter.

Referências: [Coverlet/VSTest](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/VSTestIntegration.md), [telemetria interna do Collector](https://opentelemetry.io/docs/collector/internal-telemetry/) e [consulta de traces no Tempo](https://grafana.com/docs/tempo/latest/api_docs/).
