namespace MX.Infrastructure.Configuration;

/// <summary>Which email implementation to use.</summary>
public enum EmailProvider
{
    /// <summary>Log and record, send nothing. The default.</summary>
    Mock = 0,

    /// <summary>Deliver over SMTP.</summary>
    Smtp
}

/// <summary>SMTP connection details. Only read when the provider is Smtp.</summary>
public sealed class SmtpOptions
{
    public string Host { get; set; } = "smtp.gmail.com";

    /// <summary>587 is the submission port used with STARTTLS.</summary>
    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// For Gmail this is an app password, not the account password. Belongs in
    /// user-secrets or an environment variable, never in a committed file.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Email settings, bound from the "Email" section.
///
/// <see cref="Provider"/> is the whole switch between simulated and real
/// delivery — the point of putting both behind <c>IEmailSender</c>.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public EmailProvider Provider { get; set; } = EmailProvider.Mock;

    public string FromAddress { get; set; } = "support@example.com";

    public string FromName { get; set; } = "MX Support";

    /// <summary>Base URL used to build the customer's tracking link.</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    public SmtpOptions Smtp { get; set; } = new();
}
