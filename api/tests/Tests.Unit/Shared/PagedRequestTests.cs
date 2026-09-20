using Shared.Contracts.Common;
using Shouldly;

namespace Tests.Unit.Shared;

public sealed class PagedRequestTests
{
    [Theory]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-5, 500, 1, 100)]
    [InlineData(3, 50, 3, 50)]
    public void Should_normalize_page_and_size(int page, int size, int expectedPage, int expectedSize)
    {
        var request = new PagedRequest(page, size);
        request.NormalizedPage.ShouldBe(expectedPage);
        request.NormalizedSize.ShouldBe(expectedSize);
    }

    [Fact]
    public void Total_of_pages_should_round_to_up()
    {
        new PagedResult<int>([], 1, 20, 41).TotalPages.ShouldBe(3);
        new PagedResult<int>([], 1, 20, 0).TotalPages.ShouldBe(0);
    }
}
