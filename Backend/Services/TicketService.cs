using Backend.Models;
using Backend.DTOs;
using Newtonsoft.Json;

namespace Backend.Services;

public class TicketService : ITicketService
{
    private readonly string _dataFilePath;
    private readonly IEmailService _emailService;
    private readonly IAIService _aiService;
    private readonly ILogger<TicketService> _logger;

    public TicketService(IEmailService emailService, IAIService aiService, ILogger<TicketService> logger)
    {
        _dataFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "tickets.json");
        _emailService = emailService;
        _aiService = aiService;
        _logger = logger;
        
        var dataDir = Path.GetDirectoryName(_dataFilePath);
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir!);
        }
        
        if (!File.Exists(_dataFilePath))
        {
            File.WriteAllText(_dataFilePath, "[]");
        }
    }

    private async Task<List<Ticket>> ReadTicketsAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(_dataFilePath);
            var tickets = JsonConvert.DeserializeObject<List<Ticket>>(json) ?? new List<Ticket>();
            return tickets;
        }
        catch
        {
            return new List<Ticket>();
        }
    }

    private async Task SaveTicketsAsync(List<Ticket> tickets)
    {
        var json = JsonConvert.SerializeObject(tickets, Formatting.Indented);
        await File.WriteAllTextAsync(_dataFilePath, json);
    }

    public async Task<List<TicketResponseDTO>> GetAllTicketsAsync(string? statusFilter = null, string? searchText = null)
    {
        var tickets = await ReadTicketsAsync();
        
        if (!string.IsNullOrEmpty(statusFilter))
        {
            tickets = tickets.Where(t => t.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        
        if (!string.IsNullOrEmpty(searchText))
        {
            var searchLower = searchText.ToLower();
            tickets = tickets.Where(t => 
                t.FullName.ToLower().Contains(searchLower) || 
                t.Description.ToLower().Contains(searchLower)
            ).ToList();
        }
        
        return tickets.Select(MapToDTO).ToList();
    }

    public async Task<TicketResponseDTO?> GetTicketByIdAsync(string id)
    {
        var tickets = await ReadTicketsAsync();
        var ticket = tickets.FirstOrDefault(t => t.Id == id);
        return ticket != null ? MapToDTO(ticket) : null;
    }

    public async Task<TicketResponseDTO> CreateTicketAsync(CreateTicketDTO createDto)
    {
        var tickets = await ReadTicketsAsync();
        
        var newTicket = new Ticket
        {
            Id = Guid.NewGuid().ToString(),
            FullName = createDto.FullName,
            Email = createDto.Email,
            Description = createDto.Description,
            Status = "Open",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        tickets.Add(newTicket);
        await SaveTicketsAsync(tickets);
        
        await _emailService.SendTicketCreatedEmailAsync(newTicket.Email, newTicket.Id, newTicket.Description);
        
        _ = Task.Run(async () =>
        {
            try
            {
                var summary = await _aiService.GenerateSummaryAsync(newTicket.Description);
                if (!string.IsNullOrEmpty(summary))
                {
                    newTicket.AISummary = summary;
                    newTicket.UpdatedAt = DateTime.UtcNow;
                    
                    var updatedTickets = await ReadTicketsAsync();
                    var ticketToUpdate = updatedTickets.FirstOrDefault(t => t.Id == newTicket.Id);
                    if (ticketToUpdate != null)
                    {
                        ticketToUpdate.AISummary = summary;
                        ticketToUpdate.UpdatedAt = DateTime.UtcNow;
                        await SaveTicketsAsync(updatedTickets);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה ביצירת AI Summary");
            }
        });
        
        return MapToDTO(newTicket);
    }

    public async Task<TicketResponseDTO?> UpdateTicketAsync(string id, UpdateTicketDTO updateDto)
    {
        var tickets = await ReadTicketsAsync();
        var ticket = tickets.FirstOrDefault(t => t.Id == id);
        
        if (ticket == null)
            return null;
        
        var oldStatus = ticket.Status;
        var oldResolution = ticket.Resolution;
        
        if (!string.IsNullOrEmpty(updateDto.Status))
        {
            ticket.Status = updateDto.Status;
        }
        
        if (updateDto.Resolution != null)
        {
            ticket.Resolution = updateDto.Resolution;
        }
        
        ticket.UpdatedAt = DateTime.UtcNow;
        
        await SaveTicketsAsync(tickets);
        
        if (oldStatus != ticket.Status)
        {
            await _emailService.SendStatusChangedEmailAsync(ticket.Email, ticket.Id, oldStatus, ticket.Status);
        }
        
        if (oldResolution != ticket.Resolution && !string.IsNullOrEmpty(ticket.Resolution))
        {
            await _emailService.SendResolutionUpdatedEmailAsync(ticket.Email, ticket.Id, ticket.Resolution);
        }
        
        return MapToDTO(ticket);
    }

    private TicketResponseDTO MapToDTO(Ticket ticket)
    {
        return new TicketResponseDTO
        {
            Id = ticket.Id,
            FullName = ticket.FullName,
            Email = ticket.Email,
            Description = ticket.Description,
            Status = ticket.Status,
            AISummary = ticket.AISummary,
            Resolution = ticket.Resolution,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt
        };
    }
}

