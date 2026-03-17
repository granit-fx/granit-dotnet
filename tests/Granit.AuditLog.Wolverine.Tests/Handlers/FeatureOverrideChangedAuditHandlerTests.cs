using Granit.AuditLog.Abstractions;
using Granit.AuditLog.Domain;
using Granit.AuditLog.Wolverine.Handlers;
using Granit.Core.MultiTenancy;
using Granit.Features.Events;
using Granit.Guids;
using Granit.Security;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Wolverine.Tests.Handlers;

public sealed class FeatureOverrideChangedAuditHandlerTests
{
    private static (IAuditLogWriter writer, ICurrentUserService user, ICurrentTenant tenant, IGuidGenerator guids) CreateMocks()
    {
        IAuditLogWriter writer = Substitute.For<IAuditLogWriter>();
        ICurrentUserService user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns("admin");
        user.UserName.Returns("Admin");
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        IGuidGenerator guids = Substitute.For<IGuidGenerator>();
        guids.Create().Returns(_ => Guid.NewGuid());
        return (writer, user, tenant, guids);
    }

    [Fact]
    public async Task HandleAsync_PersistsAuditEntry_WithConfigurationChangeCategory()
    {
        (IAuditLogWriter writer, ICurrentUserService user, ICurrentTenant tenant, IGuidGenerator guids) = CreateMocks();

        var tenantId = Guid.NewGuid();
        FeatureOverrideChangedEvent evt = new("Acme.MaxUsers", tenantId, "50", "200", DateTimeOffset.UtcNow);

        await FeatureOverrideChangedAuditHandler.HandleAsync(evt, writer, user, tenant, guids,
            TestContext.Current.CancellationToken);

        await writer.Received(1).WriteAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.Category == AuditLogCategory.ConfigurationChange &&
                e.TenantId == tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SetsEntityType_ToFeatureOverride()
    {
        (IAuditLogWriter writer, ICurrentUserService user, ICurrentTenant tenant, IGuidGenerator guids) = CreateMocks();

        FeatureOverrideChangedEvent evt = new("Acme.Feature", null, null, "true", DateTimeOffset.UtcNow);

        await FeatureOverrideChangedAuditHandler.HandleAsync(evt, writer, user, tenant, guids,
            TestContext.Current.CancellationToken);

        await writer.Received(1).WriteAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.EntityChanges.Count == 1 &&
                e.EntityChanges.First().EntityType == "FeatureOverride" &&
                e.EntityChanges.First().EntityId == "Acme.Feature" &&
                e.EntityChanges.First().ChangeType == AuditChangeType.Created),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CapturesOldAndNewValues()
    {
        (IAuditLogWriter writer, ICurrentUserService user, ICurrentTenant tenant, IGuidGenerator guids) = CreateMocks();

        FeatureOverrideChangedEvent evt = new("Acme.Limit", Guid.NewGuid(), "100", "500", DateTimeOffset.UtcNow);

        await FeatureOverrideChangedAuditHandler.HandleAsync(evt, writer, user, tenant, guids,
            TestContext.Current.CancellationToken);

        await writer.Received(1).WriteAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.EntityChanges.First().PropertyChanges.Count == 1 &&
                e.EntityChanges.First().PropertyChanges.First().OriginalValue == "100" &&
                e.EntityChanges.First().PropertyChanges.First().NewValue == "500"),
            Arg.Any<CancellationToken>());
    }
}
