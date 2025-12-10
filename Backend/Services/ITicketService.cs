using Backend.Models;
using Backend.DTOs;

namespace Backend.Services;

public interface ITicketService
{
    Task<List<TicketResponseDTO>> GetAllTicketsAsync(string? statusFilter = null, string? searchText = null);
    Task<TicketResponseDTO?> GetTicketByIdAsync(string id);
    Task<TicketResponseDTO> CreateTicketAsync(CreateTicketDTO createDto);
    Task<TicketResponseDTO?> UpdateTicketAsync(string id, UpdateTicketDTO updateDto);
}

