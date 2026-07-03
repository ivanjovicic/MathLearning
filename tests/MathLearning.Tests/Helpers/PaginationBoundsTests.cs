using MathLearning.Application.Helpers;

namespace MathLearning.Tests.Helpers;

public sealed class PaginationBoundsTests
{
    [Theory]
    [InlineData(int.MinValue, int.MinValue, 1, 20, 0, 20)]
    [InlineData(0, 0, 1, 20, 0, 20)]
    [InlineData(1, 20, 1, 20, 0, 20)]
    [InlineData(2, 20, 2, 20, 20, 40)]
    [InlineData(int.MaxValue, int.MaxValue, 1000, 100, 99_900, 100_000)]
    public void Normalize_ProducesBoundedWindowWithoutOverflow(
        int page,
        int pageSize,
        int expectedPage,
        int expectedPageSize,
        int expectedSkip,
        int expectedFetchCount)
    {
        var result = PaginationBounds.Normalize(
            page,
            pageSize,
            defaultPageSize: 20,
            maxPageSize: 100);

        Assert.Equal(expectedPage, result.Page);
        Assert.Equal(expectedPageSize, result.PageSize);
        Assert.Equal(expectedSkip, result.Skip);
        Assert.Equal(expectedFetchCount, result.FetchCount);
    }

    [Fact]
    public void Normalize_CustomMaxPageCapsAnalyticsWindow()
    {
        var result = PaginationBounds.Normalize(
            int.MaxValue,
            int.MaxValue,
            defaultPageSize: 10,
            maxPageSize: 100,
            maxPage: 100);

        Assert.Equal(100, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(9_900, result.Skip);
        Assert.Equal(10_000, result.FetchCount);
    }

    [Theory]
    [InlineData(0, 100, 100)]
    [InlineData(20, 19, 100)]
    [InlineData(20, 100, 0)]
    [InlineData(20, 100, int.MaxValue)]
    public void Normalize_InvalidConfiguration_Throws(
        int defaultPageSize,
        int maxPageSize,
        int maxPage)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaginationBounds.Normalize(
                page: 1,
                pageSize: 1,
                defaultPageSize,
                maxPageSize,
                maxPage));
    }
}
