using Granit.Authentication.ApiKeys.Events;
using Granit.Authentication.ApiKeys.Notifications.Handlers;
using Granit.Notifications.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Notifications.Tests.Handlers;

public sealed class ApiKeyExpiringSoonNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesExpiringSoonNotification_WithComputedDaysUntilExpiry()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        FakeTimeProvider time = new(DateTimeOffset.Parse("2026-04-27T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var keyId = Guid.NewGuid();
        const string keyName = "Partner Lab X";
        const ApiKeyType keyType = ApiKeyType.Secret;
        // Expires 10 days and a few hours from "now" — Ceiling rounds up to 11.
        DateTimeOffset expiresAt = time.GetUtcNow().AddDays(10).AddHours(3);
        ApiKeyExpiringSoonEto evt = new(keyId, keyName, keyType, expiresAt, TenantId: null);

        await ApiKeyExpiringSoonNotificationHandler.HandleAsync(evt, publisher, time, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            ApiKeyExpiringSoonNotificationType.Instance,
            Arg.Is<ApiKeyExpiringSoonNotificationData>(d =>
                d.KeyId == keyId
                && d.KeyName == keyName
                && d.KeyType == keyType
                && d.ExpiresAt == expiresAt
                && d.DaysUntilExpiry == 11),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ClampsDaysUntilExpiryAtZero_WhenKeyIsAlreadyPastExpiration()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        FakeTimeProvider time = new(DateTimeOffset.Parse("2026-04-27T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        ApiKeyExpiringSoonEto evt = new(
            Guid.NewGuid(),
            "Stale key",
            ApiKeyType.Secret,
            ExpiresAt: time.GetUtcNow().AddHours(-1),
            TenantId: null);

        await ApiKeyExpiringSoonNotificationHandler.HandleAsync(evt, publisher, time, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            ApiKeyExpiringSoonNotificationType.Instance,
            Arg.Is<ApiKeyExpiringSoonNotificationData>(d => d.DaysUntilExpiry == 0),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Defence in depth — the notification data record must NOT expose any field
    /// that could leak secret material. Mirror of the redaction pin used by
    /// <c>ApiKeyIssuedNotificationHandlerTests</c> and
    /// <c>ApiKeyRotatedNotificationHandlerTests</c>.
    /// </summary>
    [Fact]
    public void NotificationData_DoesNotExposeAnySecretMaterial()
    {
        Type dataType = typeof(ApiKeyExpiringSoonNotificationData);

        IEnumerable<string> propertyNames = dataType
            .GetProperties()
            .Select(p => p.Name);

        propertyNames.ShouldNotContain(name => ContainsSecretMarker(name));
    }

    /// <summary>
    /// FU-5 specifically widens the redaction contract: the prefix is also kept
    /// out of the Eto and the data record. Pin it explicitly so a future change
    /// re-introducing <c>Prefix</c> trips this assertion.
    /// </summary>
    [Fact]
    public void NotificationData_DoesNotExposeKeyPrefix()
    {
        Type dataType = typeof(ApiKeyExpiringSoonNotificationData);

        IEnumerable<string> propertyNames = dataType
            .GetProperties()
            .Select(p => p.Name);

        propertyNames.ShouldNotContain(
            name => name.Equals("Prefix", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Same redaction pin against the originating Eto — keep its surface aligned
    /// with the data record so the bridge can never accidentally widen it.
    /// </summary>
    [Fact]
    public void Eto_DoesNotExposeAnySecretMaterial()
    {
        Type etoType = typeof(ApiKeyExpiringSoonEto);

        IEnumerable<string> propertyNames = etoType
            .GetProperties()
            .Select(p => p.Name);

        propertyNames.ShouldNotContain(name => ContainsSecretMarker(name));
        propertyNames.ShouldNotContain(
            name => name.Equals("Prefix", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsSecretMarker(string fieldName) =>
        fieldName.Contains("Hash", StringComparison.OrdinalIgnoreCase)
        || fieldName.Contains("Secret", StringComparison.OrdinalIgnoreCase)
        || fieldName.Equals("Value", StringComparison.OrdinalIgnoreCase)
        || fieldName.Contains("RawKey", StringComparison.OrdinalIgnoreCase);
}
