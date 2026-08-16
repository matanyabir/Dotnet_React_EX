namespace MX.Application.Common;

/// <summary>
/// One page of results, plus enough context to render a pager.
///
/// <see cref="TotalCount"/> is the size of the whole match, not of
/// <see cref="Items"/> — without it a client can say "next" but never "page 3 of
/// 9", and cannot tell an empty last page apart from an empty result. The derived
/// members are computed here rather than in each caller so the browser, the tests,
/// and any future client all agree on when "next" is available.
/// </summary>
/// <param name="Items">The rows on this page, already ordered.</param>
/// <param name="Page">1-based. The first page is 1, not 0, because that is what a pager shows.</param>
/// <param name="PageSize">Rows requested per page. The last page may hold fewer.</param>
/// <param name="TotalCount">Rows matching the query across every page.</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    /// <summary>
    /// How many pages the match spans. Zero when nothing matched, so "page 1 of 0"
    /// reads as the empty result it is rather than as a page that failed to load.
    /// </summary>
    public int TotalPages =>
        PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// False on the last page and beyond it. Asking for page 50 of 3 is not an
    /// error — it is an empty page — but it must not offer a "next".
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>An empty page that still reports the requested shape.</summary>
    public static PagedResult<T> Empty(int page, int pageSize) =>
        new([], page, pageSize, 0);
}
