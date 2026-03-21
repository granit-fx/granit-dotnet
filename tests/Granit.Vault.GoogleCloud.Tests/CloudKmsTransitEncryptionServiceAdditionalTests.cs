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
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Vault.GoogleCloud.Tests;

public sealed class CloudKmsTransitEncryptionServiceAdditionalTests : IDisposable
{
    private readonly KeyManagementServiceClient _kmsClient = Substitute.For<KeyManagementServiceClient>();
    private readonly ServiceProvider _sp;
    private readonly CloudKmsTransitEncryptionService _sut;

    public CloudKmsTransitEncryptionServiceAdditionalTests()
    {
        GoogleCloudVaultOptions options = new()
        {
            ProjectId = "my-project",
            Location = "europe-west1",
            KeyRing = "granit-keyring",
            CryptoKey = "granit-key",
        };

        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        VaultMetrics metrics = new(_sp.GetRequiredService<IMeterFactory>());

        _sut = new CloudKmsTransitEncryptionService(
            _kmsClient,
            Microsoft.Extensions.Options.Options.Create(options),
            metrics,
            null,
            NullLogger<CloudKmsTransitEncryptionService>.Instance);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public async Task RewrapAsync_DecryptsThenReEncrypts()
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("secret-data");
        byte[] oldCiphertext = [1, 2, 3];
        byte[] newCiphertext = [4, 5, 6];

        _kmsClient.DecryptAsync(
                Arg.Any<CryptoKeyName>(),
                Arg.Any<ByteString>(),
                Arg.Any<CancellationToken>())
            .Returns(new DecryptResponse { Plaintext = ByteString.CopyFrom(plaintextBytes) });

        _kmsClient.EncryptAsync(
                Arg.Any<CryptoKeyName>(),
                Arg.Any<ByteString>(),
                Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse { Ciphertext = ByteString.CopyFrom(newCiphertext) });

        string result = await _sut.RewrapAsync(
            "my-key",
            Convert.ToBase64String(oldCiphertext),
            TestContext.Current.CancellationToken);

        result.ShouldBe(Convert.ToBase64String(newCiphertext));
    }

    [Fact]
    public void GetKeyVersion_AlwaysReturnsNull()
    {
        _sut.GetKeyVersion("any-ciphertext").ShouldBeNull();
        _sut.GetKeyVersion("vault:v1:abc").ShouldBeNull();
        _sut.GetKeyVersion("").ShouldBeNull();
    }

    [Fact]
    public async Task EncryptAsync_WhenClientThrows_RecordsErrorAndRethrows()
    {
        _kmsClient.EncryptAsync(
                Arg.Any<CryptoKeyName>(),
                Arg.Any<ByteString>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "unavailable")));

        await Should.ThrowAsync<Grpc.Core.RpcException>(
            () => _sut.EncryptAsync("my-key", "data", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DecryptAsync_WhenClientThrows_RecordsErrorAndRethrows()
    {
        _kmsClient.DecryptAsync(
                Arg.Any<CryptoKeyName>(),
                Arg.Any<ByteString>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "unavailable")));

        await Should.ThrowAsync<Grpc.Core.RpcException>(
            () => _sut.DecryptAsync("my-key", Convert.ToBase64String([1, 2]), TestContext.Current.CancellationToken));
    }
}
