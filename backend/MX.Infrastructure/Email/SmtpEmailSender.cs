using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MX.Application.Abstractions;
using MX.Infrastructure.Configuration;

namespace MX.Infrastructure.Email;

/// <summary>
/// Delivers mail over SMTP via MailKit — the README's Gmail/SendGrid bonus.
///
/// Interchangeable with <see cref="MockEmailSender"/>: same port, selected by the
/// "Email:Provider" setting, so switching to real delivery changes no code. That
/// substitutability is the whole reason the port exists.
///
/// A connection is opened per message. That is the wrong trade for bulk sending
/// and the right one here, where mail is occasional and a pooled connection would
/// be idle almost always and stale when finally needed.
/// </summary>
public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var mime = new MimeMessage
        {
            Subject = message.Subject,
            Body = new TextPart("plain") { Text = message.Body }
        };

        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));

        using var client = new SmtpClient();

        var security = _options.Smtp.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.Auto;

        await client.ConnectAsync(_options.Smtp.Host, _options.Smtp.Port, security, cancellationToken)
            .ConfigureAwait(false);

        // Anonymous relays exist; only authenticate when a username is configured.
        if (!string.IsNullOrWhiteSpace(_options.Smtp.Username))
        {
            await client.AuthenticateAsync(_options.Smtp.Username, _options.Smtp.Password, cancellationToken)
                .ConfigureAwait(false);
        }

        await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Sent email to {Recipient} with subject {Subject}.", message.To, message.Subject);
    }
}
