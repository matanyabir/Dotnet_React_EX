using MX.Api.Authentication;
using MX.Application.Tickets;
using MX.Application.Tickets.Dtos;
using MX.Domain.Tickets;

namespace MX.Api.Endpoints;

/// <summary>
/// The ticket HTTP surface, mapped as a group.
///
/// Kept out of Program.cs so the composition root stays readable, and grouped so
/// a cross-cutting concern — the admin authorization added in Stage 5 — attaches
/// to a route rather than being repeated per endpoint.
///
/// The handlers do nothing but translate: parse the request, call the service,
/// convert the result. All decisions live behind <see cref="ITicketService"/>,
/// which is what lets them be tested without a web host.
/// </summary>
internal static class TicketEndpoints
{
    public static RouteGroupBuilder MapTicketEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/tickets").WithTags("Tickets");

        group.MapGet("/", ListTicketsAsync)
            .WithName("ListTickets")
            .WithSummary("Lists tickets, optionally filtered by status and free text.");

        // Declared before "/{id:guid}" for readability; the guid constraint is what
        // actually keeps the two apart, so "statuses" can never be read as an id.
        group.MapGet("/statuses", GetStatuses)
            .WithName("GetTicketStatuses")
            .WithSummary("The status vocabulary, for the filter and edit dropdowns.");

        group.MapGet("/{id:guid}", GetTicketAsync)
            .WithName("GetTicket")
            .WithSummary("Fetches one ticket by its unique id.");

        group.MapPost("/", CreateTicketAsync)
            .WithName("CreateTicket")
            .WithSummary("Files a new ticket. Open to anonymous customers.");

        // The README's rule, expressed in one line: anyone may file a ticket,
        // only a signed-in admin may edit one.
        group.MapPut("/{id:guid}", UpdateTicketAsync)
            .WithName("UpdateTicket")
            .WithSummary("Updates status and/or resolution text. Admins only.")
            .RequireAuthorization(AuthenticationSetup.AdminPolicy);

        return group;
    }

    private static async Task<IResult> ListTicketsAsync(
        ITicketService tickets,
        string? status,
        string? search,
        CancellationToken cancellationToken)
    {
        var result = await tickets.ListAsync(new TicketQuery(status, search), cancellationToken);
        return result.ToHttpResult();
    }

    /// <summary>
    /// Served from the domain's own list so the frontend never hardcodes status
    /// strings — the drift this avoids is exactly what would break "In Progress".
    /// </summary>
    private static IResult GetStatuses() => Results.Ok(TicketStatusNames.All);

    private static async Task<IResult> GetTicketAsync(
        ITicketService tickets,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await tickets.GetByIdAsync(id, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> CreateTicketAsync(
        ITicketService tickets,
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tickets.CreateAsync(request, cancellationToken);

        // 201 with a Location header pointing at the new ticket — the same URL the
        // confirmation email's tracking link uses.
        return result.ToHttpResult(dto => Results.Created($"/api/tickets/{dto.Id}", dto));
    }

    private static async Task<IResult> UpdateTicketAsync(
        ITicketService tickets,
        Guid id,
        UpdateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tickets.UpdateAsync(id, request, cancellationToken);
        return result.ToHttpResult();
    }
}
