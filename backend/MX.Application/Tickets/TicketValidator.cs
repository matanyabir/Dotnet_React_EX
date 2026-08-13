using System.Net.Mail;
using MX.Application.Tickets.Dtos;

namespace MX.Application.Tickets;

/// <summary>
/// Checks customer input before it reaches the domain.
///
/// The entity guards its own invariants too, but it throws — which is the right
/// behaviour for a programming error and the wrong one for a typo in a web form.
/// Validating here turns "you left the name blank" into a 400 with a readable
/// message, and collects every problem in one pass so the user is not sent round
/// the loop once per field.
/// </summary>
internal static class TicketValidator
{
    // Generous enough not to annoy anyone, small enough to keep the JSON file sane.
    private const int MaxNameLength = 200;
    private const int MaxEmailLength = 320; // RFC 5321 maximum
    private const int MaxDescriptionLength = 5_000;
    private const int MaxResolutionLength = 5_000;

    public static IReadOnlyList<string> Validate(CreateTicketRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("Name is required.");
        }
        else if (request.Name.Trim().Length > MaxNameLength)
        {
            errors.Add($"Name must be {MaxNameLength} characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors.Add("Email address is required.");
        }
        else if (request.Email.Trim().Length > MaxEmailLength)
        {
            errors.Add($"Email address must be {MaxEmailLength} characters or fewer.");
        }
        else if (!IsWellFormedEmail(request.Email))
        {
            errors.Add("Email address is not valid.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors.Add("Issue description is required.");
        }
        else if (request.Description.Trim().Length > MaxDescriptionLength)
        {
            errors.Add($"Issue description must be {MaxDescriptionLength} characters or fewer.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(UpdateTicketRequest request)
    {
        var errors = new List<string>();

        // Status is optional, but a value that is present must be one we recognise —
        // silently ignoring a typo would leave the admin thinking the save worked.
        if (request.Status is not null && !Domain.Tickets.TicketStatusNames.TryParse(request.Status, out _))
        {
            errors.Add(
                $"'{request.Status}' is not a valid status. Expected one of: " +
                $"{string.Join(", ", Domain.Tickets.TicketStatusNames.All)}.");
        }

        if (request.Resolution is { } resolution && resolution.Trim().Length > MaxResolutionLength)
        {
            errors.Add($"Resolution must be {MaxResolutionLength} characters or fewer.");
        }

        return errors;
    }

    /// <summary>
    /// Uses the framework's own parser rather than a hand-rolled regex — email
    /// grammar is far nastier than it looks and a homemade pattern reliably
    /// rejects addresses that are perfectly legal.
    /// </summary>
    private static bool IsWellFormedEmail(string candidate)
    {
        var trimmed = candidate.Trim();

        // TryCreate also accepts the display-name form ("Ada <ada@example.com>").
        // Comparing back to the input rejects that, since a bare address is what
        // the form asks for and what the mock mailer will send to.
        return MailAddress.TryCreate(trimmed, out var parsed)
               && string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase)
               && parsed.Host.Contains('.', StringComparison.Ordinal);
    }
}
