using Granit.Caching;
using Granit.Caching.Internal;
using Granit.Caching.Options;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Granit.Caching.FusionCache.Tests;

public sealed class EncryptingFusionCacheSerializerTests
{
    private readonly FusionCacheSystemTextJsonSerializer _innerSerializer = new();
    private readonly ICacheValueEncryptor _encryptor = new AesCacheValueEncryptor(
        Microsoft.Extensions.Options.Options.Create(new CacheEncryptionOptions
        {
            Key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
        }));

    private static CachingOptions EncryptionEnabled() => new() { EncryptValues = true };
    private static CachingOptions EncryptionDisabled() => new();

    [Fact]
    public void Serialize_WithEncryptionEnabled_EncryptsData()
    {
        var sut = new EncryptingFusionCacheSerializer(_innerSerializer, _encryptor, EncryptionEnabled());
        byte[] plain = _innerSerializer.Serialize("hello");

        byte[] encrypted = sut.Serialize("hello");

        encrypted.ShouldNotBe(plain);
        encrypted.Length.ShouldBeGreaterThan(plain.Length); // nonce + tag overhead
    }

    [Fact]
    public void Deserialize_WithEncryptionEnabled_DecryptsData()
    {
        var sut = new EncryptingFusionCacheSerializer(_innerSerializer, _encryptor, EncryptionEnabled());

        byte[] encrypted = sut.Serialize("hello");
        string? result = sut.Deserialize<string>(encrypted);

        result.ShouldBe("hello");
    }

    [Fact]
    public void Serialize_WithEncryptionDisabled_PassesThrough()
    {
        var sut = new EncryptingFusionCacheSerializer(_innerSerializer, _encryptor, EncryptionDisabled());
        byte[] plain = _innerSerializer.Serialize("hello");

        byte[] result = sut.Serialize("hello");

        result.ShouldBe(plain);
    }

    [Fact]
    public void RoundTrip_ComplexObject_PreservesData()
    {
        var sut = new EncryptingFusionCacheSerializer(_innerSerializer, _encryptor, EncryptionEnabled());
        var original = new TestCacheItem { Name = "test", Value = 42 };

        byte[] encrypted = sut.Serialize(original);
        TestCacheItem? deserialized = sut.Deserialize<TestCacheItem>(encrypted);

        deserialized.ShouldNotBeNull();
        deserialized.Name.ShouldBe("test");
        deserialized.Value.ShouldBe(42);
    }

    [Fact]
    public async Task SerializeAsync_WithEncryption_EncryptsData()
    {
        var sut = new EncryptingFusionCacheSerializer(_innerSerializer, _encryptor, EncryptionEnabled());

        byte[] encrypted = await sut.SerializeAsync("async-test", TestContext.Current.CancellationToken);
        string? result = await sut.DeserializeAsync<string>(encrypted, TestContext.Current.CancellationToken);

        result.ShouldBe("async-test");
    }

    [Fact]
    public void Serialize_PerTypeAttribute_OverridesGlobalFlag()
    {
        // Arrange — global encryption disabled, but [CacheEncrypted] on the type
        var sut = new EncryptingFusionCacheSerializer(_innerSerializer, _encryptor, EncryptionDisabled());
        byte[] plain = _innerSerializer.Serialize(new AlwaysEncryptedItem { Data = "sensitive" });

        // Act
        byte[] result = sut.Serialize(new AlwaysEncryptedItem { Data = "sensitive" });

        // Assert — [CacheEncrypted] should force encryption even with EncryptValues=false
        result.ShouldNotBe(plain);
        result.Length.ShouldBeGreaterThan(plain.Length);
    }

    private sealed class TestCacheItem
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    [CacheEncrypted]
    private sealed class AlwaysEncryptedItem
    {
        public string Data { get; set; } = string.Empty;
    }
}
