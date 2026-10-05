using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Kernel.Results;
using Module.Identity.Shared;
using Shared.Contracts.Common;
namespace Module.Identity.UseCases.UpdateUserAccess;
public sealed record UpdateUserAccessRequest(Guid UserId, bool IsActive, bool RevokeTokens);
public sealed record UpdateUserAccessResponse(Guid Id, bool IsActive, DateTimeOffset TokensValidAfter);
[Command("identity")]
internal sealed class UpdateUserAccessUseCase(IdentityDbContext db, ICurrentUser actor, TimeProvider time)
    : IUseCase<UpdateUserAccessRequest, UpdateUserAccessResponse>
{
    public async Task<Result<UpdateUserAccessResponse>> HandleAsync(UpdateUserAccessRequest request, CancellationToken ct)
    {
        if (request.UserId == actor.Id && !request.IsActive)
            return Error.Conflict("Identity.SelfDeactivation", "Use outro administrador para desativar esta conta.");
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == request.UserId, ct);
        if (user is null) return Error.NotFound("Identity.NotFound", "Usuário não encontrado.");
        user.UpdateAccess(request.IsActive, request.RevokeTokens ? time.GetUtcNow() : user.TokensValidAfter);
        await db.SaveChangesAsync(ct);
        return new UpdateUserAccessResponse(user.Id, user.IsActive, user.TokensValidAfter);
    }
}
/// <summary>Somente administrador ativa, desativa ou revoga acesso.</summary>
internal sealed class UpdateUserAccessAccessPolicy(ICurrentUser user) : IAccessPolicy<UpdateUserAccessRequest>
{
    public Task<bool> CanExecuteAsync(UpdateUserAccessRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.HasRole(DefaultRoles.Administrator));
}
internal sealed class UpdateUserAccessEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPut("/users/{userId:guid}/access", async (Guid userId, UpdateUserAccessRequest request,
            IUseCase<UpdateUserAccessRequest, UpdateUserAccessResponse> useCase, CancellationToken ct) =>
            (await useCase.HandleAsync(request with { UserId = userId }, ct)).ToHttpResult())
        .WithName("UpdateUserAccess").RequireAuthorization(Policies.Administration);
}
