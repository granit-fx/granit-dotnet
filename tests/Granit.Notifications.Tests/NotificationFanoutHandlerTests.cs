// =============================================================================
// Tests - NotificationFanoutHandler
// =============================================================================
// Verifies fan-out logic: empty result when no recipients/subscribers, correct
// command count per recipient x channel, opt-out filtering, entity followers
// fallback, tenant resolution priority, and distinct delivery IDs.
// =============================================================================

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Granit.Domain;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Diagnostics;
using Granit.Notifications.Handlers;
using Granit.Notifications.Messages;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class NotificationFanoutHandlerTests : IDisposable
{
    private readonly INotificationSubscriptionReader _subscriptionReader = Substitute.For<INotificationSubscriptionReader>();
    private readonly INotificationPreferenceReader _preferenceReader = Substitute.For<INotificationPreferenceReader>();
    private readonly INotificationDefinitionStore _definitionStore = Substitute.For<INotificationDefinitionStore>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly NotificationFanoutHandler _handler;
    private readonly ActivityListener _activityListener;
    private readonly ServiceProvider _sp;

    public NotificationFanoutHandlerTests()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Granit.Notifications",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_activityListener);

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

    public void Dispose()
    {
        _activityListener.Dispose();
        _sp.Dispose();
    }

    [Fact]
    public async Task HandleAsync_NoRecipients_NoSubscribers_ReturnsEmpty()
    {
        NotificationDefinition definition = BuildDefinition("test.notification", [NotificationChannels.InApp]);
        _definitionStore.Get("test.notification").Returns(definition);
        _subscriptionReader.GetSubscriberIdsAsync("test.notification", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>([]));

        NotificationTrigger trigger = BuildTrigger(recipientUserIds: []);

        IEnumerable<DeliverNotificationCommand> result =
            await _handler.HandleAsync(trigger, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ExplicitRecipients_TwoUsersThreeChannels_ReturnsSixCommands()
    {
        NotificationDefinition definition = BuildDefinition("test.notification",
            [NotificationChannels.InApp, NotificationChannels.Email, NotificationChannels.WebPush]);
        _definitionStore.Get("test.notification").Returns(definition);
        _preferenceReader.IsChannelEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        NotificationTrigger trigger = BuildTrigger(recipientUserIds: ["user-1", "user-2"]);

        IEnumerable<DeliverNotificationCommand> result =
            await _handler.HandleAsync(trigger, TestContext.Current.CancellationToken);

        result.Count().ShouldBe(6);
    }

    [Fact]
    public async Task HandleAsync_OptedOutChannel_SkipsCommand()
    {
        NotificationDefinition definition = BuildDefinition("test.notification",
            [NotificationChannels.InApp, NotificationChannels.Email]);
        _definitionStore.Get("test.notification").Returns(definition);

        _preferenceReader.IsChannelEnabledAsync("user-1", "test.notification", NotificationChannels.InApp, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        _preferenceReader.IsChannelEnabledAsync("user-1", "test.notification", NotificationChannels.Email, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        NotificationTrigger trigger = BuildTrigger(recipientUserIds: ["user-1"]);

        IEnumerable<DeliverNotificationCommand> result =
            await _handler.HandleAsync(trigger, TestContext.Current.CancellationToken);

        var commands = result.ToList();
        commands.Count.ShouldBe(1);
        commands.Single().ChannelName.ShouldBe(NotificationChannels.InApp);
    }

    [Fact]
    public async Task HandleAsync_AllowUserOptOutFalse_IgnoresPreference()
    {
        NotificationDefinition definition = BuildDefinition("security.alert",
            [NotificationChannels.InApp, NotificationChannels.Email], allowUserOptOut: false);
        _definitionStore.Get("security.alert").Returns(definition);

        NotificationTrigger trigger = BuildTrigger(notificationTypeName: "security.alert", recipientUserIds: ["user-1"]);

        IEnumerable<DeliverNotificationCommand> result =
            await _handler.HandleAsync(trigger, TestContext.Current.CancellationToken);

        result.Count().ShouldBe(2, "AllowUserOptOut is false, so opt-out is ignored");
    }

    [Fact]
    public async Task HandleAsync_NoExplicitRecipients_WithEntityFollowers_UsesFollowers()
    {
        NotificationDefinition definition = BuildDefinition("test.notification", [NotificationChannels.InApp]);
        _definitionStore.Get("test.notification").Returns(definition);
        _preferenceReader.IsChannelEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        EntityReference entity = new("Invoice", "inv-42");
        _subscriptionReader.GetEntityFollowerIdsAsync(entity.EntityType, entity.EntityId, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["follower-1", "follower-2"]));

        NotificationTrigger trigger = BuildTrigger(recipientUserIds: [], relatedEntity: entity);

        IEnumerable<DeliverNotificationCommand> result =
            await _handler.HandleAsync(trigger, TestContext.Current.CancellationToken);

        result.Count().ShouldBe(2);
        await _subscriptionReader.Received(1).GetEntityFollowerIdsAsync(
            entity.EntityType, entity.EntityId, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NoExplicitRecipients_NoFollowers_UsesSubscribers()
    {
        NotificationDefinition definition = BuildDefinition("test.notification", [NotificationChannels.InApp]);
        _definitionStore.Get("test.notification").Returns(definition);
        _preferenceReader.IsChannelEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        _subscriptionReader.GetSubscriberIdsAsync("test.notification", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["subscriber-1"]));

        NotificationTrigger trigger = BuildTrigger(recipientUserIds: []);

        IEnumerable<DeliverNotificationCommand> result =
            await _handler.HandleAsync(trigger, TestContext.Current.CancellationToken);

        result.Count().ShouldBe(1);
        await _subscriptionReader.Received(1).GetSubscriberIdsAsync(
            "test.notification", Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_UsesAmbientTenant_WhenAvailable()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        NotificationDefinition definition = BuildDefinition("test.notification", [NotificationChannels.InApp]);
        _definitionStore.Get("test.notification").Returns(definition);
        _preferenceReader.IsChannelEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        _subscriptionReader.GetSubscriberIdsAsync("test.notification", tenantId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["user-1"]));

        NotificationTrigger trigger = BuildTrigger(recipientUserIds: [], tenantId: Guid.NewGuid());

        await _handler.HandleAsync(trigger, TestContext.Current.CancellationToken);

        await _subscriptionReader.Received(1).GetSubscriberIdsAsync(
            Arg.Any<string>(), tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_FallsBackToTriggerTenantId_WhenAmbientNotAvailable()
    {
        var triggerTenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(false);

        NotificationDefinition definition = BuildDefinition("test.notification", [NotificationChannels.InApp]);
        _definitionStore.Get("test.notification").Returns(definition);
        _preferenceReader.IsChannelEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        _subscriptionReader.GetSubscriberIdsAsync("test.notification", triggerTenantId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["user-1"]));

        NotificationTrigger trigger = BuildTrigger(recipientUserIds: [], tenantId: triggerTenantId);

        await _handler.HandleAsync(trigger, TestContext.Current.CancellationToken);

        await _subscriptionReader.Received(1).GetSubscriberIdsAsync(
            Arg.Any<string>(), triggerTenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Commands_HaveDistinctDeliveryIds()
    {
        NotificationDefinition definition = BuildDefinition("test.notification",
            [NotificationChannels.InApp, NotificationChannels.Email]);
        _definitionStore.Get("test.notification").Returns(definition);
        _preferenceReader.IsChannelEnabledAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        NotificationTrigger trigger = BuildTrigger(recipientUserIds: ["user-1", "user-2"]);

        IEnumerable<DeliverNotificationCommand> result =
            await _handler.HandleAsync(trigger, TestContext.Current.CancellationToken);

        var commands = result.ToList();
        commands.Select(c => c.DeliveryId).Distinct().Count().ShouldBe(commands.Count);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static NotificationTrigger BuildTrigger(
        string notificationTypeName = "test.notification",
        IReadOnlyList<string>? recipientUserIds = null,
        EntityReference? relatedEntity = null,
        Guid? tenantId = null) => new()
        {
            NotificationTypeName = notificationTypeName,
            Severity = NotificationSeverity.Info,
            Data = JsonSerializer.SerializeToElement(new { key = "value" }),
            RecipientUserIds = recipientUserIds ?? ["user-1"],
            RelatedEntity = relatedEntity,
            TenantId = tenantId,
            OccurredAt = DateTimeOffset.UtcNow,
        };

    private static NotificationDefinition BuildDefinition(
        string name,
        IReadOnlyList<string> channels,
        bool allowUserOptOut = true) => new(name)
        {
            DefaultChannels = channels,
            AllowUserOptOut = allowUserOptOut,
        };
}
