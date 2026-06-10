using Granit.Authentication.External.Options;
using Granit.OpenIddict.Services;
using Microsoft.AspNetCore.Authentication;
using NSubstitute;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.OpenIddict.Tests;

public sealed class OpenIddictExternalProviderRegistryTests
{
    private readonly IAuthenticationSchemeProvider _schemeProvider = Substitute.For<IAuthenticationSchemeProvider>();

    private OpenIddictExternalProviderRegistry CreateSut(params string[] configuredProviders)
    {
        Microsoft.Extensions.Options.IOptions<ExternalAuthOptions> options =
            MsOptions.Create(new ExternalAuthOptions
            {
                Providers = [.. configuredProviders.Select(name => new ExternalAuthProvider { Type = name })],
            });
        return new OpenIddictExternalProviderRegistry(options, _schemeProvider);
    }

    private static AuthenticationScheme Scheme(string name) =>
        new(name, name, typeof(DummyHandler));

    [Fact]
    public void IsProviderConfigured_ConfiguredProvider_IsCaseInsensitive()
    {
        OpenIddictExternalProviderRegistry sut = CreateSut("Google");

        sut.IsProviderConfigured("google").ShouldBeTrue();
        sut.IsProviderConfigured("Microsoft").ShouldBeFalse();
    }

    [Fact]
    public async Task IsProviderAvailableAsync_ConfiguredAndSchemeRegistered_ReturnsTrue()
    {
        OpenIddictExternalProviderRegistry sut = CreateSut("Google");
        _schemeProvider.GetSchemeAsync("Google").Returns(Scheme("Google"));

        bool available = await sut.IsProviderAvailableAsync("Google", TestContext.Current.CancellationToken);

        available.ShouldBeTrue();
    }

    [Fact]
    public async Task IsProviderAvailableAsync_ConfiguredButNoScheme_ReturnsFalse()
    {
        OpenIddictExternalProviderRegistry sut = CreateSut("Google");
        _schemeProvider.GetSchemeAsync("Google").Returns((AuthenticationScheme?)null);

        bool available = await sut.IsProviderAvailableAsync("Google", TestContext.Current.CancellationToken);

        available.ShouldBeFalse();
    }

    [Fact]
    public async Task IsProviderAvailableAsync_NotConfigured_ReturnsFalseWithoutSchemeLookup()
    {
        OpenIddictExternalProviderRegistry sut = CreateSut("Google");

        bool available = await sut.IsProviderAvailableAsync("GitHub", TestContext.Current.CancellationToken);

        available.ShouldBeFalse();
        await _schemeProvider.DidNotReceive().GetSchemeAsync(Arg.Any<string>());
    }

    private sealed class DummyHandler : IAuthenticationHandler
    {
        public Task InitializeAsync(AuthenticationScheme scheme, Microsoft.AspNetCore.Http.HttpContext context) =>
            Task.CompletedTask;

        public Task<AuthenticateResult> AuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
