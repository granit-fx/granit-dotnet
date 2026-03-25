using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class CursorEncoderTests
{
    [Fact]
    public void Roundtrip_string_value()
    {
        string encoded = CursorEncoder.Encode("42");
        string? decoded = CursorEncoder.Decode<string>(encoded);

        decoded.ShouldBe("42");
    }

    [Fact]
    public void Roundtrip_guid_as_string()
    {
        var id = Guid.NewGuid();
        string encoded = CursorEncoder.Encode(id.ToString());
        string? decoded = CursorEncoder.Decode<string>(encoded);

        decoded.ShouldBe(id.ToString());
    }

    [Fact]
    public void Invalid_cursor_returns_default()
    {
        string? decoded = CursorEncoder.Decode<string>("not-valid-base64!!!");

        decoded.ShouldBeNull();
    }

    [Fact]
    public void Encoded_string_is_url_safe()
    {
        string encoded = CursorEncoder.Encode("some/value+with=special");

        encoded.ShouldNotContain("+");
        encoded.ShouldNotContain("/");
        encoded.ShouldNotContain("=");
    }
}
