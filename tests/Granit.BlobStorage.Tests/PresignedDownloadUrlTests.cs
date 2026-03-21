using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests;

public sealed class PresignedDownloadUrlTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        Uri url = new("https://s3.example.com/bucket/key?signature=abc");
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

        PresignedDownloadUrl downloadUrl = new(url, expiresAt);

        downloadUrl.Url.ShouldBe(url);
        downloadUrl.ExpiresAt.ShouldBe(expiresAt);
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        Uri url = new("https://s3.example.com/key");
        var expiry = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        PresignedDownloadUrl a = new(url, expiry);
        PresignedDownloadUrl b = new(url, expiry);

        a.ShouldBe(b);
    }
}
