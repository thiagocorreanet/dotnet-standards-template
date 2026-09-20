using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Shared.Http.Results;
using Shared.Observability.Telemetry;

namespace Shared.Http.Endpoints;

/// <summary>
/// Decorator transparente aplicado a TODO caso de uso: garante o envelope comum de início/fim, span de trace,
/// duração, contadores de execução/falha e erro estruturado; os logs internos detalham o fluxo de negócio.
/// </summary>
internal sealed class TelemetryUseCaseDecorator<TRequest, TResponse>(
    IUseCase<TRequest, TResponse> inner,
    ModuleTelemetry telemetry,
    ILogger<TelemetryUseCaseDecorator<TRequest, TResponse>> logger) : IUseCase<TRequest, TResponse>
{
    private static readonly string UseCaseName = ResolveName();

    private static string ResolveName()
    {
        var name = typeof(TRequest).Name;
        return name.EndsWith("Request", StringComparison.Ordinal) ? name[..^"Request".Length] : name;
    }

    public async Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        using var activity = telemetry.StartActivity(UseCaseName);
        var start = Stopwatch.GetTimestamp();
        var context = UseCaseLogContext.From(request);

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["Module"] = telemetry.Module,
            ["UseCase"] = UseCaseName
        });

        activity?.SetTag("usecase.name", UseCaseName);
        activity?.SetTag("module.name", telemetry.Module);
        foreach (var (key, value) in context)
        {
            activity?.SetTag($"request.{key}", value);
        }

        logger.LogInformation("Iniciando caso de uso {UseCase} com {@RequestContext}", UseCaseName, context);
        try
        {
            var result = await inner.HandleAsync(request, cancellationToken);
            var duration = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (result.IsFailure)
            {
                activity?.SetTag("error.code", result.Error.Code);
                activity?.SetTag("error.type", result.Error.Type.ToString());
                logger.LogWarning(
                    "Caso de uso {UseCase} rejeitado pela regra {ErrorCode} ({ErrorType}) em {DurationMs:0.0} ms",
                    UseCaseName, result.Error.Code, result.Error.Type, duration);
            }
            else
            {
                logger.LogInformation("Caso de uso {UseCase} concluído com sucesso em {DurationMs:0.0} ms", UseCaseName, duration);
            }

            telemetry.RecordExecution(UseCaseName, result.IsSuccess, duration, result.IsFailure ? result.Error.Code : null);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var duration = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            activity?.SetStatus(ActivityStatusCode.Error, "Canceled");
            activity?.SetTag("canceled", true);
            telemetry.RecordExecution(UseCaseName, false, duration, "Canceled");
            logger.LogWarning("Caso de uso {UseCase} cancelado após {DurationMs:0.0} ms", UseCaseName, duration);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var duration = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            telemetry.RecordExecution(UseCaseName, false, duration, "Excecao");
            logger.LogError(ex, "Caso de uso {UseCase} falhou após {DurationMs:0.0} ms", UseCaseName, duration);
            throw;
        }
    }
}
