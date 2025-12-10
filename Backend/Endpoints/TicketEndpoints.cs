using Backend.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Endpoints;

public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tickets").WithTags("Tickets");

        group.MapGet("/", async (ITicketService ticketService, string? status, string? search) =>
        {
            var tickets = await ticketService.GetAllTicketsAsync(status, search);
            return Results.Ok(tickets);
        })
        .WithName("GetAllTickets")
        .Produces<List<TicketResponseDTO>>();

        group.MapGet("/{id}", async (ITicketService ticketService, string id) =>
        {
            var ticket = await ticketService.GetTicketByIdAsync(id);
            if (ticket == null)
                return Results.NotFound();
            
            return Results.Ok(ticket);
        })
        .WithName("GetTicketById")
        .Produces<TicketResponseDTO>();

        group.MapPost("/", async (ITicketService ticketService, CreateTicketDTO createDto) =>
        {
            if (string.IsNullOrWhiteSpace(createDto.FullName) ||
                string.IsNullOrWhiteSpace(createDto.Email) ||
                string.IsNullOrWhiteSpace(createDto.Description))
            {
                return Results.BadRequest("שדות חובה חסרים");
            }

            var ticket = await ticketService.CreateTicketAsync(createDto);
            return Results.Created($"/api/tickets/{ticket.Id}", ticket);
        })
        .WithName("CreateTicket")
        .Produces<TicketResponseDTO>(201);

        group.MapPut("/{id}", [Authorize] async (ITicketService ticketService, string id, UpdateTicketDTO updateDto) =>
        {
            var ticket = await ticketService.UpdateTicketAsync(id, updateDto);
            if (ticket == null)
                return Results.NotFound();
            
            return Results.Ok(ticket);
        })
        .WithName("UpdateTicket")
        .Produces<TicketResponseDTO>();
    }
}

