# Atualizar as bibliotecas da base

O projeto gerado recebe as bibliotecas `Shared.*` (Kernel, Contracts, Data, Http, Messaging, Observability, WebHost) como **pacotes NuGet** da base. `Module.Identity`, `Module.Audit`, `Shared.Contracts.Modules`, o host e os testes ficam no projeto, como código. Este guia cobre como consumir, atualizar e o que não é automático.

## Onde estão as peças

| Peça | Arquivo |
|---|---|
| Versão dos pacotes | `api/Directory.Packages.props`, propriedade `SharedPackagesVersion` |
| Feed e mapeamento (só os pacotes da base vêm do feed privado) | `api/nuget.config` |
| Liga pacote em vez de projeto | `api/Directory.Build.props`, `UseSharedPackages=true` |
| Referência a uma biblioteca | item `<SharedReference Include="Data" />` no `.csproj`, resolvido por `api/Directory.Build.targets` |

## Credenciais do feed

O `nuget.config` lê as credenciais de variáveis de ambiente; nunca grave token no arquivo.

- **Máquina do dev:** defina `MODULARAPI_FEED_USER` (usuário do GitHub) e `MODULARAPI_FEED_TOKEN` (token com `read:packages`) antes do `dotnet restore`. O build da imagem local (`docker compose ... up --build`) usa as mesmas variáveis como secret de build.
- **CI do projeto:** os workflows já passam `github.actor` e `GITHUB_TOKEN`. No GitHub Packages, o pacote precisa conceder leitura ao repositório do projeto (página do pacote → *Manage Actions access*).

## Primeira restauração

O template não copia os `packages.lock.json`. Depois de gerar o projeto:

```bash
cd api
dotnet restore        # grava os lock files com os pacotes da base
git add **/packages.lock.json
```

Sem isso, o CI falha no `dotnet restore --locked-mode`.

## Atualizar a versão

1. Leia o `CHANGELOG.md` do repositório da base entre a versão atual e a nova. Major indica mudança incompatível de API, configuração ou schema.
2. Altere `SharedPackagesVersion` em `api/Directory.Packages.props`.
3. `cd api && dotnet restore` (atualiza os lock files) e faça commit.
4. **Migrações:** quando a versão muda convenções de modelo (tabelas de mensageria, por exemplo), cada módulo precisa de migração nova. Rode `dotnet test`: `DeliveryContractTests` acusa o módulo com mudança de modelo pendente. Gere a migração do módulo (`docs/extending.md`), revise o SQL e aplique pelo job `migrate`.
5. Rode as quatro suítes e os gates (`docs/quality-gates.md`).

## O que não vem pelo pacote

- **`Module.Identity` e `Module.Audit`** são código do projeto: correções da base nesses módulos são reaplicadas manualmente, comparando a pasta do módulo com a da nova versão do template. As migrações deles continuam no projeto.
- **Infra, workflows, docs e scripts** também são cópia: confira o que mudou no template e traga o que fizer sentido.
- **Testes das bibliotecas** ficam no repositório da base. Os testes do projeto exercitam a API e os módulos; os de arquitetura usam alguns tipos internos das bibliotecas (`InternalsVisibleTo`) e podem precisar de ajuste numa versão major.

## Voltar para código-fonte

Não é o caminho padrão. Se for preciso depurar ou corrigir a base antes de uma release, use o SourceLink (o PDB vai no pacote) ou trabalhe no repositório da base e publique uma versão nova.
