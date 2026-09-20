# Reutilizar e estender

## Antes de incluir domínio

Escolha nome, audience, realm e roles do produto. As roles de demonstração `Administrator`/`Organizer`/`Participant` são configuráveis, mas alterar `DefaultRoles` exige atualizar policies e testes. Não confunda identidade com pessoa de negócio nem use e-mail como chave de vínculo. Para a primeira instalação, siga `installation-guide.md`.

A base é single-organization. Multi-tenancy requer TenantId em entidades, contratos, índices, policies, dados históricos e testes de isolamento; não basta acrescentar uma claim.

## Novo módulo

1. Crie `api/src/modules/Module.<Name>/Module.<Name>.csproj`, referenciando somente os Shared necessários. Adicione referência ao Host.Api. A descoberta usa assemblies `Module.*`; faça build/publish limpo ao remover módulos para não conservar DLL antiga.
2. Implemente `IModule`: configure o DbContext com schema exclusivo, validators, casos de uso e policy; mapeie o grupo versionado de endpoints. Use Identidade como exemplo pequeno e o domínio opcional como exemplo completo.
3. Mantenha `Domain/`, `Shared/` e `UseCases/<Name>/` dentro do módulo. Use DTOs de entrada/saída, FluentValidation e Result. O endpoint só adapta HTTP.
4. Implemente `IModuleAccessPolicy`, retornando a assembly do módulo. A execução sem policy é rejeitada. Diferencie leitura pública, papel global, propriedade e contexto de organização. Faça a consulta de propriedade no servidor.
5. Marque cada caso de uso de escrita com `[Command("consistency-key")]`. Escolha a chave pelo conjunto de invariantes, não pelo nome do endpoint. Todos os escritores participantes devem compartilhar a coordenação, inclusive manutenção.
6. Derive de `ModuleDbContext` e aplique `base.OnModelCreating`. `BaseEntity` suporta eventos/auditoria/soft delete. Crie constraint/índice único quando houver identidade de negócio; consultas prévias sozinhas não protegem concorrência.
7. Publique contratos síncronos/eventos em Shared.Contracts. Não referencie outro módulo, não retorne entidades EF, não faça HTTP local para outro módulo do mesmo processo.
8. Dê a cada evento `[EventContract("context.fact.v1", requiresConsumer: true)]` quando houver efeito obrigatório. Registre IIntegrationEventHandler; consumidores devem ser idempotentes. Não remova v1 enquanto houver mensagens v1 suportadas em armazenamento.
9. Gere migração com a ferramenta local e revise SQL. Execute o job com credencial de migração; use somente banco novo de teste para verificar a migração. O grant de runtime usa os módulos/tabelas registrados.
10. Acrescente testes de domínio, policy/IDOR, contrato HTTP, PostgreSQL/concorrência, Outbox e arquitetura. Rode a solução inteira.

### O que o exemplo demonstra sobre exclusão

`DeleteRoom` e `DeleteVenue` consultam os módulos consumidores antes de excluir (`IsVenueInUseAsync` e `IsRoomInUseAsync`) e recusam com `409 Venues.ResourceInUse`; `DeleteTrack` faz o mesmo com `IsTrackInUseAsync` e recusa com `409 Events.TrackInUse`. `DeleteTalk` consulta o evento para não deixar um evento publicado sem palestra ativa (`409 Talks.LastTalk`). Esse é o padrão a copiar: a verificação de referência cruzada é do caso de uso, pelo contrato do módulo dono, dentro da fronteira transacional.

O exemplo **não** modela cascata referencial completa. `DeleteEvent` não consulta Palestras e `DeletePerson` não consulta nenhum módulo, então uma palestra ou uma inscrição pode permanecer ativa apontando para um registro já excluído logicamente. Os eventos de integração publicados na exclusão são observacionais e não têm consumidor (ADR-005).

No seu domínio, decida explicitamente para cada exclusão: recusar enquanto houver referência, propagar por consumidor idempotente ou aceitar a referência órfã e documentar a consequência. Exclusão lógica não invalida sozinha o que outro módulo já gravou.

Exemplo de comando EF, a partir de `api`:

```bash
dotnet tool restore
dotnet ef migrations add Initial --project src/modules/Module.New --startup-project src/hosts/Host.Api --context NewDbContext --output-dir Shared/Migrations
```

O factory de design usa `ConnectionStrings__ModularApi`; sem ela, existe apenas um endereço local de design sem senha. Confira a variável antes de executar comandos que acessam banco. Não grave segredos no csproj/appsettings.

## Privacidade e evolução

A auditoria mascara valores por padrão. `AuditValue()` é uma exceção explícita: use só em valor não sensível e de cardinalidade controlada. `Sensitive()` permanece mascarado. Chaves compostas devem usar identificadores técnicos, nunca CPF/e-mail. Snapshots históricos são cópias de dados pessoais e precisam da política do produto.

Não registre DTO/request completo, nem mensagem de exceção externa. Tags de consulta devem ser constantes. Para métrica use dimensão com conjunto limitado de valores.

Transações não envolvem chamadas externas. Integrações externas futuras precisam de timeout, retry somente quando seguro, jitter, circuito quando houver driver, autenticação de serviço, idempotência do destino e contrato de falha. O template não inventa um broker sem necessidade.

## Remover o exemplo

Preferencialmente gere um novo projeto sem `--includeExample`. Os quatro módulos, seus contratos e testes são excluídos fisicamente pelo template; Identidade/Auditoria e testes do núcleo permanecem.

Para remover de um projeto já usado, primeiro faça plano de retenção/migração de dados, drene mensagens/consumidores, remova referências e publique artefatos limpos. Alterar apenas IncludeExample em uma pasta com DLLs antigas não é procedimento seguro de remoção produtiva.
