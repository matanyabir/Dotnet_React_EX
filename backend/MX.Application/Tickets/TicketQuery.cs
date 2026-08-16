namespace MX.Application.Tickets;

/// <summary>
/// The tickets screen's filters and its position in the result, as one object.
///
/// Bundling them keeps the querying rules in a single testable place instead of
/// spreading conditionals through the endpoint, and adding a filter later changes
/// no method signatures.
/// </summary>
/// <param name="Status">
/// Display name such as "In Progress". Null, blank, or "All" means no filter —
/// "All" because that is the literal value the mockup's dropdown starts on.
/// </param>
/// <param name="Search">
/// Case-insensitive substring matched against name or description, per the README.
/// </param>
/// <param name="Page">1-based page number. Defaults to the first page.</param>
/// <param name="PageSize">
/// Rows per page, capped at <see cref="MaxPageSize"/> so one request cannot ask
/// the server to materialise an unbounded list.
/// </param>
public sealed record TicketQuery(
    string? Status = null,
    string? Search = null,
    int Page = TicketQuery.DefaultPage,
    int PageSize = TicketQuery.DefaultPageSize)
{
    /// <summary>The dropdown's "show everything" option.</summary>
    public const string AnyStatus = "All";

    public const int DefaultPage = 1;

    /// <summary>Fills a laptop screen without needing a scroll to reach the pager.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// The ceiling exists to protect the server, not to inconvenience the client:
    /// the whole point of paging is that no single request can be made arbitrarily
    /// expensive, and an uncapped <c>pageSize</c> would hand that back.
    /// </summary>
    public const int MaxPageSize = 100;

    public static TicketQuery Unfiltered { get; } = new();

    public bool HasStatusFilter =>
        !string.IsNullOrWhiteSpace(Status) &&
        !Status.Trim().Equals(AnyStatus, StringComparison.OrdinalIgnoreCase);

    public bool HasSearchFilter => !string.IsNullOrWhiteSpace(Search);

    /// <summary>Rows to skip to reach this page. Never negative for a valid query.</summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// What is wrong with the paging arguments, if anything.
    ///
    /// These are rejected rather than clamped. Silently turning
    /// <c>pageSize=1000</c> into 100 leaves a client believing it holds the whole
    /// result when it holds a tenth of it, and that is a bug that surfaces as
    /// missing data much later; a 400 surfaces it at the call site.
    ///
    /// A page past the end is <em>not</em> listed here — that is an empty page,
    /// which is the honest answer when a filter shrinks the result under someone.
    /// </summary>
    public IReadOnlyList<string> PagingErrors
    {
        get
        {
            var errors = new List<string>();

            if (Page < 1)
            {
                errors.Add($"Page must be 1 or greater. Received {Page}.");
            }

            if (PageSize < 1)
            {
                errors.Add($"Page size must be 1 or greater. Received {PageSize}.");
            }
            else if (PageSize > MaxPageSize)
            {
                errors.Add($"Page size must be {MaxPageSize} or fewer. Received {PageSize}.");
            }

            return errors;
        }
    }
}
