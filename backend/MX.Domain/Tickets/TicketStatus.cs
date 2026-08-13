namespace MX.Domain.Tickets;

/// <summary>
/// Lifecycle of a support ticket. The four members are exactly the values
/// present in the supplied dataset.
/// </summary>
public enum TicketStatus
{
    New,
    InProgress,
    Closed,
    Resolved
}

/// <summary>
/// Maps <see cref="TicketStatus"/> to and from the wire strings.
///
/// This lives in the domain on purpose: "In Progress" (with a space) is the
/// business's own vocabulary, and both the JSON store and the HTTP API must use
/// the identical spelling. Keeping one mapping here stops the two from drifting
/// apart — a mismatch would silently corrupt the dataset.
/// </summary>
public static class TicketStatusNames
{
    public const string New = "New";
    public const string InProgress = "In Progress";
    public const string Closed = "Closed";
    public const string Resolved = "Resolved";

    /// <summary>Every status in display form, for the frontend's filter dropdown.</summary>
    public static IReadOnlyList<string> All { get; } = [New, InProgress, Closed, Resolved];

    public static string ToDisplayName(this TicketStatus status) => status switch
    {
        TicketStatus.New => New,
        TicketStatus.InProgress => InProgress,
        TicketStatus.Closed => Closed,
        TicketStatus.Resolved => Resolved,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown ticket status.")
    };

    /// <summary>
    /// Lenient in what it accepts, strict in what it emits: both "In Progress"
    /// and "InProgress" parse, in any casing, but <see cref="ToDisplayName"/>
    /// only ever writes the canonical spelling.
    /// </summary>
    public static bool TryParse(string? value, out TicketStatus status)
    {
        switch (value?.Trim())
        {
            case { } s when s.Equals(New, StringComparison.OrdinalIgnoreCase):
                status = TicketStatus.New;
                return true;
            case { } s when s.Equals(InProgress, StringComparison.OrdinalIgnoreCase)
                         || s.Equals("InProgress", StringComparison.OrdinalIgnoreCase):
                status = TicketStatus.InProgress;
                return true;
            case { } s when s.Equals(Closed, StringComparison.OrdinalIgnoreCase):
                status = TicketStatus.Closed;
                return true;
            case { } s when s.Equals(Resolved, StringComparison.OrdinalIgnoreCase):
                status = TicketStatus.Resolved;
                return true;
            default:
                status = default;
                return false;
        }
    }
}
