using Granit.Auditing.ConfigurationChanges.Handlers;
using Granit.Auditing.Domain;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Settings.Events;
using Granit.Users;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Auditing.ConfigurationChanges.Tests.Handlers;

public sealed class SettingChangedAuditHandlerTests
{
    [Fact]
    public async Task HandleAsync_PersistsAuditEntry_WithConfigurationChangeCategory()
    {
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
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
            Arg.Is<AuditEntry>(e =>
                e.Category == AuditCategory.ConfigurationChange &&
                e.EntityChanges.First().EntityType == "Setting"),
            Arg.Any<CancellationToken>());
    }
}
