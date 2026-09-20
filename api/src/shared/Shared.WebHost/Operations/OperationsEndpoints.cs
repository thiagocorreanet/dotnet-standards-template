using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Data.Outbox;
using Shared.Messaging;
namespace Shared.WebHost.Operations;
public static class OperationsEndpoints
{
    public static void MapOperations(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/operations").RequireAuthorization(Policies.Administration).WithTags("Operations");
        group.MapGet("/outbox", (OutboxRuntimeState state) => state.Read()).WithName("OutboxStatus");
        group.MapGet("/outbox/{module}/dead-letters", async (string module, IEnumerable<IOutboxStore> stores, CancellationToken ct) =>
        {
            var store = stores.SingleOrDefault(s => s.Module == module);
            return store is null ? Results.NotFound() : Results.Ok(await store.ListDeadLettersAsync(ct));
        }).WithName("OutboxDeadLetters");
        group.MapPost("/outbox/{module}/{id:guid}/replay", async (string module, Guid id, ReplayRequest request,
            IEnumerable<IOutboxStore> stores, ICurrentUser user, CancellationToken ct) =>
        {
            if (request.ReasonCode is null || request.ReasonCode.Length is < 3 or > 80 ||
                request.ReasonCode.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
                return Results.BadRequest(new { code = "InvalidReasonCode" });
            var store = stores.SingleOrDefault(s => s.Module == module);
            if (store is null) return Results.NotFound();
            return await store.ReplayAsync(id, user.Id!.Value, request.ReasonCode, ct) ? Results.NoContent() : Results.Conflict();
        }).WithName("OutboxReplay");
        app.MapGet("/health/processing", (OutboxRuntimeState state, IEnumerable<IOutboxStore> stores) =>
        {
            var snapshots = state.Read();
            var healthy = snapshots.Count == stores.Count() && snapshots.All(s => (DateTimeOffset.UtcNow - s.ObservedAt).TotalSeconds < 30 && s.DeadLetters == 0);
            return Results.Json(new { status = healthy ? "Healthy" : "Degraded" }, statusCode: healthy ? 200 : 503);
        }).RequireAuthorization(Policies.Administration).ExcludeFromDescription();
    }
    public sealed record ReplayRequest(string ReasonCode);
}
