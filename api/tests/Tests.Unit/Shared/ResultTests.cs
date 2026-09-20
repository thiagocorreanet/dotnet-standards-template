using Shared.Http.Results;
using Shouldly;

namespace Tests.Unit.Shared;

public sealed class ResultTests
{
    [Fact]
    public void Success_should_expose_value_and_not_have_error()
    {
        Result<int> result = 42;
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void Failure_should_expose_error_and_throw_to_access_value()
    {
        Result<int> result = Error.NotFound("Test.NotFound", "não achei");
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Match_should_select_the_branch_correct()
    {
        Result<string> ok = "x";
        Result<string> error = Error.Conflict("Test.Conflict", "conflito");
        ok.Match(v => v, e => e.Code).ShouldBe("x");
        error.Match(v => v, e => e.Code).ShouldBe("Test.Conflict");
    }
}
