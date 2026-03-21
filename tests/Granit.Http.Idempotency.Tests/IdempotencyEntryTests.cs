using Granit.Http.Idempotency.Models;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.Tests;

public sealed class IdempotencyEntryTests
{
    // =========================================================================
    // Required properties
    // =========================================================================

    [Fact]
    public void InProgressEntry_HasRequiredProperties()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        IdempotencyEntry entry = new()
        {
            State = IdempotencyState.InProgress,
            PayloadHash = "abc123",
            CreatedAt = now,
        };

        entry.State.ShouldBe(IdempotencyState.InProgress);
        entry.PayloadHash.ShouldBe("abc123");
        entry.CreatedAt.ShouldBe(now);
    }

    // =========================================================================
    // Optional properties default to null
    // =========================================================================

    [Fact]
    public void InProgressEntry_OptionalProperties_AreNull()
    {
        IdempotencyEntry entry = new()
        {
            State = IdempotencyState.InProgress,
            PayloadHash = "abc123",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        entry.StatusCode.ShouldBeNull();
        entry.ResponseHeaders.ShouldBeNull();
        entry.ResponseBody.ShouldBeNull();
        entry.CompletedAt.ShouldBeNull();
    }

    // =========================================================================
    // Completed entry with all properties
    // =========================================================================

    [Fact]
    public void CompletedEntry_AllProperties_SetCorrectly()
    {
        DateTimeOffset created = new(2026, 3, 21, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset completed = new(2026, 3, 21, 12, 0, 1, TimeSpan.Zero);
        byte[] body = "response"u8.ToArray();
        Dictionary<string, string[]> headers = new()
        {
            ["Content-Type"] = ["application/json"],
        };

        IdempotencyEntry entry = new()
        {
            State = IdempotencyState.Completed,
            PayloadHash = "hash-value",
            CreatedAt = created,
            StatusCode = 201,
            ResponseHeaders = headers,
            ResponseBody = body,
            CompletedAt = completed,
        };

        entry.State.ShouldBe(IdempotencyState.Completed);
        entry.StatusCode.ShouldBe(201);
        entry.ResponseHeaders.ShouldBeSameAs(headers);
        entry.ResponseBody.ShouldBe(body);
        entry.CompletedAt.ShouldBe(completed);
    }

    // =========================================================================
    // Record equality
    // =========================================================================

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        DateTimeOffset now = new(2026, 3, 21, 12, 0, 0, TimeSpan.Zero);

        IdempotencyEntry entry1 = new()
        {
            State = IdempotencyState.InProgress,
            PayloadHash = "same-hash",
            CreatedAt = now,
        };

        IdempotencyEntry entry2 = new()
        {
            State = IdempotencyState.InProgress,
            PayloadHash = "same-hash",
            CreatedAt = now,
        };

        entry1.ShouldBe(entry2);
    }

    [Fact]
    public void Equality_DifferentState_AreNotEqual()
    {
        DateTimeOffset now = new(2026, 3, 21, 12, 0, 0, TimeSpan.Zero);

        IdempotencyEntry entry1 = new()
        {
            State = IdempotencyState.InProgress,
            PayloadHash = "same-hash",
            CreatedAt = now,
        };

        IdempotencyEntry entry2 = new()
        {
            State = IdempotencyState.Completed,
            PayloadHash = "same-hash",
            CreatedAt = now,
            StatusCode = 200,
        };

        entry1.ShouldNotBe(entry2);
    }
}
