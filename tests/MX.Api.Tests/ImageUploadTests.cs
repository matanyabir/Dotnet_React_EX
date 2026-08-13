using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using MX.Application.Tickets.Dtos;

namespace MX.Api.Tests;

/// <summary>
/// The New Ticket modal's image field, end to end: multipart in, a served image
/// out. Covers what the storage unit tests cannot — form binding, the static
/// file route, and that a rejected upload takes the whole request down with it
/// rather than leaving a half-made ticket.
/// </summary>
public sealed class ImageUploadTests : IDisposable
{
    private readonly TicketApiFactory _factory = new();
    private readonly HttpClient _client;

    public ImageUploadTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>A minimal but genuine PNG header — enough for signature detection.</summary>
    private static byte[] PngBytes() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        .. Encoding.ASCII.GetBytes("test image payload")
    ];

    private static MultipartFormDataContent Form(
        byte[]? image = null,
        string imageFileName = "photo.png",
        string imageContentType = "image/png",
        string name = "Ada Lovelace",
        string email = "ada@example.com",
        string description = "The printer is on fire, photo attached.")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(name), "name" },
            { new StringContent(email), "email" },
            { new StringContent(description), "description" }
        };

        if (image is not null)
        {
            var file = new ByteArrayContent(image);
            file.Headers.ContentType = new MediaTypeHeaderValue(imageContentType);
            form.Add(file, "image", imageFileName);
        }

        return form;
    }

    // ------------------------------------------------------------ happy path

    [Fact]
    public async Task Posting_a_multipart_ticket_with_an_image_records_its_path()
    {
        using var form = Form(PngBytes());

        var response = await _client.PostAsync("/api/tickets", form);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<TicketDto>(TicketApiFactory.Json);

        Assert.NotNull(created!.ImageUrl);
        Assert.StartsWith("uploads/", created.ImageUrl, StringComparison.Ordinal);
        Assert.EndsWith(".png", created.ImageUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_uploaded_image_is_served_back_over_http()
    {
        using var form = Form(PngBytes());
        var response = await _client.PostAsync("/api/tickets", form);
        var created = await response.Content.ReadFromJsonAsync<TicketDto>(TicketApiFactory.Json);

        // The stored path is relative; the served URL is that path from the root.
        var image = await _client.GetAsync($"/{created!.ImageUrl}");

        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PngBytes(), await image.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Serving_an_image_needs_no_authentication()
    {
        // A customer following the tracking link to see their own photo is not
        // signed in; the unguessable GUID filename is what protects it.
        using var form = Form(PngBytes());
        var created = await (await _client.PostAsync("/api/tickets", form))
            .Content.ReadFromJsonAsync<TicketDto>(TicketApiFactory.Json);

        using var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/{created!.ImageUrl}")).StatusCode);
    }

    [Fact]
    public async Task A_multipart_ticket_without_an_image_is_still_accepted()
    {
        // The image is optional — most tickets will not have one.
        using var form = Form(image: null);

        var response = await _client.PostAsync("/api/tickets", form);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<TicketDto>(TicketApiFactory.Json);
        Assert.Null(created!.ImageUrl);
    }

    [Fact]
    public async Task The_image_path_is_persisted_with_the_ticket()
    {
        using var form = Form(PngBytes());
        var created = await (await _client.PostAsync("/api/tickets", form))
            .Content.ReadFromJsonAsync<TicketDto>(TicketApiFactory.Json);

        var onDisk = await File.ReadAllTextAsync(_factory.DataFilePath);

        Assert.Contains(created!.ImageUrl!, onDisk, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------ rejections

    [Fact]
    public async Task A_non_image_upload_is_rejected_with_a_validation_problem()
    {
        using var form = Form(Encoding.ASCII.GetBytes("#!/bin/sh\necho not an image\n"));

        var response = await _client.PostAsync("/api/tickets", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_file_lying_about_its_type_is_still_rejected()
    {
        // Declared image/png, named .png, and neither is true. Only reading the
        // bytes catches this.
        using var form = Form(
            Encoding.ASCII.GetBytes("MZ\x90\x00 this is an executable"),
            imageFileName: "innocent.png",
            imageContentType: "image/png");

        var response = await _client.PostAsync("/api/tickets", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_rejected_image_creates_no_ticket_at_all()
    {
        // The upload is stored before the ticket, so a bad image fails the whole
        // request rather than leaving a ticket pointing at a file that is not there.
        var before = await File.ReadAllTextAsync(_factory.DataFilePath);

        using var form = Form(Encoding.ASCII.GetBytes("definitely not an image"));
        await _client.PostAsync("/api/tickets", form);

        Assert.Equal(before, await File.ReadAllTextAsync(_factory.DataFilePath));
    }

    [Fact]
    public async Task Invalid_ticket_fields_are_still_validated_in_a_multipart_request()
    {
        using var form = Form(PngBytes(), name: "", email: "not-an-email");

        var response = await _client.PostAsync("/api/tickets", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --------------------------------------------------- JSON still supported

    [Fact]
    public async Task A_plain_json_ticket_still_works_alongside_multipart()
    {
        // Multipart is what the browser form sends; JSON keeps the endpoint
        // usable from curl or any API client without building a multipart body.
        var response = await _client.PostAsJsonAsync("/api/tickets",
            new CreateTicketRequest("Grace Hopper", "grace@example.com", "There is a moth in the relay."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TicketDto>(TicketApiFactory.Json);
        Assert.Null(created!.ImageUrl);
    }

    [Fact]
    public async Task An_empty_body_is_a_validation_error_rather_than_a_crash()
    {
        using var empty = new StringContent("", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/tickets", empty);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
