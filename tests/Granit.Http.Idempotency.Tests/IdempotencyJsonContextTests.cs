using System.Text.Json;
using Granit.Http.Idempotency.Internal;
using Granit.Http.Idempotency.Models;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.Tests;

public sealed class IdempotencyJsonContextTests
{
    // =========================================================================
    // InProgress entry round-trip
    // =========================================================================

    [Fact]
    public void RoundTrip_InProgressEntry_PreservesAllProperties()
    {
        var entry = new IdempotencyEntry
        {
            State = IdempotencyState.InProgress,
            PayloadHash = "abcdef0123456789",
            CreatedAt = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero),
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(entry, IdempotencyJsonContext.Default.IdempotencyEntry);
        IdempotencyEntry? deserialized = JsonSerializer.Deserialize(json, IdempotencyJsonContext.Default.IdempotencyEntry);

        deserialized.ShouldNotBeNull();
        deserialized.State.ShouldBe(IdempotencyState.InProgress);
        deserialized.PayloadHash.ShouldBe("abcdef0123456789");
        deserialized.CreatedAt.ShouldBe(entry.CreatedAt);
        deserialized.StatusCode.ShouldBeNull();
        deserialized.ResponseHeaders.ShouldBeNull();
        deserialized.ResponseBody.ShouldBeNull();
        deserialized.CompletedAt.ShouldBeNull();
    }

    // =========================================================================
    // Completed entry round-trip
    // =========================================================================

    [Fact]
    public void RoundTrip_CompletedEntry_PreservesAllProperties()
    {
        var headers = new Dictionary<string, string[]>
        {
            ["Content-Type"] = ["application/json"],
            ["X-Custom"] = ["value1", "value2"],
        };

        byte[] body = "hello world"u8.ToArray();

        var entry = new IdempotencyEntry
        {
            State = IdempotencyState.Completed,
            PayloadHash = "0123456789abcdef",
            CreatedAt = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero),
            StatusCode = 201,
            ResponseHeaders = headers,
            ResponseBody = body,
            CompletedAt = new DateTimeOffset(2026, 3, 10, 12, 0, 1, TimeSpan.Zero),
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(entry, IdempotencyJsonContext.Default.IdempotencyEntry);
        IdempotencyEntry? deserialized = JsonSerializer.Deserialize(json, IdempotencyJsonContext.Default.IdempotencyEntry);

        deserialized.ShouldNotBeNull();
        deserialized.State.ShouldBe(IdempotencyState.Completed);
        deserialized.PayloadHash.ShouldBe("0123456789abcdef");
        deserialized.StatusCode.ShouldBe(201);
        deserialized.ResponseHeaders.ShouldNotBeNull();
        deserialized.ResponseHeaders["Content-Type"].ShouldBe(["application/json"]);
        deserialized.ResponseHeaders["X-Custom"].ShouldBe(["value1", "value2"]);
        deserialized.ResponseBody.ShouldBe(body);
        deserialized.CompletedAt.ShouldBe(entry.CompletedAt);
    }

    // =========================================================================
    // camelCase naming policy
    // =========================================================================

    [Fact]
    public void Serialize_UsesCamelCasePropertyNames()
    {
        var entry = new IdempotencyEntry
        {
            State = IdempotencyState.InProgress,
            PayloadHash = "abc",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(entry, IdempotencyJsonContext.Default.IdempotencyEntry);
        string jsonString = System.Text.Encoding.UTF8.GetString(json);

        jsonString.ShouldContain("\"state\":");
        jsonString.ShouldContain("\"payloadHash\":");
        jsonString.ShouldContain("\"createdAt\":");
    }

    // =========================================================================
    // WhenWritingNull — null properties omitted
    // =========================================================================

    [Fact]
    public void Serialize_InProgressEntry_OmitsNullProperties()
    {
        var entry = new IdempotencyEntry
        {
            State = IdempotencyState.InProgress,
            PayloadHash = "abc",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(entry, IdempotencyJsonContext.Default.IdempotencyEntry);
        string jsonString = System.Text.Encoding.UTF8.GetString(json);

        jsonString.ShouldNotContain("\"statusCode\"");
        jsonString.ShouldNotContain("\"responseHeaders\"");
        jsonString.ShouldNotContain("\"responseBody\"");
        jsonString.ShouldNotContain("\"completedAt\"");
    }

    // =========================================================================
    // Dictionary<string, string[]> context
    // =========================================================================

    [Fact]
    public void RoundTrip_DictionaryHeaders_PreservesValues()
    {
        var headers = new Dictionary<string, string[]>
        {
            ["Content-Type"] = ["text/plain"],
            ["Accept"] = ["application/json", "text/html"],
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(headers, IdempotencyJsonContext.Default.DictionaryStringStringArray);
        Dictionary<string, string[]>? deserialized = JsonSerializer.Deserialize(json, IdempotencyJsonContext.Default.DictionaryStringStringArray);

        deserialized.ShouldNotBeNull();
        deserialized["Content-Type"].ShouldBe(["text/plain"]);
        deserialized["Accept"].ShouldBe(["application/json", "text/html"]);
    }
}
