using Granit.Tax.Domain;
using Shouldly;
using Xunit;

namespace Granit.Tax.Tests.Domain;

public sealed class TaxRateOverrideTests
{
    // ======== Create ========

    [Fact]
    public void Create_ValidParameters_ShouldSetAllProperties()
    {
        var id = Guid.NewGuid();
        DateTimeOffset effectiveFrom = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset effectiveTo = new(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);

        var over = TaxRateOverride.Create(id, "BE", 0.22m, effectiveFrom, 0.06m, effectiveTo);

        over.Id.ShouldBe(id);
        over.CountryCode.ShouldBe("BE");
        over.StandardRate.ShouldBe(0.22m);
        over.ReducedRate.ShouldBe(0.06m);
        over.EffectiveFrom.ShouldBe(effectiveFrom);
        over.EffectiveTo.ShouldBe(effectiveTo);
    }

    [Fact]
    public void Create_WithoutOptionalParams_ShouldLeaveNulls()
    {
        DateTimeOffset effectiveFrom = DateTimeOffset.UtcNow;

        var over = TaxRateOverride.Create(Guid.NewGuid(), "FR", 0.20m, effectiveFrom);

        over.ReducedRate.ShouldBeNull();
        over.EffectiveTo.ShouldBeNull();
    }

    [Fact]
    public void Create_EmptyCountryCode_ShouldThrowArgumentException() =>
        Should.Throw<ArgumentException>(() =>
            TaxRateOverride.Create(Guid.NewGuid(), "", 0.21m, DateTimeOffset.UtcNow));

    [Fact]
    public void Create_WhitespaceCountryCode_ShouldThrowArgumentException() =>
        Should.Throw<ArgumentException>(() =>
            TaxRateOverride.Create(Guid.NewGuid(), "  ", 0.21m, DateTimeOffset.UtcNow));
}
