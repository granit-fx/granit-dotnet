using System.Diagnostics.Metrics;
using System.Text;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Granit.Vault.Aws.Options;
using Granit.Vault.Aws.Services;
using Granit.Vault.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Vault.Aws.Tests;

public sealed class KmsTransitEncryptionServiceAdditionalTests : IDisposable
{
    private readonly IAmazonKeyManagementService _kmsClient = Substitute.For<IAmazonKeyManagementService>();
    private readonly ServiceProvider _sp;
    private readonly KmsTransitEncryptionService _sut;

    public KmsTransitEncryptionServiceAdditionalTests()
    {
        AwsVaultOptions options = new()
        {
            Region = "eu-west-1",
            KmsKeyId = "alias/test-key",
        };

        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        VaultMetrics metrics = new(_sp.GetRequiredService<IMeterFactory>());

        _sut = new KmsTransitEncryptionService(
            _kmsClient,
            Microsoft.Extensions.Options.Options.Create(options),
            metrics,
            null,
            NullLogger<KmsTransitEncryptionService>.Instance);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public async Task RewrapAsync_DecryptsThenReEncrypts()
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("secret-data");
        byte[] oldCiphertext = [1, 2, 3];
        byte[] newCiphertext = [4, 5, 6];

        _kmsClient.DecryptAsync(Arg.Any<DecryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DecryptResponse { Plaintext = new MemoryStream(plaintextBytes) });

        _kmsClient.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse { CiphertextBlob = new MemoryStream(newCiphertext) });

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
        _kmsClient.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonKeyManagementServiceException("KMS error"));

        await Should.ThrowAsync<AmazonKeyManagementServiceException>(
            () => _sut.EncryptAsync("my-key", "data", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DecryptAsync_WhenClientThrows_RecordsErrorAndRethrows()
    {
        _kmsClient.DecryptAsync(Arg.Any<DecryptRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonKeyManagementServiceException("KMS error"));

        await Should.ThrowAsync<AmazonKeyManagementServiceException>(
            () => _sut.DecryptAsync("my-key", Convert.ToBase64String([1, 2]), TestContext.Current.CancellationToken));
    }
}
