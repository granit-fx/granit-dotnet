using Granit.Parties.EntityFrameworkCore.Canonicalisation;
using Shouldly;
using Xunit;

namespace Granit.Parties.EntityFrameworkCore.Tests.Canonicalisation;

public sealed class PhoneCanonicaliserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Returns_null_for_null_or_whitespace(string? input) =>
        PhoneCanonicaliser.Canonicalise(input).ShouldBeNull();

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("+99999999999999999")]   // syntactically parseable but not a valid number
    [InlineData("0470123456")]            // domestic format without country code → ambiguous
    public void Returns_null_when_libphonenumber_cannot_parse_or_validate(string input) =>
        PhoneCanonicaliser.Canonicalise(input).ShouldBeNull();

    [Theory]
    // Belgian mobile in three different display formats — all collapse to the same E.164.
    [InlineData("+32 470 12 34 56", "+32470123456")]
    [InlineData("+32-470-12-34-56", "+32470123456")]
    [InlineData("+32470123456", "+32470123456")]
    // French mobile.
    [InlineData("+33 6 12 34 56 78", "+33612345678")]
    // US example (NANP).
    [InlineData("+1 415-555-2671", "+14155552671")]
    public void Normalises_E164_prefixed_input_to_strict_E164(string input, string expected) =>
        PhoneCanonicaliser.Canonicalise(input).ShouldBe(expected);

    [Fact]
    public void Idempotent_on_already_canonical_input()
    {
        string canonical = "+32470123456";
        string? once = PhoneCanonicaliser.Canonicalise(canonical);
        string? twice = PhoneCanonicaliser.Canonicalise(once);
        twice.ShouldBe(canonical);
    }
}
