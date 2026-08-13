using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MX.Api.Tests;

/// <summary>
/// Boots the real API pipeline over a throwaway copy of the dataset.
///
/// xUnit constructs a fresh test-class instance per test, so a factory created in
/// a test class's constructor gives every test its own file and its own singleton
/// repository. Tests can therefore write freely without ordering constraints or
/// leaking state into one another — and the committed dataset is never touched.
/// </summary>
public sealed class TicketApiFactory : WebApplicationFactory<Program>
{
    private static readonly string PristineDataset =
        Path.Combine(AppContext.BaseDirectory, "TestData", "dataset.json");

    public TicketApiFactory()
    {
        DataFilePath = Path.Combine(Path.GetTempPath(), $"mx-api-{Guid.NewGuid():N}.json");
        File.Copy(PristineDataset, DataFilePath);
    }

    /// <summary>The temp dataset this instance reads and writes.</summary>
    public string DataFilePath { get; }

    /// <summary>
    /// Matches the API's own serializer settings, so tests parse responses the way
    /// a real client would rather than by hand-massaging property names.
    /// </summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // An absolute path, so the result does not depend on the test host's
        // content root differing from the API project's.
        builder.UseSetting("Storage:DataFilePath", DataFilePath);
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(DataFilePath))
        {
            File.Delete(DataFilePath);
        }
    }
}
