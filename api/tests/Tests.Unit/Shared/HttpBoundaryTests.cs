using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Http.Results;
using Shared.Http.Validation;
using Shouldly;

namespace Tests.Unit.Shared;

public sealed class HttpBoundaryTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400, "Requisição inválida")]
    [InlineData(ErrorType.NotFound, 404, "Recurso não encontrado")]
    [InlineData(ErrorType.Conflict, 409, "Conflito")]
    [InlineData(ErrorType.BusinessRule, 422, "Regra de negócio violada")]
    [InlineData(ErrorType.Unauthorized, 401, "Não autenticado")]
    [InlineData(ErrorType.Forbidden, 403, "Acesso negado")]
    [InlineData(ErrorType.Failure, 500, "Erro interno")]
    public void Error_contract_preserves_status_code_title_and_problem_identity(ErrorType type, int status, string title)
    {
        var result = new Error("Test.Rejected", "Safe public explanation", type).ToProblem().ShouldBeOfType<ProblemHttpResult>();
        result.ProblemDetails.Status.ShouldBe(status);
        result.ProblemDetails.Title.ShouldBe(title);
        result.ProblemDetails.Detail.ShouldBe("Safe public explanation");
        result.ProblemDetails.Type.ShouldBe("urn:problem:Test.Rejected");
        result.ProblemDetails.Extensions["code"].ShouldBe("Test.Rejected");
    }

    [Fact]
    public void Successful_results_preserve_values_location_and_empty_response()
    {
        var result = Result.Success("value");
        result.ToHttpResult().ShouldBeOfType<Ok<string>>().Value.ShouldBe("value");
        var created = result.ToCreatedResult(value => "/resources/" + value).ShouldBeOfType<Created<string>>();
        created.Location.ShouldBe("/resources/value");
        created.Value.ShouldBe("value");
        result.ToNoContentResult().ShouldBeOfType<NoContent>();
        Result.Success().ToNoContentResult().ShouldBeOfType<NoContent>();
    }

    [Fact]
    public void Every_failed_result_uses_problem_contract_without_invoking_location_builder()
    {
        var error = Error.Forbidden("Test.Forbidden", "Denied");
        var result = Result.Failure<string>(error);
        foreach (var response in new[] { result.ToHttpResult(), result.ToCreatedResult(_ => throw new InvalidOperationException()),
                     result.ToNoContentResult(), Result.Failure(error).ToNoContentResult() })
            response.ShouldBeOfType<ProblemHttpResult>().ProblemDetails.Status.ShouldBe(403);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Missing_validator_or_request_passes_through(bool validatorAvailable, bool requestAvailable)
    {
        using var services = Services(validatorAvailable);
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext { RequestServices = services },
            requestAvailable ? [new ProbeRequest()] : []);
        var result = await Filter().InvokeAsync(context, _ => ValueTask.FromResult<object?>("next"));
        result.ShouldBe("next");
    }

    [Fact]
    public async Task Invalid_request_returns_field_errors_without_executing_endpoint()
    {
        using var services = Services();
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext { RequestServices = services }, new ProbeRequest());
        var result = await Filter().InvokeAsync(context, _ => throw new InvalidOperationException("Endpoint must not execute"));
        var problem = result.ShouldBeOfType<ValidationProblem>().ProblemDetails;
        problem.Status.ShouldBe(400);
        problem.Type.ShouldBe("urn:problem:Validation");
        problem.Errors[nameof(ProbeRequest.Name)].ShouldContain("NameRequired");
    }

    [Fact]
    public async Task Route_ids_are_bound_before_validation_and_named_route_overrides_body_id()
    {
        using var services = Services();
        var resourceId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var request = new ProbeRequest { Name = "Valid", ParentId = Guid.NewGuid() };
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.RouteValues["id"] = resourceId;
        http.Request.RouteValues["parentId"] = parentId;
        var result = await Filter().InvokeAsync(new DefaultEndpointFilterInvocationContext(http, request), _ =>
        {
            request.ResourceId.ShouldBe(resourceId);
            request.ParentId.ShouldBe(parentId);
            return ValueTask.FromResult<object?>("next");
        });
        result.ShouldBe("next");
    }

    [Fact]
    public async Task Invalid_or_unmatched_route_values_do_not_replace_existing_identifiers()
    {
        using var services = Services();
        var resourceId = Guid.NewGuid();
        var request = new ProbeRequest { Name = "Valid", ResourceId = resourceId };
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.RouteValues["ResourceId"] = "not-a-guid";
        http.Request.RouteValues["unmatched"] = Guid.NewGuid();
        http.Request.RouteValues["empty"] = null;
        await Filter().InvokeAsync(new DefaultEndpointFilterInvocationContext(http, request), _ => ValueTask.FromResult<object?>(null));
        request.ResourceId.ShouldBe(resourceId);
        request.ParentId.ShouldBe(Guid.Empty);
    }

    private static ValidationFilter<ProbeRequest> Filter() => new(NullLogger<ValidationFilter<ProbeRequest>>.Instance);
    private static ServiceProvider Services(bool includeValidator = true)
    {
        var services = new ServiceCollection();
        if (includeValidator) services.AddSingleton<IValidator<ProbeRequest>, ProbeValidator>();
        return services.BuildServiceProvider();
    }
    private sealed class ProbeRequest
    {
        public Guid ResourceId { get; set; }
        public Guid ParentId { get; set; }
        public string Name { get; set; } = "";
        public Guid ReadOnlyId => Guid.Empty;
        public int NumericId { get; set; }
    }
    private sealed class ProbeValidator : AbstractValidator<ProbeRequest>
    {
        public ProbeValidator() => RuleFor(x => x.Name).NotEmpty().WithMessage("NameRequired");
    }
}
