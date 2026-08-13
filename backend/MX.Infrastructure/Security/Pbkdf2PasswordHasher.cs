using System.Security.Cryptography;
using MX.Application.Abstractions;

namespace MX.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing.
///
/// Deliberately slow: the work factor is the defence. A general-purpose hash such
/// as SHA-256 is far too fast to store passwords with, because the same speed
/// that makes it good for checksums lets an attacker try billions of guesses
/// against a stolen file.
///
/// Stored as <c>iterations.salt.hash</c>, all base64. Embedding the iteration
/// count means the cost can be raised later without invalidating hashes already
/// written — old ones keep verifying at their original setting.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    // OWASP's floor for PBKDF2-HMAC-SHA256 at the time of writing.
    private const int DefaultIterations = 600_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private readonly int _iterations;

    public Pbkdf2PasswordHasher(int iterations = DefaultIterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        _iterations = iterations;
    }

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        // A fresh salt per call, so two users with the same password get different
        // hashes and one precomputed table cannot crack both.
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, _iterations);

        return $"{_iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        var parts = hash.Split('.');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var iterations) ||
            iterations < 1)
        {
            // A malformed stored hash is a rejection, not a crash: a corrupt
            // config entry should lock the account, not take the API down.
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Derive(password, salt, iterations, expected.Length);

        // Constant-time: a plain == would return early on the first differing
        // byte, letting an attacker recover the hash one byte at a time.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int length = HashBytes) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, length);
}
