using Granit.AuditLog.Abstractions;
using Granit.AuditLog.ConfigurationChanges.Handlers;
using Granit.AuditLog.Domain;
using Granit.Core.MultiTenancy;
using Granit.Guids;
using Granit.Security;
using Granit.Settings.Events;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.ConfigurationChanges.Tests.Handlers;

public sealed class SettingChangedAuditHandlerTests
{
    [Fact]
    public async Task HandleAsync_PersistsAuditEntry_WithConfigurationChangeCategory()
    {
        IAuditLogWriter writer = Substitute.For<IAuditLogWriter>();
        ICurrentUserService user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns("test-user");
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        IGuidGenerator guids = Substitute.For<IGuidGenerator>();
        guids.Create().Returns(_ => Guid.NewGuid());

        SettingChangedAuditHandler handler = new(writer, user, tenant, guids);
        SettingChangedEvent evt = new("App.Theme", "G", null, "light", "dark", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await writer.Received(1).WriteAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.Category == AuditLogCategory.ConfigurationChange &&
                e.EntityChanges.First().EntityType == "Setting"),
            Arg.Any<CancellationToken>());
    }
}
