namespace MX.Application.Abstractions;

/// <summary>One outbound message, in plain text.</summary>
public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>
/// Sends email.
///
/// The port the README's "generic integration with email service" asks for: the
/// mock and the SMTP implementation are interchangeable, and choosing between
/// them is a configuration value rather than a code change.
///
/// Implementations may throw — the dispatcher logs a failed handler and carries
/// on, because by the time an email is being sent the ticket is already saved and
/// a mail-server outage must not fail the customer's request.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
