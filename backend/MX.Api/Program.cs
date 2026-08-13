using MX.Api.Authentication;
using MX.Api.Endpoints;
using MX.Application;
using MX.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- composition
// Each layer registers itself. Read top-down, this is the whole dependency graph:
// the API knows about Application and Infrastructure, and neither knows about it.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddJwtAuthentication(builder.Configuration);

// Turns unhandled exceptions into RFC 9457 ProblemDetails instead of an HTML
// error page, so every failure the client sees has the same shape.
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string FrontendCorsPolicy = "frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

// ----------------------------------------------------------------- middleware
app.UseExceptionHandler();

// Gives bodyless failures (a 404 from an unmatched route, say) a ProblemDetails
// body too, so the client never has to special-case an empty response.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(FrontendCorsPolicy);

// Order matters: authentication establishes *who* the caller is, authorization
// then decides what they may do. Swapped, every protected endpoint would see an
// anonymous user and reject everyone.
app.UseAuthentication();
app.UseAuthorization();

// ------------------------------------------------------------------ endpoints
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithTags("Diagnostics")
    .WithSummary("Liveness probe.");

app.MapAuthEndpoints();
app.MapTicketEndpoints();

app.Run();

/// <summary>
/// Exposed so the integration test project can boot this exact pipeline through
/// <c>WebApplicationFactory&lt;Program&gt;</c>. Top-level statements generate an
/// internal <c>Program</c> class, which the test host cannot reach otherwise.
/// </summary>
public partial class Program;
