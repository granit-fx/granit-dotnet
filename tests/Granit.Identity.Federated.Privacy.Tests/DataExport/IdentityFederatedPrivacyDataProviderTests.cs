using System.Text.Json;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Privacy.DataExport;
using Granit.MultiTenancy;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Privacy.Tests.DataExport;

public sealed class IdentityFederatedPrivacyDataProviderTests
{
    private readonly IFederatedUserCacheReader _reader = Substitute.For<IFederatedUserCacheReader>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    private IdentityFederatedPrivacyDataProvider Sut() => new(_reader, _currentTenant);

    [Fact]
    public void ProviderName_Is_IdentityFederated() =>
        IdentityFederatedPrivacyDataProvider.ProviderName.ShouldBe("identity-federated");

    [Fact]
    public void ContentType_IsApplicationJson() =>
        IdentityFederatedPrivacyDataProvider.ContentType.ShouldBe("application/json");

    [Fact]
    public void FileName_IsStable() =>
        IdentityFederatedPrivacyDataProvider.FileName(Guid.NewGuid()).ShouldBe("identity-federated.json");

    [Fact]
    public async Task ExportAsync_UserNotCached_ReturnsEmpty()
    {
        _currentTenant.IsAvailable.Returns(false);
        _reader.FindByExternalIdAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((FederatedIdentity?)null);

        ReadOnlyMemory<byte> result = await Sut().ExportAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task ExportAsync_KnownUser_ReturnsJsonWithCacheEntry()
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

        ReadOnlyMemory<byte> result = await Sut().ExportAsync(userId, TestContext.Current.CancellationToken);

        result.IsEmpty.ShouldBeFalse();
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("externalUserId").GetString().ShouldBe(userId.ToString());
        doc.RootElement.GetProperty("email").GetString().ShouldBe("alice@example.com");
        doc.RootElement.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("tenantId").GetGuid().ShouldBe(tenantId);
    }
}
