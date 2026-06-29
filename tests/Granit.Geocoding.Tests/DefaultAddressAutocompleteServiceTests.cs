using System.Diagnostics.Metrics;
using Granit.Domain.ValueObjects;
using Granit.Geocoding.Diagnostics;
using Granit.Geocoding.Internal;
using Granit.Geocoding.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Geocoding.Tests;

public sealed class DefaultAddressAutocompleteServiceTests
{
    private static readonly AddressSuggestion Sample = new(
        "Rue de la Loi 16, 1000 Brussels, BE",
        new PostalAddress("Rue de la Loi 16", "1000", "Brussels", "BE"),
        new GeoCoordinate(50.8503, 4.3517),
        GeocodeMatchPrecision.Rooftop);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SuggestAsync_NoProviders_ReturnsEmpty()
    {
        DefaultAddressAutocompleteService sut = CreateService([]);

        (await sut.SuggestAsync("brus", 5, Ct)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    public async Task SuggestAsync_BlankOrTooShortQuery_ReturnsEmptyWithoutCallingProvider(string query)
    {
        IAddressAutocompleteProvider provider = StubProvider("Photon", [Sample]);
        DefaultAddressAutocompleteService sut = CreateService([provider]);

        (await sut.SuggestAsync(query, 5, Ct)).ShouldBeEmpty();
        await provider.DidNotReceive().SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuggestAsync_FirstProviderHits_ReturnsItsSuggestions()
    {
        DefaultAddressAutocompleteService sut = CreateService([StubProvider("Photon", [Sample])]);

        IReadOnlyList<AddressSuggestion> result = await sut.SuggestAsync("brussels", 5, Ct);

        result.ShouldHaveSingleItem().ShouldBe(Sample);
    }

    [Fact]
    public async Task SuggestAsync_FirstProviderEmpty_FallsBackToNext()
    {
        IAddressAutocompleteProvider first = StubProvider("First", []);
        IAddressAutocompleteProvider second = StubProvider("Second", [Sample]);

        DefaultAddressAutocompleteService sut = CreateService(
            [first, second],
            new GranitGeocodingOptions { ProviderOrder = { "First", "Second" } });

        (await sut.SuggestAsync("brussels", 5, Ct)).ShouldHaveSingleItem().ShouldBe(Sample);
    }

    [Fact]
    public async Task SuggestAsync_FirstProviderThrows_DegradesToNext()
    {
        IAddressAutocompleteProvider faulty = Substitute.For<IAddressAutocompleteProvider>();
        faulty.ProviderName.Returns("Faulty");
        faulty.SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<AddressSuggestion>>>(_ => throw new InvalidOperationException("boom"));
        IAddressAutocompleteProvider healthy = StubProvider("Healthy", [Sample]);

        DefaultAddressAutocompleteService sut = CreateService(
            [faulty, healthy],
            new GranitGeocodingOptions { ProviderOrder = { "Faulty", "Healthy" } });

        (await sut.SuggestAsync("brussels", 5, Ct)).ShouldHaveSingleItem().ShouldBe(Sample);
    }

    [Theory]
    [InlineData(99, 10)]   // clamped down to the max
    [InlineData(0, 1)]     // clamped up to the min
    [InlineData(5, 5)]     // passed through
    public async Task SuggestAsync_ClampsLimitBeforeCallingProvider(int requested, int expected)
    {
        IAddressAutocompleteProvider provider = StubProvider("Photon", [Sample]);
        DefaultAddressAutocompleteService sut = CreateService([provider]);

        await sut.SuggestAsync("brussels", requested, Ct);

        await provider.Received(1).SuggestAsync("brussels", expected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuggestAsync_TrimsQueryBeforeCallingProvider()
    {
        IAddressAutocompleteProvider provider = StubProvider("Photon", [Sample]);
        DefaultAddressAutocompleteService sut = CreateService([provider]);

        await sut.SuggestAsync("  brussels  ", 5, Ct);

        await provider.Received(1).SuggestAsync("brussels", Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private static IAddressAutocompleteProvider StubProvider(string name, IReadOnlyList<AddressSuggestion> suggestions)
    {
        IAddressAutocompleteProvider provider = Substitute.For<IAddressAutocompleteProvider>();
        provider.ProviderName.Returns(name);
        provider.SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(suggestions);
        return provider;
    }

    private static DefaultAddressAutocompleteService CreateService(
        IEnumerable<IAddressAutocompleteProvider> providers,
        GranitGeocodingOptions? options = null) =>
        new(
            providers,
            Microsoft.Extensions.Options.Options.Create(options ?? new GranitGeocodingOptions()),
            CreateMetrics(),
            NullLogger<DefaultAddressAutocompleteService>.Instance);

    private static GeocodingMetrics CreateMetrics()
    {
        IMeterFactory factory = Substitute.For<IMeterFactory>();
        factory.Create(Arg.Any<MeterOptions>()).Returns(call => new Meter(call.Arg<MeterOptions>().Name));
        return new GeocodingMetrics(factory);
    }
}
