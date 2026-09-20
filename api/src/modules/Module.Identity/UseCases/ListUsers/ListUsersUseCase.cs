using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Module.Identity.Shared;
using Shared.Contracts.Common;
using Shared.Data.Extensions;
namespace Module.Identity.UseCases.ListUsers;
public sealed record ListUsersRequest(int Page = 1, int PageSize = 20);
public sealed record ListUsersItemResponse(Guid Id, string Subject, string UserName, string UserEmail, bool IsActive);
internal sealed class ListUsersUseCase(IdentityDbContext db)
    : IUseCase<ListUsersRequest, PagedResult<ListUsersItemResponse>>
{
    public async Task<Result<PagedResult<ListUsersItemResponse>>> HandleAsync(ListUsersRequest request, CancellationToken ct) =>
        await db.Users.AsNoTracking().OrderBy(x => x.Id)
          .Select(x => new ListUsersItemResponse(x.Id, x.Subject, x.UserName, x.Email, x.IsActive))
          .ToPagedResultAsync(new PagedRequest(request.Page, request.PageSize), ct);
}
internal sealed class ListUsersEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/users", async ([AsParameters] ListUsersRequest request,
            IUseCase<ListUsersRequest, PagedResult<ListUsersItemResponse>> useCase, CancellationToken ct) =>
            (await useCase.HandleAsync(request, ct)).ToHttpResult()).WithName("ListUsers")
        .RequireAuthorization(Policies.Administration);
}
