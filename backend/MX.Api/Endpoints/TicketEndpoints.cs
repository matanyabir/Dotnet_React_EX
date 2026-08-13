using System.Text.Json;
using MX.Api.Authentication;
using MX.Application.Abstractions;
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

        // Accepts multipart/form-data (with an optional image) or plain JSON.
        // DisableAntiforgery because this endpoint is deliberately anonymous —
        // there is no session to forge, and requiring a token would lock out the
        // customers it exists for.
        group.MapPost("/", CreateTicketAsync)
            .WithName("CreateTicket")
            .WithSummary("Files a new ticket, optionally with an image. Open to anonymous customers.")
            .DisableAntiforgery();

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

    /// <summary>
    /// Reads the submission from either a multipart form (what the New Ticket
    /// modal sends, since it may carry an image) or a JSON body (what an API
    /// client or curl sends). Supporting both keeps the endpoint usable from a
    /// terminal without forcing every caller to build a multipart body.
    /// </summary>
    private static async Task<IResult> CreateTicketAsync(
        HttpRequest httpRequest,
        ITicketService tickets,
        IFileStorage fileStorage,
        CancellationToken cancellationToken)
    {
        CreateTicketRequest request;
        IFormFile? image = null;

        if (httpRequest.HasFormContentType)
        {
            var form = await httpRequest.ReadFormAsync(cancellationToken);

            request = new CreateTicketRequest(
                Name: form["name"].ToString(),
                Email: form["email"].ToString(),
                Description: form["description"].ToString());

            image = form.Files.GetFile("image");
        }
        else
        {
            CreateTicketRequest? body;
            try
            {
                body = await httpRequest.ReadFromJsonAsync<CreateTicketRequest>(cancellationToken);
            }
            catch (JsonException)
            {
                // Malformed or empty JSON is bad input, not a server fault — without
                // this it escapes as an unhandled exception and returns a 500.
                body = null;
            }

            if (body is null)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["request"] = ["A valid JSON request body is required."]
                    },
                    title: "The request could not be accepted");
            }

            request = body;
        }

        // Store the image first: a ticket that references a file we failed to
        // save would be worse than no ticket at all.
        if (image is { Length: > 0 })
        {
            await using var content = image.OpenReadStream();
            var stored = await fileStorage.SaveImageAsync(content, cancellationToken);

            if (stored.IsFailure)
            {
                return stored.ToHttpResult();
            }

            request = request with { ImageUrl = stored.Value };
        }

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
