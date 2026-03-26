using Granit.Caching.Internal;
using Granit.Caching.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Granit.Caching.FusionCache.Tests;

public sealed class EncryptingFusionCacheSerializerAdditionalTests
{
    private readonly FusionCacheSystemTextJsonSerializer _innerSerializer = new();

    private static CachingOptions EncryptionEnabled() => new() { EncryptValues = true };
    private static CachingOptions EncryptionDisabled() => new();

    [Fact]
    public void Deserialize_WithEncryptionDisabled_PassesThroughWithoutDecryption()
    {
        ICacheValueEncryptor encryptor = Substitute.For<ICacheValueEncryptor>();
        EncryptingFusionCacheSerializer sut = new(_innerSerializer, encryptor, EncryptionDisabled());

        byte[] serialized = _innerSerializer.Serialize("hello");
        string? result = sut.Deserialize<string>(serialized);

        result.ShouldBe("hello");
        encryptor.DidNotReceive().Decrypt(Arg.Any<byte[]>());
    }

    [Fact]
    public async Task SerializeAsync_WithEncryptionDisabled_PassesThroughWithoutEncryption()
    {
        ICacheValueEncryptor encryptor = Substitute.For<ICacheValueEncryptor>();
        EncryptingFusionCacheSerializer sut = new(_innerSerializer, encryptor, EncryptionDisabled());

        byte[] expected = _innerSerializer.Serialize("async-passthrough");
        byte[] result = await sut.SerializeAsync("async-passthrough", TestContext.Current.CancellationToken);

        result.ShouldBe(expected);
        encryptor.DidNotReceive().Encrypt(Arg.Any<byte[]>());
    }

    [Fact]
    public async Task DeserializeAsync_WithEncryptionDisabled_PassesThroughWithoutDecryption()
    {
        ICacheValueEncryptor encryptor = Substitute.For<ICacheValueEncryptor>();
        EncryptingFusionCacheSerializer sut = new(_innerSerializer, encryptor, EncryptionDisabled());

        byte[] serialized = _innerSerializer.Serialize(42);
        int? result = await sut.DeserializeAsync<int>(serialized, TestContext.Current.CancellationToken);

        result.ShouldBe(42);
        encryptor.DidNotReceive().Decrypt(Arg.Any<byte[]>());
    }

    [Fact]
    public async Task DeserializeAsync_WithEncryptionEnabled_CallsDecrypt()
    {
        ICacheValueEncryptor encryptor = new AesCacheValueEncryptor(
            Microsoft.Extensions.Options.Options.Create(new CacheEncryptionOptions
            {
                Key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            }));
        EncryptingFusionCacheSerializer sut = new(_innerSerializer, encryptor, EncryptionEnabled());

        byte[] encrypted = await sut.SerializeAsync("decrypt-test", TestContext.Current.CancellationToken);
        string? result = await sut.DeserializeAsync<string>(encrypted, TestContext.Current.CancellationToken);

        result.ShouldBe("decrypt-test");
    }

    [Fact]
    public void Serialize_WithEncryptionEnabled_CallsEncryptorEncrypt()
    {
        byte[] plainBytes = [1, 2, 3];
        byte[] encryptedBytes = [10, 20, 30, 40];

        IFusionCacheSerializer inner = Substitute.For<IFusionCacheSerializer>();
        inner.Serialize("test").Returns(plainBytes);

        ICacheValueEncryptor encryptor = Substitute.For<ICacheValueEncryptor>();
        encryptor.Encrypt(plainBytes).Returns(encryptedBytes);

        EncryptingFusionCacheSerializer sut = new(inner, encryptor, EncryptionEnabled());

        byte[] result = sut.Serialize("test");

        result.ShouldBe(encryptedBytes);
        encryptor.Received(1).Encrypt(plainBytes);
    }

    [Fact]
    public void Deserialize_WithEncryptionEnabled_CallsEncryptorDecrypt()
    {
        byte[] encryptedBytes = [10, 20, 30, 40];
        byte[] plainBytes = [1, 2, 3];

        IFusionCacheSerializer inner = Substitute.For<IFusionCacheSerializer>();
        inner.Deserialize<string>(plainBytes).Returns("result");

        ICacheValueEncryptor encryptor = Substitute.For<ICacheValueEncryptor>();
        encryptor.Decrypt(encryptedBytes).Returns(plainBytes);

        EncryptingFusionCacheSerializer sut = new(inner, encryptor, EncryptionEnabled());

        string? result = sut.Deserialize<string>(encryptedBytes);

        result.ShouldBe("result");
        encryptor.Received(1).Decrypt(encryptedBytes);
    }

    [Fact]
    public void RoundTrip_NullValue_PreservesNull()
    {
        ICacheValueEncryptor encryptor = new NullCacheValueEncryptor();
        EncryptingFusionCacheSerializer sut = new(_innerSerializer, encryptor, EncryptionDisabled());

        byte[] serialized = sut.Serialize<string?>(null);
        string? result = sut.Deserialize<string?>(serialized);

        result.ShouldBeNull();
    }
}
