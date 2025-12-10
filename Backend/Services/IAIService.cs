namespace Backend.Services;

public interface IAIService
{
    Task<string?> GenerateSummaryAsync(string description);
}

