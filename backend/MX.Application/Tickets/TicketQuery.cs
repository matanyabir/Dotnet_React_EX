namespace MX.Application.Tickets;

/// <summary>
/// The tickets screen's two filters, as one object.
///
/// Bundling them keeps the filtering rules in a single testable place instead of
/// spreading conditionals through the endpoint, and adding a third filter later
/// changes no method signatures.
/// </summary>
/// <param name="Status">
/// Display name such as "In Progress". Null, blank, or "All" means no filter —
/// "All" because that is the literal value the mockup's dropdown starts on.
/// </param>
/// <param name="Search">
/// Case-insensitive substring matched against name or description, per the README.
/// </param>
public sealed record TicketQuery(string? Status = null, string? Search = null)
{
    /// <summary>The dropdown's "show everything" option.</summary>
    public const string AnyStatus = "All";

    public static TicketQuery Unfiltered { get; } = new();

    public bool HasStatusFilter =>
        !string.IsNullOrWhiteSpace(Status) &&
        !Status.Trim().Equals(AnyStatus, StringComparison.OrdinalIgnoreCase);

    public bool HasSearchFilter => !string.IsNullOrWhiteSpace(Search);
}
