using Granit.Querying.EntityFrameworkCore.Internal;
using Shouldly;
using Xunit;

namespace Granit.Querying.EntityFrameworkCore.Tests.Internal;

public sealed class CursorEncoderAdditionalTests
{
    // ── EncodeComposite / DecodeComposite ─────────────────────────────

    [Fact]
    public void EncodeComposite_DecodeComposite_roundtrips()
    {
        Dictionary<string, string> values = new()
        {
            ["Price"] = "500",
            ["Name"] = "Laptop",
        };

        string encoded = CursorEncoder.EncodeComposite(values);
        Dictionary<string, string>? decoded = CursorEncoder.DecodeComposite(encoded);

        decoded.ShouldNotBeNull();
        decoded["Price"].ShouldBe("500");
        decoded["Name"].ShouldBe("Laptop");
    }

    [Fact]
    public void DecodeComposite_returns_null_for_legacy_string_cursor()
    {
        // Legacy cursors are JSON primitives (e.g., "\"42\"")
        string encoded = CursorEncoder.Encode("42");

        Dictionary<string, string>? decoded = CursorEncoder.DecodeComposite(encoded);

        decoded.ShouldBeNull();
    }

    [Fact]
    public void DecodeComposite_returns_null_for_invalid_base64()
    {
        Dictionary<string, string>? decoded = CursorEncoder.DecodeComposite("!!!invalid!!!");

        decoded.ShouldBeNull();
    }

    [Fact]
    public void Decode_int_value()
    {
        string encoded = CursorEncoder.Encode(42);
        int? decoded = CursorEncoder.Decode<int>(encoded);

        decoded.ShouldBe(42);
    }

    [Fact]
    public void Decode_invalid_base64_returns_default()
    {
        int decoded = CursorEncoder.Decode<int>("not-valid!!!");

        decoded.ShouldBe(0);
    }

    [Fact]
    public void EncodeComposite_produces_url_safe_string()
    {
        Dictionary<string, string> values = new()
        {
            ["Field"] = "value/with+special=chars",
        };

        string encoded = CursorEncoder.EncodeComposite(values);

        encoded.ShouldNotContain("+");
        encoded.ShouldNotContain("/");
        encoded.ShouldNotContain("=");
    }

    [Fact]
    public void Encode_empty_string_roundtrips()
    {
        string encoded = CursorEncoder.Encode("");
        string? decoded = CursorEncoder.Decode<string>(encoded);

        decoded.ShouldBe("");
    }
}
