using System.Diagnostics.Metrics;
using System.Text;
using Azure;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Granit.Vault.Azure.Options;
using Granit.Vault.Azure.Services;
using Granit.Vault.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Vault.Azure.Tests;

public sealed class AzureManagedHsmMacServiceTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly VaultMetrics _metrics;

    public AzureManagedHsmMacServiceTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _metrics = new VaultMetrics(_sp.GetRequiredService<IMeterFactory>());
    }

    public void Dispose() => _sp.Dispose();

    private AzureManagedHsmMacService BuildSut(Func<Uri, CryptographyClient> factory) => new(
        Microsoft.Extensions.Options.Options.Create(new AzureManagedHsmMacOptions
        {
            HsmUri = "https://hsm.example.managedhsm.azure.net/",
            KeyName = "granit-mac",
        }),
        _metrics,
        currentTenant: null,
        NullLogger<AzureManagedHsmMacService>.Instance,
        factory);

    [Fact]
    public async Task MacAsync_ExtractsVersionFromKeyId_AndPrefixesTag()
    {
        CryptographyClient client = Substitute.For<CryptographyClient>();
        client.SignDataAsync(SignatureAlgorithm.HS256, Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.SignResult(
                keyId: "https://hsm.example.managedhsm.azure.net/keys/granit-mac/abc123",
                signature: [9, 8, 7],
                algorithm: SignatureAlgorithm.HS256));
        AzureManagedHsmMacService sut = BuildSut(_ => client);

        TransitMacResult result = await sut.MacAsync(
            "k",
            Encoding.UTF8.GetBytes("hello"),
            TestContext.Current.CancellationToken);

        result.Mac.ShouldBe("ahsm:abc123:" + Convert.ToBase64String([9, 8, 7]));
        result.KeyVersion.ShouldBe(0); // version "abc123" is non-numeric → KeyVersion 0
        sut.Dispose();
    }

    [Fact]
    public async Task VerifyAsync_RoundTripsViaHsm()
    {
        CryptographyClient client = Substitute.For<CryptographyClient>();
        client.VerifyDataAsync(
                SignatureAlgorithm.HS256,
                Arg.Any<byte[]>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.VerifyResult(
                isValid: true,
                keyId: "https://hsm.example.managedhsm.azure.net/keys/granit-mac/abc",
                algorithm: SignatureAlgorithm.HS256));
        AzureManagedHsmMacService sut = BuildSut(_ => client);

        bool ok = await sut.VerifyAsync(
            "k",
            Encoding.UTF8.GetBytes("hello"),
            "ahsm:abc:" + Convert.ToBase64String([9, 8, 7]),
            TestContext.Current.CancellationToken);

        ok.ShouldBeTrue();
        sut.Dispose();
    }

    [Fact]
    public async Task VerifyAsync_RejectsNonAhsmTag()
    {
        CryptographyClient client = Substitute.For<CryptographyClient>();
        AzureManagedHsmMacService sut = BuildSut(_ => client);

        (await sut.VerifyAsync(
                "k",
                Encoding.UTF8.GetBytes("x"),
                "vault:v1:abc",
                TestContext.Current.CancellationToken))
            .ShouldBeFalse();
        sut.Dispose();
    }

    [Fact]
    public async Task VerifyAsync_TreatsKeyNotFoundAsMiss()
    {
        CryptographyClient client = Substitute.For<CryptographyClient>();
        client.VerifyDataAsync(
                SignatureAlgorithm.HS256,
                Arg.Any<byte[]>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(status: 404, message: "Key not found"));
        AzureManagedHsmMacService sut = BuildSut(_ => client);

        (await sut.VerifyAsync(
                "k",
                Encoding.UTF8.GetBytes("hello"),
                "ahsm:abc:" + Convert.ToBase64String([9, 8, 7]),
                TestContext.Current.CancellationToken))
            .ShouldBeFalse();
        sut.Dispose();
    }
}
