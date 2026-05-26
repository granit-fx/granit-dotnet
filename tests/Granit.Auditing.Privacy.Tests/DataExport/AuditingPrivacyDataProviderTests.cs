using Granit.Auditing.Domain;
using Granit.Auditing.Privacy.DataExport;
using Granit.Domain.ValueObjects;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Privacy.Tests.DataExport;

public sealed class AuditingPrivacyDataProviderTests
{
    private readonly IAuditingReader _reader = Substitute.For<IAuditingReader>();
    private readonly IStagedFragmentBuilder _builder = Substitute.For<IStagedFragmentBuilder>();

    private static PrivacyExportContext Ctx(Guid? subjectUserId = null) =>
        new(
            RequestId: Guid.NewGuid(),
            SubjectUserId: subjectUserId ?? Guid.NewGuid(),
            CallerUserId: subjectUserId ?? Guid.NewGuid(),
            TenantId: null,
            Regulation: "EU_GDPR");

    private static StagedExportFragment StubFragment(string entryPath = "auditing.json") =>
        new()
        {
            EntryPath = entryPath,
            ContentType = "application/json",
            IntegrityTag = "v1:stub",
            StagedBlob = BlobReference.Create(Guid.NewGuid().ToString()),
        };

    [Fact]
    public void ProviderName_Is_Auditing() =>
        AuditingPrivacyDataProvider.ProviderName.ShouldBe("auditing");

    [Fact]
    public void DisplayKey_TargetsScopeSelectorLocKey() =>
        AuditingPrivacyDataProvider.DisplayKey.ShouldBe("Privacy.Scopes.Auditing");

    [Fact]
    public void FeatureName_IsNull_AlwaysVisible() =>
        AuditingPrivacyDataProvider.FeatureName.ShouldBeNull();

    [Fact]
    public async Task HasDataAsync_ReturnsTrue_WhenReaderProbeFindsEntries()
    {
        var userId = Guid.NewGuid();
        _reader.GetByUserAsync(userId.ToString(), limit: 1, Arg.Any<CancellationToken>())
            .Returns([new AuditEntry { Id = Guid.NewGuid(), UserId = userId.ToString(), Category = AuditCategory.DataMutation, EntityChanges = [] }]);

        AuditingPrivacyDataProvider sut = new(_reader, _builder);

        bool has = await sut.HasDataAsync(Ctx(userId), TestContext.Current.CancellationToken);

        has.ShouldBeTrue();
        await _reader.Received(1).GetByUserAsync(userId.ToString(), limit: 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HasDataAsync_ReturnsFalse_WhenReaderProbeIsEmpty()
    {
        _reader.GetByUserAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        AuditingPrivacyDataProvider sut = new(_reader, _builder);

        bool has = await sut.HasDataAsync(Ctx(), TestContext.Current.CancellationToken);

        has.ShouldBeFalse();
    }

    [Fact]
    public async Task ExportAsync_NoEntries_YieldsNothing()
    {
        _reader.GetByUserAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        AuditingPrivacyDataProvider sut = new(_reader, _builder);

        List<ExportFragment> fragments = [];
        await foreach (ExportFragment f in sut.ExportAsync(Ctx(), TestContext.Current.CancellationToken))
        {
            fragments.Add(f);
        }

        fragments.ShouldBeEmpty();
        await _builder.DidNotReceive().BuildJsonAsync(
            Arg.Any<PrivacyExportContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportAsync_WithEntries_HandsDtoToBuilder_AndYieldsFragment()
    {
        var userId = Guid.NewGuid();
        List<AuditEntry> entries =
        [
            new()
            {
                Id = Guid.NewGuid(),
                Timestamp = new DateTimeOffset(2026, 4, 19, 10, 0, 0, TimeSpan.Zero),
                UserId = userId.ToString(),
                UserName = "alice",
                Category = AuditCategory.DataMutation,
                EntityChanges = [],
            },
        ];
        _reader.GetByUserAsync(userId.ToString(), AuditingPrivacyDataProvider.AuditExportLimit, Arg.Any<CancellationToken>())
            .Returns(entries);

        AuditingExportDto? capturedDto = null;
        _builder.BuildJsonAsync(
            Arg.Any<PrivacyExportContext>(),
            AuditingPrivacyDataProvider.ProviderName,
            "auditing.json",
            Arg.Do<AuditingExportDto>(d => capturedDto = d),
            Arg.Any<CancellationToken>())
            .Returns(StubFragment());

        AuditingPrivacyDataProvider sut = new(_reader, _builder);

        List<ExportFragment> fragments = [];
        await foreach (ExportFragment f in sut.ExportAsync(Ctx(userId), TestContext.Current.CancellationToken))
        {
            fragments.Add(f);
        }

        fragments.Count.ShouldBe(1);
        fragments[0].ShouldBeOfType<StagedExportFragment>();
        capturedDto.ShouldNotBeNull();
        capturedDto!.UserId.ShouldBe(userId);
        capturedDto.ExportedEntries.ShouldBe(1);
        capturedDto.Truncated.ShouldBeFalse();
        capturedDto.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExportAsync_AtLimit_DtoFlagsTruncated()
    {
        var userId = Guid.NewGuid();
        List<AuditEntry> entries = [.. Enumerable.Range(0, AuditingPrivacyDataProvider.AuditExportLimit)
            .Select(i => new AuditEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-i),
                UserId = userId.ToString(),
                Category = AuditCategory.DataMutation,
                EntityChanges = [],
            })];
        _reader.GetByUserAsync(userId.ToString(), AuditingPrivacyDataProvider.AuditExportLimit, Arg.Any<CancellationToken>())
            .Returns(entries);

        AuditingExportDto? capturedDto = null;
        _builder.BuildJsonAsync(
            Arg.Any<PrivacyExportContext>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<AuditingExportDto>(d => capturedDto = d),
            Arg.Any<CancellationToken>())
            .Returns(StubFragment());

        AuditingPrivacyDataProvider sut = new(_reader, _builder);

        await foreach (ExportFragment _ in sut.ExportAsync(Ctx(userId), TestContext.Current.CancellationToken)) { }

        capturedDto.ShouldNotBeNull();
        capturedDto!.Truncated.ShouldBeTrue();
        capturedDto.Limit.ShouldBe(AuditingPrivacyDataProvider.AuditExportLimit);
    }
}
