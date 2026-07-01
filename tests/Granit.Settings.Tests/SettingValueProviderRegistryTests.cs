// =============================================================================
// SettingValueProviderRegistryTests - Unit tests for the provider registry
// =============================================================================
// Verifies that providers are sorted by ascending Order
// and that GetOrNull returns the correct provider or null.
// =============================================================================

using Granit.Settings.Providers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Settings.Tests;

public sealed class SettingValueProviderRegistryTests
{
    private static ISettingValueProvider MakeProvider(string name, int order)
    {
        ISettingValueProvider provider = Substitute.For<ISettingValueProvider>();
        provider.Name.Returns(name);
        provider.Order.Returns(order);
        return provider;
    }

    [Fact]
    public void Providers_Are_Sorted_By_Ascending_Order()
    {
        ISettingValueProvider global = MakeProvider("G", 300);
        ISettingValueProvider user = MakeProvider("U", 100);
        ISettingValueProvider defaultP = MakeProvider("D", 500);

        SettingValueProviderRegistry manager = new([global, user, defaultP]);

        manager.Providers.Select(p => p.Name).ShouldBe(new[] { "U", "G", "D" });
    }

    [Fact]
    public void GetOrNull_ReturnsProvider_ByName()
    {
        ISettingValueProvider user = MakeProvider("U", 100);
        ISettingValueProvider global = MakeProvider("G", 300);

        SettingValueProviderRegistry manager = new([user, global]);

        ISettingValueProvider? found = manager.GetOrNull("G");

        found.ShouldBeSameAs(global);
    }

    [Fact]
    public void GetOrNull_UnknownName_Returns_Null()
    {
        SettingValueProviderRegistry manager = new([MakeProvider("G", 300)]);

        ISettingValueProvider? found = manager.GetOrNull("X");

        found.ShouldBeNull();
    }

    [Fact]
    public void EmptyProviders_Produces_EmptyList()
    {
        SettingValueProviderRegistry manager = new([]);

        manager.Providers.ShouldBeEmpty();
    }

    [Fact]
    public void All_Five_Standard_Providers_Are_Ordered_U_T_G_C_D()
    {
        // Simulate the full cascade registered by AddGranitSettings
        ISettingValueProvider[] providers =
        [
            MakeProvider("D", 500),
            MakeProvider("C", 400),
            MakeProvider("G", 300),
            MakeProvider("T", 200),
            MakeProvider("U", 100),
        ];

        SettingValueProviderRegistry manager = new(providers);

        manager.Providers.Select(p => p.Name).ShouldBe(new[] { "U", "T", "G", "C", "D" });
    }
}
