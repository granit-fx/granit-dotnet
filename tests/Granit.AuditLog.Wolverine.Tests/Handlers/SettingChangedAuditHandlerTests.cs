using Granit.AuditLog.Abstractions;
using Granit.AuditLog.Domain;
using Granit.AuditLog.Wolverine.Handlers;
using Granit.Core.MultiTenancy;
using Granit.Guids;
using Granit.Security;
using Granit.Settings.Events;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Wolverine.Tests.Handlers;

public sealed class SettingChangedAuditHandlerTests
{
    private static (IAuditLogWriter writer, ICurrentUserService user, ICurrentTenant tenant, IGuidGenerator guids) CreateMocks()
    {
        IAuditLogWriter writer = Substitute.For<IAuditLogWriter>();
        ICurrentUserService user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns("test-user");
        user.UserName.Returns("Test User");
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

        SettingChangedEvent evt = new("App.Theme", "G", null, "light", "dark", DateTimeOffset.UtcNow);

        await SettingChangedAuditHandler.HandleAsync(evt, writer, user, tenant, guids,
            TestContext.Current.CancellationToken);

        await writer.Received(1).WriteAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.Category == AuditLogCategory.ConfigurationChange &&
                e.UserId == "test-user"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SetsEntityType_ToSetting()
    {
        (IAuditLogWriter writer, ICurrentUserService user, ICurrentTenant tenant, IGuidGenerator guids) = CreateMocks();

        SettingChangedEvent evt = new("App.Theme", "T", "tenant-123", null, "blue", DateTimeOffset.UtcNow);

        await SettingChangedAuditHandler.HandleAsync(evt, writer, user, tenant, guids,
            TestContext.Current.CancellationToken);

        await writer.Received(1).WriteAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.EntityChanges.Count == 1 &&
                e.EntityChanges.First().EntityType == "Setting" &&
                e.EntityChanges.First().ChangeType == AuditChangeType.Created),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DeletedSetting_SetsChangeTypeDeleted()
    {
        (IAuditLogWriter writer, ICurrentUserService user, ICurrentTenant tenant, IGuidGenerator guids) = CreateMocks();

        SettingChangedEvent evt = new("App.Theme", "G", null, "dark", null, DateTimeOffset.UtcNow);

        await SettingChangedAuditHandler.HandleAsync(evt, writer, user, tenant, guids,
            TestContext.Current.CancellationToken);

        await writer.Received(1).WriteAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.EntityChanges.First().ChangeType == AuditChangeType.Deleted),
            Arg.Any<CancellationToken>());
    }
}
