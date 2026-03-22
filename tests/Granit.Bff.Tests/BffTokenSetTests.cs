using Shouldly;
using Xunit;

namespace Granit.Bff.Tests;

public sealed class BffTokenSetTests
{
    [Fact]
    public void Properties_AreAccessible()
    {
        var expiresAt = new DateTimeOffset(2026, 3, 22, 18, 0, 0, TimeSpan.Zero);

        var tokens = new BffTokenSet("access-token", "refresh-token", "id-token", expiresAt);

        tokens.AccessToken.ShouldBe("access-token");
        tokens.RefreshToken.ShouldBe("refresh-token");
        tokens.IdToken.ShouldBe("id-token");
        tokens.ExpiresAt.ShouldBe(expiresAt);
    }

    [Fact]
    public void Properties_AllowNullOptionalTokens()
    {
        var tokens = new BffTokenSet("access-only", null, null, DateTimeOffset.UtcNow);

        tokens.RefreshToken.ShouldBeNull();
        tokens.IdToken.ShouldBeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var expiresAt = new DateTimeOffset(2026, 3, 22, 18, 0, 0, TimeSpan.Zero);
        var tokens1 = new BffTokenSet("at", "rt", "idt", expiresAt);
        var tokens2 = new BffTokenSet("at", "rt", "idt", expiresAt);

        tokens1.ShouldBe(tokens2);
        (tokens1 == tokens2).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var expiresAt = new DateTimeOffset(2026, 3, 22, 18, 0, 0, TimeSpan.Zero);
        var tokens1 = new BffTokenSet("at-1", "rt", "idt", expiresAt);
        var tokens2 = new BffTokenSet("at-2", "rt", "idt", expiresAt);

        tokens1.ShouldNotBe(tokens2);
        (tokens1 != tokens2).ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_SameValues_ProduceSameHash()
    {
        var expiresAt = new DateTimeOffset(2026, 3, 22, 18, 0, 0, TimeSpan.Zero);
        var tokens1 = new BffTokenSet("at", "rt", "idt", expiresAt);
        var tokens2 = new BffTokenSet("at", "rt", "idt", expiresAt);

        tokens1.GetHashCode().ShouldBe(tokens2.GetHashCode());
    }

    [Fact]
    public void ToString_ContainsTypeName()
    {
        var tokens = new BffTokenSet("at", "rt", "idt", DateTimeOffset.UtcNow);

        tokens.ToString().ShouldContain("BffTokenSet");
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new BffTokenSet("at", "rt", "idt", DateTimeOffset.UtcNow);

        BffTokenSet modified = original with { AccessToken = "new-at" };

        modified.AccessToken.ShouldBe("new-at");
        modified.RefreshToken.ShouldBe("rt");
        original.AccessToken.ShouldBe("at");
    }
}
