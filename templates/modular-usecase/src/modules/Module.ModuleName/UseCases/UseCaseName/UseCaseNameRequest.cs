namespace Module.ModuleName.UseCases.UseCaseName;

#if (command)
/// <summary>Entrada do comando. Substitua pelos campos reais (nomes em inglês, documentação em pt-BR).</summary>
public sealed record UseCaseNameRequest(string Description);
#else
/// <summary>Entrada da consulta: o identificador vem da rota.</summary>
public sealed record UseCaseNameRequest(Guid Id);
#endif
