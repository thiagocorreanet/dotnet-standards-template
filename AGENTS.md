# Modular API Template

API reutilizável em .NET 10. As decisões estão em `docs/architecture.md`; no repositório de origem, `docs/architecture-review.md` contém a análise histórica.

Contrato de trabalho completo: `CLAUDE.md`. Guias de apoio: `docs/architecture-practices.md` (onde uma regra mora, quando um padrão se justifica) e `docs/dotnet-practices.md` (código do dia a dia).

- Preserve o monolito modular: `Host.Api`, `Module.*`, `Shared.*`; módulos dependem apenas de `Shared.*`.
- Código e contratos técnicos são em inglês: identificadores, arquivos, rotas, JSON, enums, roles, schemas, eventos e códigos de erro. Mensagens humanas, comentários/XML e documentação são em pt-BR. Consulte `docs/language-conventions.md`.
- Esta versão utiliza uma nova base de migrações em inglês. Não aponte o migrador para bases legadas nem remova a proteção contra históricos de schemas não registrados para contornar uma incompatibilidade.
- Infraestrutura compartilhada não contém regras, schemas ou nomes dos módulos de exemplo.
- Keycloak/OIDC é o emissor de tokens; a API autoriza recursos e resolve `(issuer, subject)` para identidade interna.
- Não registre senhas, tokens, documentos ou payloads pessoais em telemetria/auditoria.
- Toda escrita de negócio entra na fronteira transacional antes de ler/validar invariantes. Retries reexecutam com escopo/DbContext novos e verificam commit indeterminado.
- Outbox tem entrega pelo menos uma vez, claim token, confirmação condicional e consumidores idempotentes.
- Casos de uso ficam em `UseCases/<Name>/`; domínio em `Domain/`; infraestrutura do módulo em `Shared/`.
- Use EF Core direto, contratos explícitos, FluentValidation e Result/ProblemDetails. Não acrescentar broker, microserviços ou repositório genérico sem driver.
- Testes de segurança, concorrência e resiliência usam PostgreSQL real em Testcontainers.
- Configuração local e produtiva são independentes. Não publicar banco, OTLP ou gerenciamento em produção; não oferecer segredo padrão produtivo.
- Módulos de eventos são exemplo removível. A geração sem exemplo deve compilar e executar testes da base genérica.
- Atualize `docs/implementation-status.md` com evidência de aceite, sem marcar requisitos externos como verificados localmente.

Comandos principais: `cd api && dotnet test`; scripts de operações ficam em `scripts/`.
