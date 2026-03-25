using System.Text.Json;
using Granit.Http.Idempotency.Models;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.Tests;

public sealed class IdempotencyEntrySerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // =========================================================================
    // InProgress entry round-trip
    // =========================================================================

    [Fact]
    public void RoundTrip_InProgressEntry_PreservesAllProperties()
    {
        IdempotencyEntry entry = new()
        {
            State = IdempotencyState.InProgress,
            PayloadHash = "abcdef0123456789",
            CreatedAt = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero),
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(entry, JsonOptions);
        IdempotencyEntry? deserialized = JsonSerializer.Deserialize<IdempotencyEntry>(json, JsonOptions);

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
        Dictionary<string, string[]> headers = new()
        {
            ["Content-Type"] = ["application/json"],
            ["X-Custom"] = ["value1", "value2"],
        };

        byte[] body = "hello world"u8.ToArray();

        IdempotencyEntry entry = new()
        {
            State = IdempotencyState.Completed,
            PayloadHash = "0123456789abcdef",
            CreatedAt = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero),
            StatusCode = 201,
            ResponseHeaders = headers,
            ResponseBody = body,
            CompletedAt = new DateTimeOffset(2026, 3, 10, 12, 0, 1, TimeSpan.Zero),
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(entry, JsonOptions);
        IdempotencyEntry? deserialized = JsonSerializer.Deserialize<IdempotencyEntry>(json, JsonOptions);

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
        IdempotencyEntry entry = new()
        {
            State = IdempotencyState.InProgress,
            PayloadHash = "abc",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(entry, JsonOptions);
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
        IdempotencyEntry entry = new()
        {
            State = IdempotencyState.InProgress,
            PayloadHash = "abc",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(entry, JsonOptions);
        string jsonString = System.Text.Encoding.UTF8.GetString(json);

        jsonString.ShouldNotContain("\"statusCode\"");
        jsonString.ShouldNotContain("\"responseHeaders\"");
        jsonString.ShouldNotContain("\"responseBody\"");
        jsonString.ShouldNotContain("\"completedAt\"");
    }

    // =========================================================================
    // Dictionary<string, string[]> round-trip
    // =========================================================================

    [Fact]
    public void RoundTrip_DictionaryHeaders_PreservesValues()
    {
        Dictionary<string, string[]> headers = new()
        {
            ["Content-Type"] = ["text/plain"],
            ["Accept"] = ["application/json", "text/html"],
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(headers, JsonOptions);
        Dictionary<string, string[]>? deserialized = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized["Content-Type"].ShouldBe(["text/plain"]);
        deserialized["Accept"].ShouldBe(["application/json", "text/html"]);
    }
}
