// =============================================================================
// Tests - IdempotencyEntry encryption
// =============================================================================
// IdempotencyEntry is [CacheEncrypted], so a captured response (status, headers,
// body — potentially PII/tokens) is AES-256-GCM encrypted on the L2 (Redis)
// serialization path even when the global CachingOptions.EncryptValues flag is off,
// and is left as a plain object on the L1 (in-memory) store — same threat model as
// IMemoryCache.
// =============================================================================

using System.Text;
using Granit.Caching;
using Granit.Caching.Internal;
using Granit.Caching.Options;
using Granit.Http.Idempotency.Models;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Granit.Http.Idempotency.Tests;

public sealed class IdempotencyEntryEncryptionTests
{
    private static IdempotencyEntry SampleCompletedEntry() => new()
    {
        State = IdempotencyState.Completed,
        PayloadHash = "hash-value",
        CreatedAt = new DateTimeOffset(2026, 3, 21, 12, 0, 0, TimeSpan.Zero),
        StatusCode = 200,
        ResponseHeaders = new Dictionary<string, string[]> { ["Content-Type"] = ["application/json"] },
        // A body that would leak PII if stored in plaintext.
        ResponseBody = Encoding.UTF8.GetBytes("""{"ssn":"123-45-6789","email":"jane@example.com"}"""),
        CompletedAt = new DateTimeOffset(2026, 3, 21, 12, 0, 1, TimeSpan.Zero),
    };

    private static AesCacheValueEncryptor NewAesEncryptor()
    {
        byte[] keyBytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(keyBytes);
        return new AesCacheValueEncryptor(Options.Create(new CacheEncryptionOptions
        {
            Key = Convert.ToBase64String(keyBytes),
        }));
    }

    [Fact]
    public void CacheEncryptionResolver_IdempotencyEntry_EncryptsEvenWhenGlobalFlagOff()
    {
        // The [CacheEncrypted] attribute must win over EncryptValues = false.
        CacheEncryptionResolver.ShouldEncrypt(typeof(IdempotencyEntry), new CachingOptions { EncryptValues = false })
            .ShouldBeTrue();
    }

    [Fact]
    public void L2SerializerPath_IdempotencyEntry_IsCiphertext_AndRoundTrips()
    {
        // Arrange — the L2 (Redis) serializer decorator with the global flag OFF.
        AesCacheValueEncryptor encryptor = NewAesEncryptor();
        var options = new CachingOptions { EncryptValues = false };
        var serializer = new EncryptingFusionCacheSerializer(
            new FusionCacheSystemTextJsonSerializer(),
            encryptor,
            options);

        IdempotencyEntry entry = SampleCompletedEntry();

        // Plaintext JSON of the same entry (encryption disabled) — the leak baseline.
        var plainSerializer = new EncryptingFusionCacheSerializer(
            new FusionCacheSystemTextJsonSerializer(),
            new NullCacheValueEncryptor(),
            options);
        byte[] plaintextBytes = plainSerializer.Serialize(entry);

        // Act
        byte[] wireBytes = serializer.Serialize(entry);

        // Assert — the encrypted wire bytes must not equal the plaintext, and the sensitive body
        // must not be discoverable in the ciphertext.
        wireBytes.ShouldNotBe(plaintextBytes);
        Encoding.UTF8.GetString(wireBytes).ShouldNotContain("123-45-6789");
        Encoding.UTF8.GetString(wireBytes).ShouldNotContain("jane@example.com");

        // And it must decrypt back to the original entry.
        IdempotencyEntry? roundTripped = serializer.Deserialize<IdempotencyEntry>(wireBytes);
        roundTripped.ShouldNotBeNull();
        roundTripped.ResponseBody.ShouldBe(entry.ResponseBody);
        roundTripped.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task L1InMemoryStore_IdempotencyEntry_IsNotSerialized()
    {
        // The L1 in-memory conditional cache holds live object graphs; [CacheEncrypted] is a no-op
        // there. Proof: the exact same instance comes back out (no serialize/encrypt round trip).
        var l1 = new InMemoryConditionalCache(TimeProvider.System);
        IdempotencyEntry entry = SampleCompletedEntry();

        bool added = await l1.SetIfAbsentAsync("k", entry, TimeSpan.FromMinutes(5), CancellationToken.None);
        added.ShouldBeTrue();

        IdempotencyEntry? fetched = await l1.GetAsync<IdempotencyEntry>("k", CancellationToken.None);
        fetched.ShouldBeSameAs(entry);
    }
}
