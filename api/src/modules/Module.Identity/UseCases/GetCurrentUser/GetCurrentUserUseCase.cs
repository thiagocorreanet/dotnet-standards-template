using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Module.Identity.Shared;
using Shared.Contracts.Common;
namespace Module.Identity.UseCases.GetCurrentUser;
public sealed record GetCurrentUserRequest(Guid UserId);
public sealed record GetCurrentUserResponse(Guid Id, string UserName, string UserEmail, IReadOnlyCollection<string> Roles);
internal sealed class GetCurrentUserUseCase(IdentityDbContext db, ICurrentUser actor)
    : IUseCase<GetCurrentUserRequest, GetCurrentUserResponse>
{
    public async Task<Result<GetCurrentUserResponse>> HandleAsync(GetCurrentUserRequest request, CancellationToken ct)
    {
        if (request.UserId != actor.Id) return Error.Forbidden("Identity.AccessDenied", "Acesso negado.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.UserId, ct);
        return user is null ? Error.NotFound("Identity.NotFound", "Usuário não encontrado.")
            : new GetCurrentUserResponse(user.Id, user.UserName, user.Email, actor.Roles);
    }
}
internal sealed class GetCurrentUserEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/users/me", async (ICurrentUser user, IUseCase<GetCurrentUserRequest, GetCurrentUserResponse> useCase, CancellationToken ct) =>
            (await useCase.HandleAsync(new(user.Id ?? Guid.Empty), ct)).ToHttpResult()).WithName("GetCurrentUser");
}
