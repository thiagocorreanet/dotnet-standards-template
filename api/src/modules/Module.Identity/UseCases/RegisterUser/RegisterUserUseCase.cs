using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;
using Module.Identity.Shared;
using Microsoft.Extensions.Options;
using Module.Identity.Domain;
using Shared.WebHost.Security;
namespace Module.Identity.UseCases.RegisterUser;
public sealed record RegisterUserRequest(string Subject, string UserName, string UserEmail);
public sealed record RegisterUserResponse(Guid Id, string Subject);
public sealed class RegisterUserValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(255).Must(x => x is not null && x == x.Trim());
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UserEmail).NotEmpty().EmailAddress().MaximumLength(254);
    }
}
[Command("identity")]
internal sealed class RegisterUserUseCase(IdentityDbContext db, IOptions<OidcOptions> oidc)
    : IUseCase<RegisterUserRequest, RegisterUserResponse>
{
    public async Task<Result<RegisterUserResponse>> HandleAsync(RegisterUserRequest request, CancellationToken ct)
    {
        var issuer = oidc.Value.Authority;
        if (await db.Users.IgnoreQueryFilters().AnyAsync(x => x.Issuer == issuer && x.Subject == request.Subject, ct))
            return Error.Conflict("Identity.AlreadyLinked", "Identidade já vinculada.");
        var user = User.Create(issuer, request.Subject, request.UserName, request.UserEmail);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return new RegisterUserResponse(user.Id, user.Subject);
    }
}
internal sealed class RegisterUserEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/users", async (RegisterUserRequest request, IUseCase<RegisterUserRequest, RegisterUserResponse> useCase, CancellationToken ct) =>
            (await useCase.HandleAsync(request, ct)).ToCreatedResult(x => $"/api/v1/identity/users/{x.Id}"))
        .WithName("RegisterUser").WithSummary("Provisiona vínculo local com subject existente no provedor")
        .WithValidation<RegisterUserRequest>().RequireAuthorization(Policies.Administration);
}
