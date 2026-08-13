using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MX.Application.Abstractions;

namespace MX.Infrastructure.Email;

/// <summary>
/// Simulates sending by logging the message and keeping it in memory — the
/// README's suggested mock, and the default so the app runs with no credentials.
///
/// A real implementation of the port, not a no-op: it records what it was asked
/// to send, which is what lets tests assert "exactly one email, to this address"
/// instead of merely "nothing threw". Registered as a singleton so the log spans
/// the process rather than a request.
/// </summary>
public sealed class MockEmailSender(ILogger<MockEmailSender> logger) : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    /// <summary>Everything sent since startup, oldest first.</summary>
    public IReadOnlyList<EmailMessage> Sent => _sent.ToArray();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        _sent.Enqueue(message);

        // Logged at Information so the simulated mail is visible in the console
        // while running the exercise, which is how a reviewer sees it happen.
        logger.LogInformation(
            "[simulated email] To: {Recipient} | Subject: {Subject}\n{Body}",
            message.To,
            message.Subject,
            message.Body);

        return Task.CompletedTask;
    }

    /// <summary>Forgets everything sent. For tests that assert on counts.</summary>
    public void Clear() => _sent.Clear();
}
