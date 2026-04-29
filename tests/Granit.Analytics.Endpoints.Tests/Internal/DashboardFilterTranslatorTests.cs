using Granit.Analytics.EntityFrameworkCore.Internal;
using Granit.Analytics.Internal;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Internal;

/// <summary>
/// Pins <see cref="DashboardFilterTranslator"/> shape — bare <c>field</c>
/// keys default to <c>field.eq</c>, pre-encoded <c>field.operator</c> keys
/// pass through, null/empty input produces null.
/// </summary>
public sealed class DashboardFilterTranslatorTests
{
    [Fact]
    public void ToQueryRequestFilter_NullInput_ReturnsNull()
    {
        DashboardFilterTranslator.ToQueryRequestFilter(null).ShouldBeNull();
    }

    [Fact]
    public void ToQueryRequestFilter_EmptyInput_ReturnsNull()
    {
        DashboardFilterTranslator.ToQueryRequestFilter(new Dictionary<string, string>()).ShouldBeNull();
    }

    [Fact]
    public void ToQueryRequestFilter_BareFieldKey_DefaultsToEqualityOperator()
    {
        IReadOnlyDictionary<string, string>? result = DashboardFilterTranslator.ToQueryRequestFilter(
            new Dictionary<string, string> { ["Status"] = "Open" });

        result.ShouldNotBeNull();
        result!.Count.ShouldBe(1);
        result["Status.eq"].ShouldBe("Open");
    }

    [Fact]
    public void ToQueryRequestFilter_OperatorEncodedKey_PassesThroughUnchanged()
    {
        // Caller wants gte/contains/in — pass the encoded form directly.
        IReadOnlyDictionary<string, string>? result = DashboardFilterTranslator.ToQueryRequestFilter(
            new Dictionary<string, string>
            {
                ["Amount.gte"] = "100",
                ["Name.contains"] = "Acme",
            });

        result.ShouldNotBeNull();
        result!["Amount.gte"].ShouldBe("100");
        result["Name.contains"].ShouldBe("Acme");
    }

    [Fact]
    public void ToQueryRequestFilter_MixedKeys_ProducesCanonicalDictionary()
    {
        IReadOnlyDictionary<string, string>? result = DashboardFilterTranslator.ToQueryRequestFilter(
            new Dictionary<string, string>
            {
                ["Status"] = "Open",
                ["Amount.gte"] = "100",
            });

        result.ShouldNotBeNull();
        result!.Count.ShouldBe(2);
        result["Status.eq"].ShouldBe("Open");
        result["Amount.gte"].ShouldBe("100");
    }
}
