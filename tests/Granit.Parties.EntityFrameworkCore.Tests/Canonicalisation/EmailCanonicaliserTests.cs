using Granit.Parties.EntityFrameworkCore.Canonicalisation;
using Shouldly;
using Xunit;

namespace Granit.Parties.EntityFrameworkCore.Tests.Canonicalisation;

public sealed class EmailCanonicaliserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("alice@")]
    [InlineData("alice@two@example.com")]
    public void Returns_null_for_invalid_input(string? input) =>
        EmailCanonicaliser.Canonicalise(input).ShouldBeNull();

    [Theory]
    [InlineData("Alice@Example.com", "alice@example.com")]
    [InlineData("  alice@example.com  ", "alice@example.com")]
    [InlineData("alice@EXAMPLE.COM", "alice@example.com")]
    public void Lowercases_and_trims_for_non_gmail(string input, string expected) =>
        EmailCanonicaliser.Canonicalise(input).ShouldBe(expected);

    [Theory]
    [InlineData("alice.smith@example.com")]
    [InlineData("alice+billing@example.com")]
    public void Does_NOT_strip_dots_or_plus_tags_outside_gmail(string input) =>
        // Outlook, Yahoo, Proton etc. treat dots and +tags as significant by default.
        // Only Gmail has the documented "dots are ignored" semantics.
        EmailCanonicaliser.Canonicalise(input).ShouldBe(input);

    [Theory]
    [InlineData("Alice@gmail.com", "alice@gmail.com")]
    [InlineData("a.l.i.c.e@gmail.com", "alice@gmail.com")]
    [InlineData("alice+billing@gmail.com", "alice@gmail.com")]
    [InlineData("a.lice+newsletter@gmail.com", "alice@gmail.com")]
    [InlineData("alice@googlemail.com", "alice@gmail.com")]
    [InlineData("a.l.ice+x@GoogleMail.com", "alice@gmail.com")]
    public void Strips_dots_and_plus_tags_for_gmail_and_googlemail(string input, string expected) =>
        EmailCanonicaliser.Canonicalise(input).ShouldBe(expected);

    [Theory]
    [InlineData(".@gmail.com")]            // local part collapses to empty after dot strip
    [InlineData("+tag@gmail.com")]         // local part collapses to empty after plus strip
    [InlineData(".+x@gmail.com")]          // both
    public void Returns_null_when_gmail_local_part_collapses_to_empty(string input) =>
        EmailCanonicaliser.Canonicalise(input).ShouldBeNull();
}
