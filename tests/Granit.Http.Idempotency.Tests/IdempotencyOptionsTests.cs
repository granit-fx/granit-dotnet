using Granit.Http.Idempotency.Models;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.Tests;

public sealed class IdempotencyOptionsTests
{
    // =========================================================================
    // Default values
    // =========================================================================

    [Fact]
    public void SectionName_IsIdempotency() => IdempotencyOptions.SectionName.ShouldBe("Idempotency");

    [Fact]
    public void DefaultHeaderName_IsIdempotencyKey()
    {
        IdempotencyOptions options = new();

        options.HeaderName.ShouldBe("Idempotency-Key");
    }

    [Fact]
    public void DefaultKeyPrefix_IsIdp()
    {
        IdempotencyOptions options = new();

        options.KeyPrefix.ShouldBe("idp");
    }

    [Fact]
    public void DefaultCompletedTtl_Is24Hours()
    {
        IdempotencyOptions options = new();

        options.CompletedTtl.ShouldBe(TimeSpan.FromHours(24));
    }

    [Fact]
    public void DefaultInProgressTtl_Is30Seconds()
    {
        IdempotencyOptions options = new();

        options.InProgressTtl.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void DefaultExecutionTimeout_Is25Seconds()
    {
        IdempotencyOptions options = new();

        options.ExecutionTimeout.ShouldBe(TimeSpan.FromSeconds(25));
    }

    [Fact]
    public void DefaultMaxBodySizeBytes_Is1MiB()
    {
        IdempotencyOptions options = new();

        options.MaxBodySizeBytes.ShouldBe(1 * 1024 * 1024);
    }

    // =========================================================================
    // ShouldCacheStatusCode — default predicate
    // =========================================================================

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, true)]
    [InlineData(204, true)]
    [InlineData(299, true)]
    [InlineData(400, true)]
    [InlineData(404, true)]
    [InlineData(409, true)]
    [InlineData(410, true)]
    [InlineData(422, true)]
    public void ShouldCacheStatusCode_CacheableStatusCodes_ReturnsTrue(int statusCode, bool expected)
    {
        IdempotencyOptions options = new();

        options.ShouldCacheStatusCode(statusCode).ShouldBe(expected);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public void ShouldCacheStatusCode_NonCacheableStatusCodes_ReturnsFalse(int statusCode)
    {
        IdempotencyOptions options = new();

        options.ShouldCacheStatusCode(statusCode).ShouldBeFalse();
    }

    // =========================================================================
    // Custom values
    // =========================================================================

    [Fact]
    public void HeaderName_CanBeCustomized()
    {
        IdempotencyOptions options = new() { HeaderName = "X-Custom-Key" };

        options.HeaderName.ShouldBe("X-Custom-Key");
    }

    [Fact]
    public void KeyPrefix_CanBeCustomized()
    {
        IdempotencyOptions options = new() { KeyPrefix = "myapp" };

        options.KeyPrefix.ShouldBe("myapp");
    }

    [Fact]
    public void ShouldCacheStatusCode_CanBeOverridden()
    {
        IdempotencyOptions options = new()
        {
            ShouldCacheStatusCode = static sc => sc == 200,
        };

        options.ShouldCacheStatusCode(200).ShouldBeTrue();
        options.ShouldCacheStatusCode(201).ShouldBeFalse();
        options.ShouldCacheStatusCode(400).ShouldBeFalse();
    }
}
