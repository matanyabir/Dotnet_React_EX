namespace MX.Application.Abstractions;

/// <summary>
/// Turns a password into something safe to store, and checks a candidate against it.
///
/// A port rather than a direct call to a crypto API, so the algorithm can be
/// upgraded without touching the sign-in logic, and so tests can substitute a
/// fast hasher instead of paying for key derivation on every run.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a password for storage. Must salt each call independently.</summary>
    string Hash(string password);

    /// <summary>
    /// Checks a candidate password. Must compare in constant time so the answer
    /// cannot be recovered one character at a time by measuring how long it took.
    /// </summary>
    bool Verify(string password, string hash);
}
