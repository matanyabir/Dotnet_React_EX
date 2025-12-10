using Backend.DTOs;
using Backend.Services;

namespace Backend.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (IAuthService authService, LoginDTO loginDto) =>
        {
            if (string.IsNullOrWhiteSpace(loginDto.Username) || string.IsNullOrWhiteSpace(loginDto.Password))
            {
                return Results.BadRequest("שם משתמש וסיסמה נדרשים");
            }

            var result = await authService.LoginAsync(loginDto);
            if (result == null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(result);
        })
        .WithName("Login")
        .Produces<LoginResponseDTO>();

        group.MapGet("/validate", [Microsoft.AspNetCore.Authorization.Authorize] () =>
        {
            return Results.Ok(new { valid = true });
        })
        .WithName("ValidateToken");
    }
}

