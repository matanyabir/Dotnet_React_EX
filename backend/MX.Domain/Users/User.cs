namespace MX.Domain.Users;

/// <summary>
/// The roles the system recognises.
///
/// Strings rather than an enum because these travel in a JWT claim, where the
/// wire value is the contract — an enum's numeric backing would make the token
/// depend on declaration order.
/// </summary>
public static class UserRoles
{
    /// <summary>May edit tickets. The README's "only logged users can edit".</summary>
    public const string Admin = "Admin";
}

/// <summary>
/// Someone who can sign in.
///
/// Holds only the password *hash* — the plaintext never exists as state anywhere
/// in the system, so it cannot be logged, serialized, or leaked by a debugger.
/// </summary>
public sealed class User
{
    public User(string email, string passwordHash, string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        Email = email.Trim();
        PasswordHash = passwordHash;
        Role = role.Trim();
    }

    public string Email { get; }

    public string PasswordHash { get; }

    public string Role { get; }

    public bool IsAdmin => Role.Equals(UserRoles.Admin, StringComparison.OrdinalIgnoreCase);
}
