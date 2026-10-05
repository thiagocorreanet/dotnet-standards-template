# Geradores de módulo e de caso de uso

Templates de item instalados junto com o template de projeto: `dotnet new install <pasta-do-repositório>` registra `modular-api`, `modular-module` e `modular-usecase`. Esta pasta não é exportada para o projeto gerado; uso para o dev em [`docs/extending.md`](../docs/extending.md).

| Template | Token substituído | Saída (relativa a `api/`) |
|---|---|---|
| `modular-module` | `ModuleName` (nome), `module-route` (kebab-case) | `src/modules/Module.<Nome>/`, `tests/Tests.Unit/<Nome>/` |
| `modular-usecase` | `UseCaseName` (nome), `ModuleName` (`--module`), `use-case-route` (kebab-case) | `src/modules/Module.<Módulo>/UseCases/<Caso>/`, `tests/Tests.Unit/<Módulo>/` |

Os post-actions de `modular-module` adicionam o projeto à solução e as referências em `Host.Api`, `Tests.Unit` e `Tests.Architecture`. Eles usam `continueOnError` e imprimem o comando manual quando falham; `scripts/test-template.mjs` confirma que cada registro aconteceu.

## Manutenção

**Migração inicial do módulo.** `modular-module/src/modules/Module.ModuleName/Migrations/` foi gerada pelo `dotnet ef` com o próprio template compilado como módulo real (`ModuleName`). Quando as convenções de `Shared.Data` mudarem (por exemplo, `ModuleModelConventions` ou a Outbox/Inbox), regenere:

1. Copie `modular-module/src/modules/Module.ModuleName` para `api/src/modules/` de uma cópia descartável do repositório.
2. Na cópia, em `api/`: `dotnet add src/hosts/Host.Api/Host.Api.csproj reference src/modules/Module.ModuleName/Module.ModuleName.csproj`.
3. `dotnet ef migrations add Initial --project src/modules/Module.ModuleName --startup-project src/hosts/Host.Api --context ModuleNameDbContext --output-dir Migrations`.
4. Substitua os três arquivos de `Migrations/` do template pelos gerados (sem BOM, com LF, como as demais migrações).

Sem isso, `test-template.mjs` falha: o projeto gerado pelo `modular-module` acusa mudança de modelo pendente em `DeliveryContractTests`.

**Código gerado sem avisos.** `test-template.mjs` compila o projeto com um módulo e dois casos de uso gerados e recusa qualquer aviso em arquivo gerado.
