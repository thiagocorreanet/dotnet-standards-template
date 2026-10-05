# Modular API Template

API reutilizável em .NET 10. As decisões estão em `docs/architecture.md`; no repositório de origem, `docs/architecture-review.md` contém a análise histórica.

Contrato de trabalho completo: `CLAUDE.md`. Guias de apoio: `docs/architecture-practices.md` (onde uma regra mora, quando um padrão se justifica) e `docs/dotnet-practices.md` (código do dia a dia).

- Preserve o monolito modular: `Host.Api`, `Module.*`, `Shared.*`; módulos dependem apenas de `Shared.*`.
<!--#if (sharedSource) -->
- As bibliotecas `Shared.*` genéricas são código deste projeto, copiado da base na geração e sem feed nem pacote (item `SharedReference`); continuam genéricas, sem regra nem nome de módulo de negócio. Contratos dos módulos ficam em `Shared.Contracts.Modules` (ADR-010).
<!--#else -->
- No projeto gerado, as bibliotecas `Shared.*` genéricas chegam como pacotes (item `SharedReference`, versão em `SharedPackagesVersion`); contratos dos módulos ficam em `Shared.Contracts.Modules`. Correção na base vira versão nova do pacote, não cópia de código (ADR-010, `docs/upgrading.md`).
<!--#endif -->
- Código e contratos técnicos são em inglês: identificadores, arquivos, rotas, JSON, enums, roles, schemas, eventos e códigos de erro. Mensagens humanas, comentários/XML e documentação são em pt-BR; o `README.md` fica em inglês, por ser a porta de entrada pública. Consulte `docs/language-conventions.md`.
- Esta versão utiliza uma nova base de migrações em inglês. Não aponte o migrador para bases legadas nem remova a proteção contra históricos de schemas não registrados para contornar uma incompatibilidade.
- Infraestrutura compartilhada não contém regras, schemas ou nomes dos módulos de exemplo.
- Keycloak/OIDC é o emissor de tokens; a API autoriza recursos e resolve `(issuer, subject)` para identidade interna.
- Não registre senhas, tokens, documentos ou payloads pessoais em telemetria/auditoria.
- Caso de uso não narra o fluxo em log: o `TelemetryUseCaseDecorator` registra entrada, sucesso, rejeição e duração. Só `LogWarning` para situação anômala que não vira erro.
- Toda escrita de negócio é `[Command]` e entra na fronteira transacional antes de ler/validar invariantes. Sem argumento, a chave é o módulo; `{Propriedade}` trava por recurso; chave fixa coordena um conjunto entre módulos (ADR-009). Retries reexecutam com escopo/DbContext novos e verificam commit indeterminado.
- Outbox tem entrega pelo menos uma vez, claim token e confirmação condicional. Handlers registrados por `AddIntegrationEventHandler` passam pela Inbox do módulo (idempotência por evento e consumidor, na transação do efeito); opt-out só com `[SkipInbox("justificativa")]`.
- Módulo e caso de uso novos começam pelos geradores, na pasta `api/`: `dotnet new modular-module -n <Nome>` e `dotnet new modular-usecase -n <Caso> --module <Módulo> [--command]`. A policy gerada nega tudo até ser escrita.
- Casos de uso ficam em `UseCases/<Name>/` (Endpoint, Request, Response, Validator, UseCase e AccessPolicy); domínio em `Domain/`; infraestrutura do módulo em `Shared/`.
- `Domain/` só depende de `System.*`, `Shared.Kernel` e `Shared.Contracts`; o `Shared.Kernel` não referencia ASP.NET Core nem EF Core.
- Cada caso de uso tem exatamente uma `IAccessPolicy<TRequest>` no próprio slice; sem ela a composição falha no startup. Regra de acesso repetida vira serviço pequeno em `Module.<Name>/Shared/`, sem classe base.
- Use EF Core direto, contratos explícitos, FluentValidation e Result/ProblemDetails. Não acrescentar broker, microserviços ou repositório genérico sem driver.
- Testes de segurança, concorrência e resiliência usam PostgreSQL real em Testcontainers.
- Configuração local e produtiva são independentes. Não publicar banco, OTLP ou gerenciamento em produção; não oferecer segredo padrão produtivo.
- Módulos de eventos são exemplo removível. A geração sem exemplo deve compilar e executar testes da base genérica.
<!--#if (sourceRepository) -->
- No repositório de origem do template, atualize `docs/implementation-status.md` com evidência de aceite, sem marcar requisitos externos como verificados localmente.
<!--#endif -->

Comandos principais: `cd api && dotnet test`; scripts de operações ficam em `scripts/`.
