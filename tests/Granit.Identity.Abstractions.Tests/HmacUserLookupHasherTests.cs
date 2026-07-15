using Granit.Identity.Internal;
using Granit.Identity.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Abstractions.Tests;

/// <summary>
/// Coverage for the single lookup hasher shared by the local and federated encrypted-PII
/// stores (Vague 2b / G4). The digest must be deterministic and normalisation-stable so a
/// given email produces the same EmailHash on both sides; the pepper is mandatory.
/// </summary>
public sealed class HmacUserLookupHasherTests
{
    private static HmacUserLookupHasher Create(string? pepper = "unit-test-pepper-value") =>
        new(Microsoft.Extensions.Options.Options.Create(new UserLookupHasherOptions { Pepper = pepper }));

    [Fact]
    public void ComputeEmailHash_IsDeterministic_AcrossCaseAndWhitespace()
    {
        HmacUserLookupHasher hasher = Create();

        string? a = hasher.ComputeEmailHash("Jane.Doe@Example.com");
        string? b = hasher.ComputeEmailHash("  jane.doe@example.com  ");

        a.ShouldNotBeNull();
        a.ShouldBe(b);
    }

    [Fact]
    public void ComputeEmailHash_DiffersByInput()
    {
        HmacUserLookupHasher hasher = Create();

        hasher.ComputeEmailHash("a@example.com")
            .ShouldNotBe(hasher.ComputeEmailHash("b@example.com"));
    }

    [Fact]
    public void ComputeEmailHash_DiffersByPepper()
    {
        // Distinct peppers must yield distinct digests — this is exactly why a divergent
        // federated pepper cannot silently resolve against the unified index.
        Create("pepper-one").ComputeEmailHash("a@example.com")
            .ShouldNotBe(Create("pepper-two").ComputeEmailHash("a@example.com"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ComputeEmailHash_ReturnsNull_ForEmptyInput(string? input) =>
        Create().ComputeEmailHash(input).ShouldBeNull();

    [Fact]
    public void ComputePhoneHash_StripsSeparators()
    {
        HmacUserLookupHasher hasher = Create();

        string? spaced = hasher.ComputePhoneHash("+32 (470) 12-34.56");
        string? compact = hasher.ComputePhoneHash("+32470123456");

        spaced.ShouldNotBeNull();
        spaced.ShouldBe(compact);
    }

    [Fact]
    public void Constructor_Throws_WhenPepperMissing()
    {
        Action act = () => Create(pepper: null);

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("Identity:LookupHasher:Pepper");
    }
}
