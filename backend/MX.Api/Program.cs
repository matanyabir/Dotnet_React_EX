var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Liveness probe. Real endpoints are mapped in Stage 4.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

/// <summary>
/// Exposed so the integration test project can boot this exact pipeline through
/// <c>WebApplicationFactory&lt;Program&gt;</c>. Top-level statements generate an
/// internal <c>Program</c> class, which the test host cannot reach otherwise.
/// </summary>
public partial class Program;
