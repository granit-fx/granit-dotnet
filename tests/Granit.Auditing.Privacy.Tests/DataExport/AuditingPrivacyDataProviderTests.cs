using System.Text.Json;
using Granit.Auditing.Domain;
using Granit.Auditing.Privacy.DataExport;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Privacy.Tests.DataExport;

public sealed class AuditingPrivacyDataProviderTests
{
    private readonly IAuditingReader _reader = Substitute.For<IAuditingReader>();

    [Fact]
    public void ProviderName_Is_Auditing() =>
        AuditingPrivacyDataProvider.ProviderName.ShouldBe("auditing");

    [Fact]
    public void ContentType_IsApplicationJson() =>
        AuditingPrivacyDataProvider.ContentType.ShouldBe("application/json");

    [Fact]
    public void FileName_IsStable() =>
        AuditingPrivacyDataProvider.FileName(Guid.NewGuid()).ShouldBe("auditing.json");

    [Fact]
    public async Task ExportAsync_NoEntries_ReturnsEmpty()
    {
        _reader.GetByUserAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        AuditingPrivacyDataProvider sut = new(_reader);

        ReadOnlyMemory<byte> result = await sut.ExportAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task ExportAsync_WithEntries_ReturnsJsonWithFlagsAndPayload()
    {
        var userId = Guid.NewGuid();
        List<AuditEntry> entries =
        [
            new AuditEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = new DateTimeOffset(2026, 4, 19, 10, 0, 0, TimeSpan.Zero),
                UserId = userId.ToString(),
                UserName = "alice",
                Category = AuditCategory.DataMutation,
                EntityChanges = [],
            },
        ];
        _reader.GetByUserAsync(userId.ToString(), AuditingPrivacyDataProvider.AuditExportLimit,
                Arg.Any<CancellationToken>())
            .Returns(entries);

        AuditingPrivacyDataProvider sut = new(_reader);

        ReadOnlyMemory<byte> result = await sut.ExportAsync(userId, TestContext.Current.CancellationToken);

        result.IsEmpty.ShouldBeFalse();
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("userId").GetGuid().ShouldBe(userId);
        doc.RootElement.GetProperty("exportedEntries").GetInt32().ShouldBe(1);
        doc.RootElement.GetProperty("truncated").GetBoolean().ShouldBeFalse();
        doc.RootElement.GetProperty("entries").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task ExportAsync_AtLimit_FlagsTruncated()
    {
        var userId = Guid.NewGuid();
        var entries = Enumerable.Range(0, AuditingPrivacyDataProvider.AuditExportLimit)
            .Select(i => new AuditEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-i),
                UserId = userId.ToString(),
                Category = AuditCategory.DataMutation,
            })
            .ToList();
        _reader.GetByUserAsync(userId.ToString(), AuditingPrivacyDataProvider.AuditExportLimit,
                Arg.Any<CancellationToken>())
            .Returns(entries);

        AuditingPrivacyDataProvider sut = new(_reader);

        ReadOnlyMemory<byte> result = await sut.ExportAsync(userId, TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("truncated").GetBoolean().ShouldBeTrue();
    }
}
