using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Backend.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendTicketCreatedEmailAsync(string email, string ticketId, string description)
    {
        var subject = "כרטיס תמיכה חדש נוצר";
        var body = $@"
            <h2>כרטיס תמיכה חדש נוצר</h2>
            <p>מספר כרטיס: <strong>{ticketId}</strong></p>
            <p>תיאור הבעיה: {description}</p>
            <p>ניתן לעקוב אחרי הכרטיס בקישור: <a href='http://localhost:3000/tickets/{ticketId}'>לצפייה בכרטיס</a></p>
        ";
        
        await SendEmailAsync(email, subject, body);
    }

    public async Task SendStatusChangedEmailAsync(string email, string ticketId, string oldStatus, string newStatus)
    {
        var subject = "עדכון סטטוס כרטיס תמיכה";
        var body = $@"
            <h2>עדכון סטטוס כרטיס תמיכה</h2>
            <p>מספר כרטיס: <strong>{ticketId}</strong></p>
            <p>הסטטוס השתנה מ-<strong>{oldStatus}</strong> ל-<strong>{newStatus}</strong></p>
            <p><a href='http://localhost:3000/tickets/{ticketId}'>לצפייה בכרטיס</a></p>
        ";
        
        await SendEmailAsync(email, subject, body);
    }

    public async Task SendResolutionUpdatedEmailAsync(string email, string ticketId, string resolution)
    {
        var subject = "עדכון פתרון כרטיס תמיכה";
        var body = $@"
            <h2>עדכון פתרון כרטיס תמיכה</h2>
            <p>מספר כרטיס: <strong>{ticketId}</strong></p>
            <p>פתרון: {resolution}</p>
            <p><a href='http://localhost:3000/tickets/{ticketId}'>לצפייה בכרטיס</a></p>
        ";
        
        await SendEmailAsync(email, subject, body);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        try
        {
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = _configuration.GetValue<int>("Email:SmtpPort", 587);
            var smtpUser = _configuration["Email:SmtpUser"];
            var smtpPassword = _configuration["Email:SmtpPassword"];
            
            if (!string.IsNullOrEmpty(smtpHost) && !string.IsNullOrEmpty(smtpUser) && !string.IsNullOrEmpty(smtpPassword))
            {
                await SendViaSmtpAsync(toEmail, subject, body, smtpHost, smtpPort, smtpUser, smtpPassword);
            }
            else
            {
                _logger.LogInformation($"[EMAIL SIMULATION] To: {toEmail}, Subject: {subject}");
                _logger.LogInformation($"[EMAIL BODY] {body}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"שגיאה בשליחת אימייל ל-{toEmail}");
        }
    }

    private async Task SendViaSmtpAsync(string toEmail, string subject, string body, string host, int port, string user, string password)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("מערכת תמיכה", user));
        message.To.Add(new MailboxAddress("", toEmail));
        message.Subject = subject;
        
        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = body
        };
        message.Body = bodyBuilder.ToMessageBody();
        
        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(user, password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
        
        _logger.LogInformation($"אימייל נשלח בהצלחה ל-{toEmail}");
    }
}

