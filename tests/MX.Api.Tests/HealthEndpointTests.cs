using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MX.Api.Tests;

/// <summary>
/// Smoke test proving the test host can boot the real API pipeline. Stage 4
/// builds the ticket endpoint tests on this same foundation.
/// </summary>
public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Health_endpoint_reports_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ok", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
