using Granit.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain.ValueObjects;

public sealed class HexColorTests
{
    [Theory]
    [InlineData("#abc")]        // 3
    [InlineData("#abcd")]       // 4 (alpha)
    [InlineData("#8B5CF6")]     // 6
    [InlineData("#8B5CF6FF")]   // 8 (alpha)
    public void Create_accepts_3_4_6_or_8_hex_digits(string value)
    {
        var color = HexColor.Create(value);

        color.Value.ShouldBe(value.ToUpperInvariant());
    }

    [Fact]
    public void Create_normalises_to_upper_case_and_trims()
    {
        HexColor.Create("  #8b5cf6  ").Value.ShouldBe("#8B5CF6");
    }

    [Theory]
    [InlineData("8B5CF6")]      // missing '#'
    [InlineData("#12345")]      // 5 digits
    [InlineData("#GGGGGG")]     // non-hex
    [InlineData("#8B5CF")]      // too short
    public void Create_rejects_malformed_values(string value)
    {
        Should.Throw<ArgumentException>(() => HexColor.Create(value));
    }

    [Fact]
    public void Create_rejects_null_or_whitespace()
    {
        Should.Throw<ArgumentException>(() => HexColor.Create("  "));
    }

    [Fact]
    public void Implicit_conversions_round_trip_through_string()
    {
        HexColor color = "#10b981";
        string asString = color;

        asString.ShouldBe("#10B981");
    }
}
