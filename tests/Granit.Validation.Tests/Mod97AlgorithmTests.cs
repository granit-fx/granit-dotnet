using Granit.Validation.Internal;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class Mod97AlgorithmTests
{
    // =========================================================================
    // Compute
    // =========================================================================

    [Fact]
    public void Compute_SingleDigit_ReturnsDigitMod97()
    {
        int result = Mod97Algorithm.Compute("5");

        result.ShouldBe(5);
    }

    [Fact]
    public void Compute_97_Returns0()
    {
        int result = Mod97Algorithm.Compute("97");

        result.ShouldBe(0);
    }

    [Fact]
    public void Compute_98_Returns1()
    {
        int result = Mod97Algorithm.Compute("98");

        result.ShouldBe(1);
    }

    [Fact]
    public void Compute_LargeNumber_DoesNotOverflow()
    {
        // A very large number that would overflow long arithmetic
        string largeNumber = "111111111111111111111111111111111111111111111";

        int result = Mod97Algorithm.Compute(largeNumber);

        result.ShouldBeGreaterThanOrEqualTo(0);
        result.ShouldBeLessThan(97);
    }

    // =========================================================================
    // LettersToDigits
    // =========================================================================

    [Fact]
    public void LettersToDigits_AllDigits_ReturnsUnchanged()
    {
        string result = Mod97Algorithm.LettersToDigits("12345");

        result.ShouldBe("12345");
    }

    [Fact]
    public void LettersToDigits_AConvertsTo10()
    {
        string result = Mod97Algorithm.LettersToDigits("A");

        result.ShouldBe("10");
    }

    [Fact]
    public void LettersToDigits_ZConvertsTo35()
    {
        string result = Mod97Algorithm.LettersToDigits("Z");

        result.ShouldBe("35");
    }

    [Fact]
    public void LettersToDigits_MixedAlphanumeric_ConvertsCorrectly()
    {
        // B = 11, E = 14
        string result = Mod97Algorithm.LettersToDigits("BE68");

        result.ShouldBe("111468");
    }

    [Fact]
    public void LettersToDigits_Lowercase_ConvertedToUppercase()
    {
        string result = Mod97Algorithm.LettersToDigits("a");

        result.ShouldBe("10");
    }
}
