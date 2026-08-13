namespace MX.Application.Notifications;

/// <summary>
/// The handful of values the notification text needs.
///
/// A plain record supplied by the composition root rather than IOptions, so the
/// application layer stays free of the options framework and these tests need no
/// configuration scaffolding to construct one.
/// </summary>
/// <param name="FrontendBaseUrl">
/// Where the customer's tracking link points — the frontend, not the API, since
/// a person following it wants the page and not the JSON.
/// </param>
public sealed record NotificationSettings(string FrontendBaseUrl)
{
    public static NotificationSettings Default { get; } = new("http://localhost:5173");

    /// <summary>The README's "tracking link": the deep link to one ticket.</summary>
    public string TrackingLinkFor(Guid ticketId) =>
        $"{FrontendBaseUrl.TrimEnd('/')}/tickets/{ticketId}";
}
