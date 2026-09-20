using System.Diagnostics;
using OpenTelemetry;
namespace Shared.Observability;
/// <summary>Remove dados SQL, query string e detalhes livres de erro antes da exportação.</summary>
internal sealed class PrivacyTraceProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        foreach (var tag in activity.TagObjects.ToArray())
            if (tag.Key is "db.statement" or "db.query.text" or "url.query" or "exception.message" or "exception.stacktrace" ||
                tag.Key.StartsWith("db.query.parameter.", StringComparison.Ordinal))
                activity.SetTag(tag.Key, null);
        if (Uri.TryCreate(activity.GetTagItem("url.full") as string, UriKind.Absolute, out var url))
            activity.SetTag("url.full", url.GetLeftPart(UriPartial.Authority));
    }
}
