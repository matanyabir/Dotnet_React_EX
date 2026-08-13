using MX.Domain.Users;

namespace MX.Application.Abstractions;

/// <summary>
/// Looks up accounts that may sign in.
///
/// The exercise needs one admin, supplied by configuration, but keeping this a
/// port means moving accounts into the JSON file or a database later is a new
/// adapter rather than a change to sign-in.
/// </summary>
public interface IUserStore
{
    /// <returns>The matching user, or <c>null</c> when there is no such account.</returns>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
}
