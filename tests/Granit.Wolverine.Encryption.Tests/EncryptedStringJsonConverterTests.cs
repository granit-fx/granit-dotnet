// =============================================================================
// Tests - EncryptedStringJsonConverter
// =============================================================================
// Verifies the round-trip: a string property carrying [Encrypted] is written
// as `enc:v1:<cipher>` on the wire and decoded back to plaintext on read.
// Legacy plaintext values (without prefix) are returned verbatim — backward
// compatibility for outbox/saga rows persisted before the converter shipped.
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Granit.Encryption;
using Granit.Wolverine.Encryption.Internal;
using Shouldly;
using Xunit;

namespace Granit.Wolverine.Encryption.Tests;

public sealed class EncryptedStringJsonConverterTests
{
    private readonly Base64FlipEncryption _encryption = new();
    private readonly JsonSerializerOptions _options;

    public EncryptedStringJsonConverterTests()
    {
        EncryptedPropertyJsonTypeInfoModifier modifier = new(_encryption);
        DefaultJsonTypeInfoResolver resolver = new();
        resolver.Modifiers.Add(modifier.Modify);
        _options = new JsonSerializerOptions { TypeInfoResolver = resolver };
    }

    [Fact]
    public void Marked_property_is_encrypted_on_serialize()
    {
        DeletionPayload payload = new("alice@example.com", "I want my data deleted");

        string json = JsonSerializer.Serialize(payload, _options);

        // Email is plaintext (no [Encrypted]); Reason is encrypted with v1 prefix.
        json.ShouldContain("\"alice@example.com\"");
        json.ShouldNotContain("I want my data deleted");
        json.ShouldContain("enc:v1:");
    }

    [Fact]
    public void Marked_property_round_trips_to_original_plaintext()
    {
        DeletionPayload original = new("alice@example.com", "I want my data deleted");

        string json = JsonSerializer.Serialize(original, _options);
        DeletionPayload? decoded = JsonSerializer.Deserialize<DeletionPayload>(json, _options);

        decoded.ShouldNotBeNull();
        decoded.Email.ShouldBe("alice@example.com");
        decoded.Reason.ShouldBe("I want my data deleted");
    }

    [Fact]
    public void Legacy_plaintext_without_prefix_reads_as_is()
    {
        // An outbox row written before the converter shipped contains the raw
        // PII string. The read path must not throw — return as-is so existing
        // rows remain materializable until rewritten.
        const string legacyJson = """{"Email":"bob@example.com","Reason":"plain-old-text"}""";

        DeletionPayload? decoded = JsonSerializer.Deserialize<DeletionPayload>(legacyJson, _options);

        decoded.ShouldNotBeNull();
        decoded.Reason.ShouldBe("plain-old-text");
    }

    [Fact]
    public void Null_value_serializes_as_null_and_deserializes_as_null()
    {
        DeletionPayload payload = new("nobody@example.com", null);

        string json = JsonSerializer.Serialize(payload, _options);
        DeletionPayload? decoded = JsonSerializer.Deserialize<DeletionPayload>(json, _options);

        json.ShouldContain("\"Reason\":null");
        decoded.ShouldNotBeNull();
        decoded.Reason.ShouldBeNull();
    }

    [Fact]
    public void Unmarked_string_property_is_unaffected()
    {
        // Email has no [Encrypted] — must round-trip without prefix at any point.
        DeletionPayload payload = new("clear@example.com", "secret");

        string json = JsonSerializer.Serialize(payload, _options);

        json.ShouldContain("\"Email\":\"clear@example.com\"");
        json.ShouldNotContain("enc:v1:clear");
    }

    [Fact]
    public void Different_payloads_produce_different_ciphers()
    {
        // Sanity: the converter does not collapse different inputs to the same output.
        string aJson = JsonSerializer.Serialize(new DeletionPayload("a@x", "first"), _options);
        string bJson = JsonSerializer.Serialize(new DeletionPayload("a@x", "second"), _options);

        aJson.ShouldNotBe(bJson);
    }

    // ──── Test fixtures ────

    private sealed record DeletionPayload(
        string Email,
        [property: EncryptedAttribute] string? Reason);

    /// <summary>
    /// Deterministic fake encryption: encodes plaintext as base64, decodes back.
    /// Not real crypto — just a reversible transform that lets the tests assert
    /// "the converter changed the wire bytes" without a real provider.
    /// </summary>
    private sealed class Base64FlipEncryption : IStringEncryptionService
    {
        public string Encrypt(string plainText) =>
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainText));

        public string? Decrypt(string cipherText) =>
            System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cipherText));
    }
}
