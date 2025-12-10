namespace Backend.Services;

public interface IEmailService
{
    Task SendTicketCreatedEmailAsync(string email, string ticketId, string description);
    Task SendStatusChangedEmailAsync(string email, string ticketId, string oldStatus, string newStatus);
    Task SendResolutionUpdatedEmailAsync(string email, string ticketId, string resolution);
}

