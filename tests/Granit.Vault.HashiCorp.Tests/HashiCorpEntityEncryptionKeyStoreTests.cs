using System.Net;
using Granit.Vault.HashiCorp.Options;
using Granit.Vault.HashiCorp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using VaultSharp;
using VaultSharp.Core;
using VaultSharp.V1;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;
using VaultSharp.V1.SecretsEngines.KeyValue;
using VaultSharp.V1.SecretsEngines.KeyValue.V2;
using Xunit;

namespace Granit.Vault.HashiCorp.Tests;

public sealed class HashiCorpEntityEncryptionKeyStoreTests
{
    private readonly IVaultClient _vaultClient;
    private readonly IKeyValueSecretsEngineV2 _kvV2;
    private readonly HashiCorpEntityEncryptionKeyStore _sut;

    public HashiCorpEntityEncryptionKeyStoreTests()
    {
        _vaultClient = Substitute.For<IVaultClient>();
        _kvV2 = Substitute.For<IKeyValueSecretsEngineV2>();

        IVaultClientV1 v1 = Substitute.For<IVaultClientV1>();
        ISecretsEngine secrets = Substitute.For<ISecretsEngine>();
        IKeyValueSecretsEngine kv = Substitute.For<IKeyValueSecretsEngine>();

        _vaultClient.V1.Returns(v1);
        v1.Secrets.Returns(secrets);
        secrets.KeyValue.Returns(kv);
        kv.V2.Returns(_kvV2);

        IOptions<HashiCorpVaultOptions> options = Microsoft.Extensions.Options.Options.Create(new HashiCorpVaultOptions
        {
            KvMountPoint = "secret"
        });

        _sut = new HashiCorpEntityEncryptionKeyStore(
            _vaultClient,
            options,
            NullLogger<HashiCorpEntityEncryptionKeyStore>.Instance);
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenKeyDoesNotExist_CreatesNewKey()
    {
        _kvV2.ReadSecretAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new VaultApiException(HttpStatusCode.NotFound, "not found"));

        byte[] key = await _sut.GetOrCreateKeyAsync("Patient", "abc-123", TestContext.Current.CancellationToken);

        key.ShouldNotBeNull();
        key.Length.ShouldBe(32, "Should generate a 32-byte AES-256 key");

        await _kvV2.Received(1).WriteSecretAsync(
            "granit/encryption/isolated/Patient/abc-123",
            Arg.Any<IDictionary<string, object>>(),
            Arg.Any<int?>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenKeyExists_ReturnsExistingKey()
    {
        byte[] existingKey = new byte[32];
        Random.Shared.NextBytes(existingKey);
        string base64Key = Convert.ToBase64String(existingKey);

        Secret<SecretData> secret = CreateSecretData(new Dictionary<string, object>
        {
            ["key"] = base64Key
        });

        _kvV2.ReadSecretAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .Returns(Task.FromResult(secret));

        byte[] key = await _sut.GetOrCreateKeyAsync("Patient", "abc-123", TestContext.Current.CancellationToken);

        key.ShouldBe(existingKey);

        await _kvV2.DidNotReceive().WriteSecretAsync(
            Arg.Any<string>(), Arg.Any<IDictionary<string, object>>(),
            Arg.Any<int?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task GetKeyAsync_WhenKeyDoesNotExist_ReturnsNull()
    {
        _kvV2.ReadSecretAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new VaultApiException(HttpStatusCode.NotFound, "not found"));

        byte[]? key = await _sut.GetKeyAsync("Patient", "abc-123", TestContext.Current.CancellationToken);

        key.ShouldBeNull();
    }

    [Fact]
    public async Task GetKeyAsync_WhenKeyExists_ReturnsCachedOnSecondCall()
    {
        byte[] existingKey = new byte[32];
        Random.Shared.NextBytes(existingKey);

        Secret<SecretData> secret = CreateSecretData(new Dictionary<string, object>
        {
            ["key"] = Convert.ToBase64String(existingKey)
        });

        _kvV2.ReadSecretAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .Returns(Task.FromResult(secret));

        // First call — reads from Vault
        byte[]? key1 = await _sut.GetKeyAsync("Patient", "abc-123", TestContext.Current.CancellationToken);
        // Second call — should be cached
        byte[]? key2 = await _sut.GetKeyAsync("Patient", "abc-123", TestContext.Current.CancellationToken);

        key1.ShouldBe(existingKey);
        key2.ShouldBe(existingKey);

        // Only one Vault read
        await _kvV2.Received(1).ReadSecretAsync(
            Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task DeleteKeyAsync_CallsDeleteMetadataAsync()
    {
        await _sut.DeleteKeyAsync("Patient", "abc-123", TestContext.Current.CancellationToken);

        await _kvV2.Received(1).DeleteMetadataAsync(
            "granit/encryption/isolated/Patient/abc-123",
            Arg.Any<string>());
    }

    [Fact]
    public async Task DeleteKeyAsync_EvictsCacheEntry()
    {
        byte[] existingKey = new byte[32];
        Random.Shared.NextBytes(existingKey);

        Secret<SecretData> secret = CreateSecretData(new Dictionary<string, object>
        {
            ["key"] = Convert.ToBase64String(existingKey)
        });

        _kvV2.ReadSecretAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .Returns(Task.FromResult(secret));

        // Populate cache
        await _sut.GetKeyAsync("Patient", "abc-123", TestContext.Current.CancellationToken);

        // Delete — should evict cache
        await _sut.DeleteKeyAsync("Patient", "abc-123", TestContext.Current.CancellationToken);

        // After delete, reading from Vault again (returns 404)
        _kvV2.ReadSecretAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new VaultApiException(HttpStatusCode.NotFound, "not found"));

        byte[]? key = await _sut.GetKeyAsync("Patient", "abc-123", TestContext.Current.CancellationToken);
        key.ShouldBeNull();
    }

    [Fact]
    public async Task KeyExistsAsync_ReturnsTrue_WhenKeyExists()
    {
        Secret<SecretData> secret = CreateSecretData(new Dictionary<string, object>
        {
            ["key"] = Convert.ToBase64String(new byte[32])
        });

        _kvV2.ReadSecretAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .Returns(Task.FromResult(secret));

        bool exists = await _sut.KeyExistsAsync("Patient", "abc-123", TestContext.Current.CancellationToken);

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task KeyExistsAsync_ReturnsFalse_WhenKeyDoesNotExist()
    {
        _kvV2.ReadSecretAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new VaultApiException(HttpStatusCode.NotFound, "not found"));

        bool exists = await _sut.KeyExistsAsync("Patient", "abc-123", TestContext.Current.CancellationToken);

        exists.ShouldBeFalse();
    }

    private static Secret<SecretData> CreateSecretData(Dictionary<string, object> data) =>
        new()
        {
            Data = new SecretData
            {
                Data = data
            }
        };
}
