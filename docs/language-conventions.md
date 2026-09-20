# Convenções de idioma e compatibilidade

## Padrão da base

Código e contratos técnicos em inglês; mensagens humanas e documentação em português do Brasil. A convenção também acompanha os projetos gerados pelo template.

| Elemento | Padrão | Exemplo |
|---|---|---|
| Projetos, namespaces, pastas e arquivos de código | Inglês | `Module.Identity`, `UseCases/RegisterUser` |
| Classes, interfaces, métodos e propriedades | Inglês | `BaseEntity`, `IIdentityResolver`, `CreatedAt` |
| Rotas e parâmetros | Inglês | `/api/v1/identity/users`, `pageSize` |
| JSON e valores de enum | Inglês | `userName`, `eventStatus: "Draft"` |
| Roles e policies | Inglês | `Administrator`, `Management` |
| Schemas, tabelas, colunas e migrações | Inglês | `Identity.Users`, `DeletedAt` |
| Códigos de erro e eventos | Inglês | `Identity.AlreadyLinked`, `audit.entity-changed.v1` |
| Propriedades estruturadas de log | Inglês | `Module`, `UserId`, `ErrorCode` |
| Mensagens de erro, validação e texto dos logs da aplicação | pt-BR | `Requisição inválida`, `Usuário não encontrado.` |
| Comentários, XML docs, Markdown e descrições do OpenAPI/Scalar | pt-BR | Prosa em português com referências técnicas em inglês |

Módulos do núcleo: `Module.Identity` e `Module.Audit`. Exemplo opcional: `Module.Venues`, `Module.People`, `Module.Events` e `Module.Talks`.

As mensagens padrão do FluentValidation são fixadas em `pt-BR` na composição do host. Isso não altera cultura de números/datas, nomes JSON ou os valores técnicos dos enums. `Accept-Language: en-US` não traduz a API: esta base não implementa negociação de idiomas.

Problem Details utiliza `code` em inglês e `title`/`detail` em pt-BR. Os nomes de campos em `errors` seguem as propriedades C# em inglês. Mensagens internas de frameworks/ferramentas, a interface nativa de terceiros e valores digitados pelos usuários não são traduzidos automaticamente. CPF permanece `Cpf`, pois é a sigla oficial de um documento brasileiro, não um identificador genérico de outro país.

Os cenários Gherkin permanecem em pt-BR como documentação executável; o código de bindings é em inglês. Arquivos `.feature.cs` são gerados a partir desse texto, não editados manualmente nem exportados pelo template. `docs/reference/` e `architecture-review.md` são arquivos históricos da origem, não contratos atuais.

## Mudança incompatível com a versão anterior

Esta padronização **não é um upgrade de banco em uso**. Mudaram rotas, DTOs, schemas, nomes de migrações, roles e nomes/payloads de eventos Outbox. Não há aliases de rotas ou nomes JSON antigos, nem adaptação automática de tokens ou eventos pendentes.

Para um projeto novo, gere uma pasta nova e utilize banco, credenciais, volumes e nome Compose próprios. Não copie `.env`, `.local`, banco nem volumes do template. A mudança não apagou ou migrou os ambientes antigos.

O migrador, sob advisory lock, verifica históricos EF em schemas não registrados e recusa a execução antes de aplicar migrações. Essa proteção pressupõe banco dedicado ao produto: módulos removidos ou schemas de outra aplicação também exigem análise. Ela não certifica a compatibilidade de qualquer banco arbitrário. `dotnet ef database update`, SQL manual e outras ferramentas podem contornar esse caminho; não os use contra uma base legada.

Se houver dados a preservar, planeje uma migração separada: backup e restore testados; inventário de clientes/consumidores; mapeamento de schema/colunas/índices e históricos; tratamento de Outbox pendente e auditoria; mudança administrativa de roles no Keycloak; corte de clientes/tokens e rollback compatível. Não remova o histórico de migrações para simular um banco vazio, nem apague volumes para atualizar o realm.

## Como manter o padrão

- Consulte `AGENTS.md` ao criar um módulo; não introduza contratos misturando idiomas.
- Atualize OpenAPI, exemplos, roles e eventos junto com o código correspondente.
- Preserve os testes `LanguageConventionTests` e `MigrationBaselineTests`, além das suítes de domínio, segurança, concorrência e arquitetura.
- Execute `scripts/test-template.mjs` após mudanças no núcleo ou na geração para validar os modos genérico e com exemplo.
