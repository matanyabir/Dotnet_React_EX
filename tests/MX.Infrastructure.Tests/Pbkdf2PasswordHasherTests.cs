using MX.Infrastructure.Security;

namespace MX.Infrastructure.Tests;

/// <summary>
/// The hasher is the one place where getting it subtly wrong is invisible until
/// it matters, so its properties are asserted directly rather than inferred from
/// login working.
///
/// A low iteration count keeps these fast; the cost factor is configuration, not
/// behaviour, and the cross-cost test below proves the two are independent.
/// </summary>
public class Pbkdf2PasswordHasherTests
{
    private static Pbkdf2PasswordHasher Fast() => new(iterations: 1_000);

    [Fact]
    public void Verifies_the_password_it_hashed()
    {
        var hasher = Fast();

        Assert.True(hasher.Verify("correct horse battery staple", hasher.Hash("correct horse battery staple")));
    }

    [Fact]
    public void Rejects_a_different_password()
    {
        var hasher = Fast();

        Assert.False(hasher.Verify("wrong password", hasher.Hash("correct password")));
    }

    [Fact]
    public void Rejects_a_password_differing_only_in_case()
    {
        var hasher = Fast();

        Assert.False(hasher.Verify("password", hasher.Hash("PASSWORD")));
    }

    [Fact]
    public void Hashing_the_same_password_twice_gives_different_results()
    {
        // Independent salts. Without them, identical passwords produce identical
        // hashes and one precomputed table cracks every account at once.
        var hasher = Fast();

        Assert.NotEqual(hasher.Hash("same password"), hasher.Hash("same password"));
    }

    [Fact]
    public void Both_hashes_of_the_same_password_still_verify()
    {
        var hasher = Fast();

        Assert.True(hasher.Verify("same password", hasher.Hash("same password")));
        Assert.True(hasher.Verify("same password", hasher.Hash("same password")));
    }

    [Fact]
    public void Stores_the_iteration_count_alongside_the_hash()
    {
        var parts = new Pbkdf2PasswordHasher(iterations: 4_242).Hash("anything").Split('.');

        Assert.Equal(3, parts.Length);
        Assert.Equal("4242", parts[0]);
    }

    [Fact]
    public void A_hash_made_at_one_cost_verifies_under_a_hasher_configured_for_another()
    {
        // Why the cost is stored per hash: raising it later must not invalidate
        // credentials already on disk.
        var cheap = new Pbkdf2PasswordHasher(iterations: 1_000);
        var expensive = new Pbkdf2PasswordHasher(iterations: 50_000);

        Assert.True(expensive.Verify("shared", cheap.Hash("shared")));
        Assert.True(cheap.Verify("shared", expensive.Hash("shared")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-separators")]
    [InlineData("only.two")]
    [InlineData("notanumber.c2FsdA==.aGFzaA==")]
    [InlineData("1000.!!!not-base64!!!.aGFzaA==")]
    [InlineData("0.c2FsdA==.aGFzaA==")]
    public void Treats_a_corrupt_stored_hash_as_a_rejection_rather_than_a_crash(string corrupt)
    {
        // A mangled config entry should lock the account, not take the API down
        // with an unhandled FormatException on the login path.
        Assert.False(Fast().Verify("any password", corrupt));
    }

    [Fact]
    public void Rejects_an_empty_candidate_password()
    {
        var hasher = Fast();

        Assert.False(hasher.Verify("", hasher.Hash("real password")));
    }

    [Fact]
    public void Refuses_to_hash_an_empty_password()
    {
        Assert.Throws<ArgumentException>(() => Fast().Hash(""));
    }

    [Fact]
    public void Refuses_a_nonsensical_iteration_count()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pbkdf2PasswordHasher(iterations: 0));
    }

    [Fact]
    public void The_stored_hash_does_not_contain_the_password()
    {
        Assert.DoesNotContain("hunter2", Fast().Hash("hunter2"), StringComparison.OrdinalIgnoreCase);
    }
}
