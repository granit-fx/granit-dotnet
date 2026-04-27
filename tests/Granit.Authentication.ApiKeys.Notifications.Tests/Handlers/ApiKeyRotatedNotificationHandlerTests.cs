using Granit.Authentication.ApiKeys.Events;
using Granit.Authentication.ApiKeys.Notifications.Handlers;
using Granit.Notifications.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Notifications.Tests.Handlers;

public sealed class ApiKeyRotatedNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesRotatedNotification_ToSubscribers()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        // The originating Eto carries an OldHashedKey for cache-eviction. The handler
        // must NOT propagate it.
        ApiKeyRotatedEto evt = new(oldId, newId, OldHashedKey: "sha256:DO_NOT_FORWARD_THIS");

        await ApiKeyRotatedNotificationHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            ApiKeyRotatedNotificationType.Instance,
            Arg.Is<ApiKeyRotatedNotificationData>(d =>
                d.OldKeyId == oldId
                && d.NewKeyId == newId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The originating <see cref="ApiKeyRotatedEto"/> carries an <c>OldHashedKey</c> field.
    /// The notification data record must NOT expose any field that could leak that hash.
    /// </summary>
    [Fact]
    public void NotificationData_DoesNotExposeAnySecretMaterial()
    {
        Type dataType = typeof(ApiKeyRotatedNotificationData);

        IEnumerable<string> propertyNames = dataType
            .GetProperties()
            .Select(p => p.Name);

        propertyNames.ShouldNotContain(name => ContainsSecretMarker(name));
    }

    /// <summary>
    /// Even when the originating Eto carries a SHA-256 hash, the serialized
    /// notification payload must contain no trace of it.
    /// </summary>
    [Fact]
    public async Task HandleAsync_DoesNotForwardHashedKey_EvenWhenEtoCarriesIt()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        const string hash = "sha256:LEAK_BAIT";
        ApiKeyRotatedEto evt = new(Guid.NewGuid(), Guid.NewGuid(), hash);

        await ApiKeyRotatedNotificationHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            ApiKeyRotatedNotificationType.Instance,
            Arg.Is<ApiKeyRotatedNotificationData>(d =>
                !System.Text.Json.JsonSerializer.Serialize(d).Contains(hash, StringComparison.OrdinalIgnoreCase)),
            Arg.Any<CancellationToken>());
    }

    private static bool ContainsSecretMarker(string fieldName) =>
        fieldName.Contains("Hash", StringComparison.OrdinalIgnoreCase)
        || fieldName.Contains("Secret", StringComparison.OrdinalIgnoreCase)
        || fieldName.Equals("Value", StringComparison.OrdinalIgnoreCase)
        || fieldName.Contains("RawKey", StringComparison.OrdinalIgnoreCase);
}
