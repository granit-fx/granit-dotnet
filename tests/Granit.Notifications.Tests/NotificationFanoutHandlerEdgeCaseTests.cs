using System.Diagnostics.Metrics;
using System.Text.Json;
using Granit.Core.MultiTenancy;
using Granit.Guids;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Diagnostics;
using Granit.Notifications.Handlers;
using Granit.Notifications.Messages;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class NotificationFanoutHandlerEdgeCaseTests : IDisposable
{
    private readonly INotificationSubscriptionReader _subscriptionReader = Substitute.For<INotificationSubscriptionReader>();
    private readonly INotificationPreferenceReader _preferenceReader = Substitute.For<INotificationPreferenceReader>();
    private readonly INotificationDefinitionStore _definitionStore = Substitute.For<INotificationDefinitionStore>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly NotificationFanoutHandler _handler;
    private readonly ServiceProvider _sp;

    public NotificationFanoutHandlerEdgeCaseTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        IMeterFactory meterFactory = _sp.GetRequiredService<IMeterFactory>();

        _currentTenant.IsAvailable.Returns(false);
        _handler = new NotificationFanoutHandler(
            _subscriptionReader,
            _preferenceReader,
            _definitionStore,
            new SimpleGuidGenerator(),
            _currentTenant,
            new NotificationsMetrics(meterFactory));
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public async Task HandleAsync_definition_not_found_uses_InApp_default()
    {
        // Arrange — definition returns null, handler defaults to InApp
        _definitionStore.Get("unknown.notification").Returns((NotificationDefinition?)null);
        _preferenceReader.IsChannelEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        NotificationTrigger trigger = BuildTrigger(
            notificationTypeName: "unknown.notification",
            recipientUserIds: ["user-1"]);

        // Act
        IEnumerable<DeliverNotificationCommand> result =
            await _handler.HandleAsync(trigger, TestContext.Current.CancellationToken);

        // Assert — defaults to InApp channel
        var commands = result.ToList();
        commands.Count.ShouldBe(1);
        commands.Single().ChannelName.ShouldBe(NotificationChannels.InApp);
    }

    [Fact]
    public async Task HandleAsync_definition_not_found_allows_opt_out_by_default()
    {
        // Arrange — null definition → allowOptOut defaults to true
        _definitionStore.Get("unknown.notification").Returns((NotificationDefinition?)null);
        _preferenceReader.IsChannelEnabledAsync("user-1", "unknown.notification", NotificationChannels.InApp, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        NotificationTrigger trigger = BuildTrigger(
            notificationTypeName: "unknown.notification",
            recipientUserIds: ["user-1"]);

        // Act
        IEnumerable<DeliverNotificationCommand> result =
            await _handler.HandleAsync(trigger, TestContext.Current.CancellationToken);

        // Assert — opted out, no commands
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_commands_contain_culture_from_trigger()
    {
        // Arrange
        NotificationDefinition definition = new("test.notification")
        {
            DefaultChannels = [NotificationChannels.InApp],
            AllowUserOptOut = false,
        };
        _definitionStore.Get("test.notification").Returns(definition);

        NotificationTrigger trigger = BuildTrigger(
            recipientUserIds: ["user-1"],
            culture: "nl-BE");

        // Act
        IEnumerable<DeliverNotificationCommand> result =
            await _handler.HandleAsync(trigger, TestContext.Current.CancellationToken);

        // Assert
        var commands = result.ToList();
        commands.Single().Culture.ShouldBe("nl-BE");
    }

    [Fact]
    public async Task HandleAsync_commands_contain_related_entity()
    {
        // Arrange
        NotificationDefinition definition = new("test.notification")
        {
            DefaultChannels = [NotificationChannels.InApp],
            AllowUserOptOut = false,
        };
        _definitionStore.Get("test.notification").Returns(definition);

        EntityReference entity = new("Document", "doc-99");
        _subscriptionReader.GetEntityFollowerIdsAsync("Document", "doc-99", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["follower-1"]));

        NotificationTrigger trigger = BuildTrigger(
            recipientUserIds: [],
            relatedEntity: entity);

        // Act
        IEnumerable<DeliverNotificationCommand> result =
            await _handler.HandleAsync(trigger, TestContext.Current.CancellationToken);

        // Assert
        var commands = result.ToList();
        commands.Single().RelatedEntity.ShouldBe(entity);
    }

    [Fact]
    public async Task HandleAsync_command_tenant_id_uses_ambient_over_trigger()
    {
        // Arrange
        var ambientTenantId = Guid.NewGuid();
        var triggerTenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(ambientTenantId);

        NotificationDefinition definition = new("test.notification")
        {
            DefaultChannels = [NotificationChannels.InApp],
            AllowUserOptOut = false,
        };
        _definitionStore.Get("test.notification").Returns(definition);

        NotificationTrigger trigger = BuildTrigger(
            recipientUserIds: ["user-1"],
            tenantId: triggerTenantId);

        // Act
        IEnumerable<DeliverNotificationCommand> result =
            await _handler.HandleAsync(trigger, TestContext.Current.CancellationToken);

        // Assert — should use ambient, not trigger
        var commands = result.ToList();
        commands.Single().TenantId.ShouldBe(ambientTenantId);
    }

    [Fact]
    public async Task HandleAsync_no_followers_no_subscribers_returns_empty()
    {
        // Arrange
        NotificationDefinition definition = new("test.notification")
        {
            DefaultChannels = [NotificationChannels.InApp],
        };
        _definitionStore.Get("test.notification").Returns(definition);

        EntityReference entity = new("Patient", "pat-1");
        _subscriptionReader.GetEntityFollowerIdsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>([]));
        _subscriptionReader.GetSubscriberIdsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>([]));

        NotificationTrigger trigger = BuildTrigger(
            recipientUserIds: [],
            relatedEntity: entity);

        // Act
        IEnumerable<DeliverNotificationCommand> result =
            await _handler.HandleAsync(trigger, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static NotificationTrigger BuildTrigger(
        string notificationTypeName = "test.notification",
        IReadOnlyList<string>? recipientUserIds = null,
        EntityReference? relatedEntity = null,
        Guid? tenantId = null,
        string? culture = null) => new()
        {
            NotificationTypeName = notificationTypeName,
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(new { key = "value" }),
            RecipientUserIds = recipientUserIds ?? ["user-1"],
            RelatedEntity = relatedEntity,
            TenantId = tenantId,
            OccurredAt = DateTimeOffset.UtcNow,
            Culture = culture,
        };
}
