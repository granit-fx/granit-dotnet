using System.Diagnostics.Metrics;
using System.Text;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Granit.Vault.Diagnostics;
using Granit.Vault.GoogleCloud.Options;
using Granit.Vault.GoogleCloud.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Vault.GoogleCloud.Tests;

public sealed class GoogleCloudKmsMacServiceTests : IDisposable
{
    private readonly KeyManagementServiceClient _kms = Substitute.For<KeyManagementServiceClient>();
    private readonly ServiceProvider _sp;
    private readonly VaultMetrics _metrics;

    public GoogleCloudKmsMacServiceTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _metrics = new VaultMetrics(_sp.GetRequiredService<IMeterFactory>());
    }

    public void Dispose() => _sp.Dispose();

    private GoogleCloudKmsMacService BuildSut() => new(
        _kms,
        Microsoft.Extensions.Options.Options.Create(new GoogleCloudVaultOptions
        {
            ProjectId = "p",
            Location = "europe-west1",
            KeyRing = "kr",
            CryptoKey = "ck-encrypt",
        }),
        Microsoft.Extensions.Options.Options.Create(new GoogleCloudKmsMacOptions { CryptoKeyId = "ck-mac" }),
        _metrics,
        currentTenant: null,
        NullLogger<GoogleCloudKmsMacService>.Instance);

    [Fact]
    public async Task MacAsync_DelegatesToCloudKms_AndExtractsVersion()
    {
        byte[] expectedMac = [1, 2, 3, 4];
        _kms.MacSignAsync(Arg.Any<MacSignRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MacSignResponse
            {
                Name = "projects/p/locations/europe-west1/keyRings/kr/cryptoKeys/ck-mac/cryptoKeyVersions/3",
                Mac = ByteString.CopyFrom(expectedMac),
            });
        GoogleCloudKmsMacService sut = BuildSut();

        TransitMacResult result = await sut.MacAsync(
            "k",
            Encoding.UTF8.GetBytes("hello"),
            TestContext.Current.CancellationToken);

        result.KeyVersion.ShouldBe(3);
        result.Mac.ShouldStartWith("gcpkms:v3:");
    }

    [Fact]
    public async Task VerifyAsync_RefusesDisabledVersion()
    {
        _kms.GetCryptoKeyVersionAsync(Arg.Any<CryptoKeyVersionName>(), Arg.Any<CancellationToken>())
            .Returns(new CryptoKeyVersion
            {
                CryptoKeyVersionName = new CryptoKeyVersionName("p", "europe-west1", "kr", "ck-mac", "3"),
                State = CryptoKeyVersion.Types.CryptoKeyVersionState.Disabled,
            });
        GoogleCloudKmsMacService sut = BuildSut();

        bool ok = await sut.VerifyAsync(
            "k",
            Encoding.UTF8.GetBytes("hello"),
            "gcpkms:v3:" + Convert.ToBase64String([1, 2, 3]),
            TestContext.Current.CancellationToken);

        ok.ShouldBeFalse();
        await _kms.DidNotReceive().MacVerifyAsync(Arg.Any<MacVerifyRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyAsync_ReturnsTrue_ForEnabledVersionAndValidMac()
    {
        _kms.GetCryptoKeyVersionAsync(Arg.Any<CryptoKeyVersionName>(), Arg.Any<CancellationToken>())
            .Returns(new CryptoKeyVersion
            {
                CryptoKeyVersionName = new CryptoKeyVersionName("p", "europe-west1", "kr", "ck-mac", "3"),
                State = CryptoKeyVersion.Types.CryptoKeyVersionState.Enabled,
            });
        _kms.MacVerifyAsync(Arg.Any<MacVerifyRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MacVerifyResponse { Success = true });
        GoogleCloudKmsMacService sut = BuildSut();

        bool ok = await sut.VerifyAsync(
            "k",
            Encoding.UTF8.GetBytes("hello"),
            "gcpkms:v3:" + Convert.ToBase64String([1, 2, 3]),
            TestContext.Current.CancellationToken);

        ok.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyAsync_RejectsNonGcpKmsTag()
    {
        GoogleCloudKmsMacService sut = BuildSut();

        bool ok = await sut.VerifyAsync(
            "k",
            Encoding.UTF8.GetBytes("x"),
            "vault:v1:foo",
            TestContext.Current.CancellationToken);

        ok.ShouldBeFalse();
    }
}
