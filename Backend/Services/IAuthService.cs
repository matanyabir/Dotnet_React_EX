using Backend.DTOs;

namespace Backend.Services;

public interface IAuthService
{
    Task<LoginResponseDTO?> LoginAsync(LoginDTO loginDto);
    bool ValidateToken(string token);
}

