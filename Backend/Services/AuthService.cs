using Backend.DTOs;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;

namespace Backend.Services;

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    private readonly Dictionary<string, string> _users = new()
    {
        { "admin", BCrypt.Net.BCrypt.HashPassword("admin123") },
        { "user", BCrypt.Net.BCrypt.HashPassword("user123") }
    };

    public AuthService(IConfiguration configuration, ILogger<AuthService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LoginResponseDTO?> LoginAsync(LoginDTO loginDto)
    {
        await Task.CompletedTask;
        
        if (!_users.ContainsKey(loginDto.Username))
            return null;
        
        if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, _users[loginDto.Username]))
            return null;
        
        var token = GenerateJwtToken(loginDto.Username);
        
        return new LoginResponseDTO
        {
            Token = token,
            Username = loginDto.Username
        };
    }

    private string GenerateJwtToken(string username)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "YourSuperSecretKeyForJWTTokenGeneration12345";
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "TicketSystem";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "TicketSystemUsers";
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, "Admin")
        };
        
        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool ValidateToken(string token)
    {
        try
        {
            var jwtKey = _configuration["Jwt:Key"] ?? "YourSuperSecretKeyForJWTTokenGeneration12345";
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? "TicketSystem";
            var jwtAudience = _configuration["Jwt:Audience"] ?? "TicketSystemUsers";
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwtKey);
            
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);
            
            return true;
        }
        catch
        {
            return false;
        }
    }
}

