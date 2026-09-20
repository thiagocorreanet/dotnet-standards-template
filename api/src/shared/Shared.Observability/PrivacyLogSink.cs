using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Shared.Observability;

/// <summary>Preserva a falha, mas não encaminha Exception, template livre ou payload para nenhum destino.</summary>
internal sealed class PrivacyLogSink(Logger destination) : ILogEventSink, IDisposable
{
    private static readonly MessageTemplate FailureTemplate = new MessageTemplateParser()
        .Parse("Falha sanitizada {ExceptionType}, fingerprint {ErrorFingerprint}");
    private static readonly HashSet<string> ContextProperties = new(StringComparer.Ordinal)
    {
        "SourceContext", "Application", "EnvironmentName", "Version", "Module", "Module", "UseCase", "Operation",
        "TraceId", "SpanId", "CorrelationId", "OperationId", "MessageId", "EventId", "ErrorCode"
    };

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Exception is not { } exception)
        {
            destination.Write(logEvent);
            return;
        }

        // Allowlist de contexto técnico: nem Exception.Data, inner messages, propriedades arbitrárias
        // nem o template original são confiáveis (frameworks também interpolam dados nesses campos).
        var properties = logEvent.Properties
            .Where(p => ContextProperties.Contains(p.Key) && IsTechnicalIdentifier(p.Value))
            .Select(p => new LogEventProperty(p.Key, p.Value)).ToList();
        if (!properties.Any(p => p.Name == "ErrorCode"))
            properties.Add(new("ErrorCode", new ScalarValue("ExceptionRecorded")));
        properties.Add(new("ExceptionType", new ScalarValue(exception.GetType().FullName)));
        properties.Add(new("ErrorFingerprint", new ScalarValue(Fingerprint(exception))));
        destination.Write(new LogEvent(logEvent.Timestamp, logEvent.Level, null, FailureTemplate,
            properties, logEvent.TraceId ?? default, logEvent.SpanId ?? default));
    }

    private static bool IsTechnicalIdentifier(LogEventPropertyValue value) => value is ScalarValue { Value: Guid } || value is ScalarValue { Value: string text }
        && text.Length is > 0 and <= 256
        && text.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '+' or '`');

    private static string Fingerprint(Exception exception)
    {
        var signature = new StringBuilder();
        for (var depth = 0; exception is not null && depth < 4; depth++, exception = exception.InnerException!)
        {
            signature.Append(exception.GetType().FullName).Append('|');
            foreach (var frame in (new StackTrace(exception, false).GetFrames() ?? []).Take(8))
            {
                var method = frame.GetMethod();
                signature.Append(method?.DeclaringType?.FullName).Append('.').Append(method?.Name).Append('|');
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature.ToString())))[..24];
    }

    public void Dispose() => destination.Dispose();
}
