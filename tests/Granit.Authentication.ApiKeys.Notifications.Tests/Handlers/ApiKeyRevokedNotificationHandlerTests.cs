using Granit.Authentication.ApiKeys.Events;
using Granit.Authentication.ApiKeys.Notifications.Handlers;
using Granit.Notifications.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Notifications.Tests.Handlers;

public sealed class ApiKeyRevokedNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesRevokedNotification_ToSubscribers()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var keyId = Guid.NewGuid();
        // The originating Eto carries a HashedKey for cache-eviction. The handler
        // must NOT propagate it.
        ApiKeyRevokedEto evt = new(keyId, HashedKey: "sha256:DO_NOT_FORWARD_THIS");

        await ApiKeyRevokedNotificationHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            ApiKeyRevokedNotificationType.Instance,
            Arg.Is<ApiKeyRevokedNotificationData>(d => d.KeyId == keyId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The notification data record must not expose any field that could leak the
    /// hash carried by the originating <see cref="ApiKeyRevokedEto"/>.
    /// </summary>
    [Fact]
    public void NotificationData_DoesNotExposeAnySecretMaterial()
    {
        Type dataType = typeof(ApiKeyRevokedNotificationData);

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
        ApiKeyRevokedEto evt = new(Guid.NewGuid(), hash);

        await ApiKeyRevokedNotificationHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            ApiKeyRevokedNotificationType.Instance,
            Arg.Is<ApiKeyRevokedNotificationData>(d =>
                !System.Text.Json.JsonSerializer.Serialize(d).Contains(hash, StringComparison.OrdinalIgnoreCase)),
            Arg.Any<CancellationToken>());
    }

    private static bool ContainsSecretMarker(string fieldName) =>
        fieldName.Contains("Hash", StringComparison.OrdinalIgnoreCase)
        || fieldName.Contains("Secret", StringComparison.OrdinalIgnoreCase)
        || fieldName.Equals("Value", StringComparison.OrdinalIgnoreCase)
        || fieldName.Contains("RawKey", StringComparison.OrdinalIgnoreCase);
}
