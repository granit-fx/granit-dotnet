using Granit.Parties.EntityFrameworkCore.Canonicalisation;
using Shouldly;
using Xunit;

namespace Granit.Parties.EntityFrameworkCore.Tests.Canonicalisation;

public sealed class TaxIdCanonicaliserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" - . / ")]   // separators only — nothing left after stripping
    public void Returns_null_for_null_or_whitespace_or_separators_only(string? input) =>
        TaxIdCanonicaliser.Canonicalise(input).ShouldBeNull();

    [Theory]
    // Belgian VAT in various display formats — all collapse to the same canonical form.
    [InlineData("BE 0123.456.789", "BE0123456789")]
    [InlineData("be0123456789", "BE0123456789")]
    [InlineData("BE-0123-456-789", "BE0123456789")]
    [InlineData("  be 0123/456/789  ", "BE0123456789")]
    // French VAT.
    [InlineData("FR12 345 678 901", "FR12345678901")]
    // German VAT.
    [InlineData("DE-123/456 789", "DE123456789")]
    // UK Companies House.
    [InlineData("01234567", "01234567")]
    public void Strips_separators_and_uppercases(string input, string expected) =>
        TaxIdCanonicaliser.Canonicalise(input).ShouldBe(expected);

    [Fact]
    public void Idempotent_on_already_canonical_input()
    {
        string canonical = "BE0123456789";
        string? once = TaxIdCanonicaliser.Canonicalise(canonical);
        string? twice = TaxIdCanonicaliser.Canonicalise(once);
        twice.ShouldBe(canonical);
    }
}
