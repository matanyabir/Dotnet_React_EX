using Microsoft.Extensions.Options;
using MX.Application.Abstractions;
using MX.Domain.Users;
using MX.Infrastructure.Configuration;

namespace MX.Infrastructure.Security;

/// <summary>
/// Serves sign-in accounts from configuration.
///
/// The exercise needs one admin and no registration flow, so configuration is the
/// honest storage choice — a user table would be ceremony around a single row.
/// Because it sits behind <see cref="IUserStore"/>, moving accounts into the JSON
/// file or a database later replaces this class and nothing else.
/// </summary>
public sealed class ConfiguredUserStore(IOptions<AuthOptions> options) : IUserStore
{
    private readonly IReadOnlyList<User> _users = options.Value.Users
        .Where(u => !string.IsNullOrWhiteSpace(u.Email) && !string.IsNullOrWhiteSpace(u.PasswordHash))
        .Select(u => new User(u.Email, u.PasswordHash, u.Role))
        .ToArray();

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // Email addresses are matched case-insensitively; nobody expects
        // Admin@example.com and admin@example.com to be different accounts.
        var match = _users.FirstOrDefault(u =>
            u.Email.Equals(email?.Trim(), StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(match);
    }
}
