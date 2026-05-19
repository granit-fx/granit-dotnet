using Granit.Http.Cookies;
using Granit.Privacy.Regulations.Cookies.Internal;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Regulations.Cookies.Tests;

public sealed class RegulationBasedCookieConsentModelProviderTests
{
    [Fact]
    public async Task GetConsentModelAsync_CcpaProfile_ReturnOptOutWithGpc()
    {
        IPrivacyRegulationResolver resolver = CreateResolver(ConsentModel.OptOut, true);
        RegulationBasedCookieConsentModelProvider sut = new(resolver);
        DefaultHttpContext httpContext = new();

        ConsentModelInfo? result = await sut.GetConsentModelAsync(httpContext);

        result.ShouldNotBeNull();
        result.Mode.ShouldBe(CookieConsentMode.OptOut);
        result.HonorGlobalPrivacyControl.ShouldBeTrue();
    }

    [Fact]
    public async Task GetConsentModelAsync_GdprProfile_ReturnsOptInWithoutGpc()
    {
        IPrivacyRegulationResolver resolver = CreateResolver(ConsentModel.OptIn, false);
        RegulationBasedCookieConsentModelProvider sut = new(resolver);
        DefaultHttpContext httpContext = new();

        ConsentModelInfo? result = await sut.GetConsentModelAsync(httpContext);

        result.ShouldNotBeNull();
        result.Mode.ShouldBe(CookieConsentMode.OptIn);
        result.HonorGlobalPrivacyControl.ShouldBeFalse();
    }

    [Fact]
    public async Task GetConsentModelAsync_MemoizesPerRequest()
    {
        IPrivacyRegulationResolver resolver = CreateResolver(ConsentModel.OptIn, false);
        RegulationBasedCookieConsentModelProvider sut = new(resolver);
        DefaultHttpContext httpContext = new();

        ConsentModelInfo? first = await sut.GetConsentModelAsync(httpContext);
        ConsentModelInfo? second = await sut.GetConsentModelAsync(httpContext);

        first.ShouldBe(second);
        await resolver.Received(1).ResolveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetConsentModelAsync_DifferentRequests_ResolveSeparately()
    {
        IPrivacyRegulationResolver resolver = CreateResolver(ConsentModel.OptOut, true);
        RegulationBasedCookieConsentModelProvider sut = new(resolver);

        await sut.GetConsentModelAsync(new DefaultHttpContext());
        await sut.GetConsentModelAsync(new DefaultHttpContext());

        await resolver.Received(2).ResolveAsync(Arg.Any<CancellationToken>());
    }

    private static IPrivacyRegulationResolver CreateResolver(ConsentModel cookieModel, bool honorGpc)
    {
        IPrivacyRegulationResolver resolver = Substitute.For<IPrivacyRegulationResolver>();
        resolver.ResolveAsync(Arg.Any<CancellationToken>()).Returns(new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.EuGdpr,
            DisplayName = "Test",
            JurisdictionCode = "XX",
            ConsentModel = cookieModel,
            AvailableLegalBases = [],
            SubjectAccessRequestDays = 30,
            DefaultDeletionGracePeriodDays = 30,
            MaxDeletionGracePeriodDays = 90,
            CookieConsentModel = cookieModel,
            HonorGlobalPrivacyControl = honorGpc,
        });
        return resolver;
    }
}
