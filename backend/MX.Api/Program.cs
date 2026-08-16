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
builder.Services.AddLoginRateLimiting(builder.Configuration);

// Turns unhandled exceptions into RFC 9457 ProblemDetails instead of an HTML
// error page, so every failure the client sees has the same shape.
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string FrontendCorsPolicy = "frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        // The session travels as a cookie, and a browser will not attach one to a
        // cross-origin request — nor let the page read the response — unless the
        // server says credentials are allowed. Note that this is incompatible
        // with a wildcard origin by specification, which is why both branches
        // below name the origins they trust.
        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();

        if (builder.Environment.IsDevelopment())
        {
            // Dev servers take whatever port is free — Vite moves to 5174 when
            // something else already holds 5173 — so in development the check is
            // "is this the local machine", not "is this one exact port". Without
            // this, a developer whose 5173 is occupied gets a frontend that
            // renders but silently fails every request.
            //
            // Development only: outside it, the configured allowlist applies
            // strictly, because "any loopback origin" is not a boundary worth
            // anything on a real host.
            policy.SetIsOriginAllowed(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback);
        }
        else
        {
            policy.WithOrigins(allowedOrigins);
        }
    }));

var app = builder.Build();

// Uploaded images live under wwwroot and are served straight from disk. Created
// eagerly because the static file middleware needs the web root to exist, and a
// fresh clone has never received an upload.
Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "uploads"));

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

// Serves uploaded ticket images from wwwroot at /uploads/{file}. Static files
// come before auth on purpose: a customer viewing their own ticket's photo is
// not signed in, and the filenames are unguessable GUIDs.
app.UseStaticFiles();

app.UseCors(FrontendCorsPolicy);

// After CORS, before authentication, and both placements matter. After, because
// a 429 the browser is not allowed to read is indistinguishable from the API
// being down, and the CORS headers are what make it readable. Before, because
// the point of turning a flood away is to turn it away without first paying for
// a password hash.
app.UseRateLimiter();

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
