using System.Diagnostics.Metrics;
using System.Text;
using Granit.Vault.Diagnostics;
using Granit.Vault.HashiCorp.Options;
using Granit.Vault.HashiCorp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using VaultSharp;
using VaultSharp.V1;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;
using VaultSharp.V1.SecretsEngines.Transit;
using Xunit;

namespace Granit.Vault.HashiCorp.Tests;

public sealed class HashiCorpTransitMacServiceTests : IDisposable
{
    private readonly IVaultClient _vaultClient = Substitute.For<IVaultClient>();
    private readonly ITransitSecretsEngine _transit = Substitute.For<ITransitSecretsEngine>();
    private readonly HashiCorpTransitMacService _sut;
    private readonly ServiceProvider _sp;

    public HashiCorpTransitMacServiceTests()
    {
        ISecretsEngine secrets = Substitute.For<ISecretsEngine>();
        secrets.Transit.Returns(_transit);
        _vaultClient.V1.Returns(Substitute.For<IVaultClientV1>());
        _vaultClient.V1.Secrets.Returns(secrets);

        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        VaultMetrics metrics = new(_sp.GetRequiredService<IMeterFactory>());

        _sut = new HashiCorpTransitMacService(
            _vaultClient,
            Microsoft.Extensions.Options.Options.Create(new HashiCorpVaultOptions { TransitMountPoint = "transit" }),
            metrics,
            currentTenant: null,
            NullLogger<HashiCorpTransitMacService>.Instance);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public async Task MacAsync_DelegatesToTransitHmac_AndParsesKeyVersion()
    {
        byte[] input = Encoding.UTF8.GetBytes("payload");
        _transit.GenerateHmacAsync("k", Arg.Any<HmacRequestOptions>(), "transit", null)
            .Returns(new Secret<HmacResponse> { Data = new HmacResponse { Hmac = "vault:v7:abcdef" } });

        TransitMacResult result = await _sut.MacAsync("k", input, TestContext.Current.CancellationToken);

        result.Mac.ShouldBe("vault:v7:abcdef");
        result.KeyVersion.ShouldBe(7);
    }

    [Fact]
    public async Task VerifyAsync_ShortCircuitsForNonVaultTag()
    {
        bool ok = await _sut.VerifyAsync(
            "k",
            Encoding.UTF8.GetBytes("x"),
            "sbm:v1:not-a-vault-tag",
            TestContext.Current.CancellationToken);

        ok.ShouldBeFalse();
        await _transit.DidNotReceive().VerifySignedDataAsync(
            Arg.Any<string>(), Arg.Any<VerifyRequestOptions>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task VerifyAsync_ReturnsTrue_WhenVaultEchoesValid()
    {
        _transit.VerifySignedDataAsync("k", Arg.Any<VerifyRequestOptions>(), "transit", null)
            .Returns(new Secret<VerifyResponse>
            {
                Data = new VerifyResponse
                {
                    BatchResults = [new VerifySingleResponse { Valid = true }]
                }
            });

        (await _sut.VerifyAsync(
                "k",
                Encoding.UTF8.GetBytes("payload"),
                "vault:v1:abc",
                TestContext.Current.CancellationToken))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyAsync_ReturnsFalse_WhenVaultEchoesInvalid()
    {
        _transit.VerifySignedDataAsync("k", Arg.Any<VerifyRequestOptions>(), "transit", null)
            .Returns(new Secret<VerifyResponse>
            {
                Data = new VerifyResponse
                {
                    BatchResults = [new VerifySingleResponse { Valid = false }]
                }
            });

        (await _sut.VerifyAsync(
                "k",
                Encoding.UTF8.GetBytes("payload"),
                "vault:v1:abc",
                TestContext.Current.CancellationToken))
            .ShouldBeFalse();
    }
}
