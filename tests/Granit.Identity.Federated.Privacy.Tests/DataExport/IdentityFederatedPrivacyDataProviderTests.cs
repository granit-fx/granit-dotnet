using Granit.Domain.ValueObjects;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Privacy.DataExport;
using Granit.MultiTenancy;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Privacy.Tests.DataExport;

public sealed class IdentityFederatedPrivacyDataProviderTests
{
    private readonly IFederatedUserCacheReader _reader = Substitute.For<IFederatedUserCacheReader>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IStagedFragmentBuilder _builder = Substitute.For<IStagedFragmentBuilder>();

    private IdentityFederatedPrivacyDataProvider Sut() => new(_reader, _currentTenant, _builder);

    private static PrivacyExportContext Ctx(Guid? subjectUserId = null) =>
        new(
            RequestId: Guid.NewGuid(),
            SubjectUserId: subjectUserId ?? Guid.NewGuid(),
            CallerUserId: subjectUserId ?? Guid.NewGuid(),
            TenantId: null,
            Regulation: "EU_GDPR");

    private static StagedExportFragment StubFragment() =>
        new()
        {
            EntryPath = "identity-federated.json",
            ContentType = "application/json",
            IntegrityTag = "v1:stub",
            StagedBlob = BlobReference.Create(Guid.NewGuid().ToString()),
        };

    [Fact]
    public void ProviderName_Is_IdentityFederated() =>
        IdentityFederatedPrivacyDataProvider.ProviderName.ShouldBe("identity-federated");

    [Fact]
    public void DisplayKey_TargetsScopeSelectorLocKey() =>
        IdentityFederatedPrivacyDataProvider.DisplayKey.ShouldBe("Privacy.Scopes.IdentityFederated");

    [Fact]
    public void FeatureName_IsNull_AlwaysVisible() =>
        IdentityFederatedPrivacyDataProvider.FeatureName.ShouldBeNull();

    [Fact]
    public async Task HasDataAsync_ReturnsTrue_WhenCacheReaderFindsEntry()
    {
        var userId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(false);
        _reader.FindByExternalIdAsync(userId.ToString(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new FederatedIdentity
            {
                Id = Guid.NewGuid(),
                ExternalUserId = userId.ToString(),
                Enabled = true,
                LastSyncedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "system",
            });

        bool has = await Sut().HasDataAsync(Ctx(userId), TestContext.Current.CancellationToken);

        has.ShouldBeTrue();
    }

    [Fact]
    public async Task HasDataAsync_ReturnsFalse_WhenCacheReaderMisses()
    {
        _currentTenant.IsAvailable.Returns(false);
        _reader.FindByExternalIdAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((FederatedIdentity?)null);

        bool has = await Sut().HasDataAsync(Ctx(), TestContext.Current.CancellationToken);

        has.ShouldBeFalse();
    }

    [Fact]
    public async Task ExportAsync_UserNotCached_YieldsNothing()
    {
        _currentTenant.IsAvailable.Returns(false);
        _reader.FindByExternalIdAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((FederatedIdentity?)null);

        List<ExportFragment> fragments = [];
        await foreach (ExportFragment f in Sut().ExportAsync(Ctx(), TestContext.Current.CancellationToken))
        {
            fragments.Add(f);
        }

        fragments.ShouldBeEmpty();
        await _builder.DidNotReceive().BuildJsonAsync(
            Arg.Any<PrivacyExportContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportAsync_KnownUser_BuildsDtoFromCacheEntry_AndYieldsFragment()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        FederatedIdentity entry = new()
        {
            Id = Guid.NewGuid(),
            ExternalUserId = userId.ToString(),
            Username = "alice",
            Email = "alice@example.com",
            FirstName = "Alice",
            LastName = "Example",
            Enabled = true,
            LastSyncedAt = new DateTimeOffset(2026, 4, 19, 10, 0, 0, TimeSpan.Zero),
            TenantId = tenantId,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = "system",
        };
        _reader.FindByExternalIdAsync(userId.ToString(), tenantId, Arg.Any<CancellationToken>())
            .Returns(entry);

        IdentityFederatedExportResponse? capturedDto = null;
        _builder.BuildJsonAsync(
            Arg.Any<PrivacyExportContext>(),
            IdentityFederatedPrivacyDataProvider.ProviderName,
            "identity-federated.json",
            Arg.Do<IdentityFederatedExportResponse>(d => capturedDto = d),
            Arg.Any<CancellationToken>())
            .Returns(StubFragment());

        List<ExportFragment> fragments = [];
        await foreach (ExportFragment f in Sut().ExportAsync(Ctx(userId), TestContext.Current.CancellationToken))
        {
            fragments.Add(f);
        }

        fragments.Count.ShouldBe(1);
        fragments[0].ShouldBeOfType<StagedExportFragment>();
        capturedDto.ShouldNotBeNull();
        capturedDto!.ExternalUserId.ShouldBe(userId.ToString());
        capturedDto.Email.ShouldBe("alice@example.com");
        capturedDto.Enabled.ShouldBeTrue();
        capturedDto.TenantId.ShouldBe(tenantId);
    }
}
