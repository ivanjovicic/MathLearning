namespace MathLearning.Application.Helpers;

public readonly record struct PaginationWindow(
    int Page,
    int PageSize,
    int Skip,
    int FetchCount);

public static class PaginationBounds
{
    public const int DefaultMaxPage = 1_000;

    public static PaginationWindow Normalize(
        int page,
        int pageSize,
        int defaultPageSize,
        int maxPageSize,
        int maxPage = DefaultMaxPage)
    {
        if (defaultPageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(defaultPageSize));
        if (maxPageSize < defaultPageSize)
            throw new ArgumentOutOfRangeException(nameof(maxPageSize));
        if (maxPage < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPage));
        if ((long)maxPage * maxPageSize > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maxPage), "Maximum page window must fit in Int32.");

        var normalizedPage = Math.Clamp(page, 1, maxPage);
        var normalizedPageSize = pageSize < 1
            ? defaultPageSize
            : Math.Min(pageSize, maxPageSize);

        var skip = checked((normalizedPage - 1) * normalizedPageSize);
        var fetchCount = checked(normalizedPage * normalizedPageSize);

        return new PaginationWindow(
            normalizedPage,
            normalizedPageSize,
            skip,
            fetchCount);
    }
}
