using Granit.Authentication.ApiKeys.Events;
using Granit.Authentication.ApiKeys.Notifications.Handlers;
using Granit.Notifications.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Notifications.Tests.Handlers;

public sealed class ApiKeyIssuedNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesIssuedNotification_ToSubscribers()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var keyId = Guid.NewGuid();
        const string keyName = "Partner Lab X";
        const ApiKeyType keyType = ApiKeyType.Secret;
        ApiKeyCreatedEto evt = new(keyId, keyName, keyType);

        await ApiKeyIssuedNotificationHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            ApiKeyIssuedNotificationType.Instance,
            Arg.Is<ApiKeyIssuedNotificationData>(d =>
                d.KeyId == keyId
                && d.KeyName == keyName
                && d.KeyType == keyType),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Defence in depth — even though <see cref="ApiKeyCreatedEto"/> intentionally does
    /// not carry the raw key or its hash, this test pins the contract: no field on the
    /// notification data record is named <c>Hash</c>, <c>Secret</c>, <c>Value</c>, or
    /// <c>RawKey</c>. If a future refactor adds one of those fields, this test fails.
    /// </summary>
    [Fact]
    public void NotificationData_DoesNotExposeAnySecretMaterial()
    {
        Type dataType = typeof(ApiKeyIssuedNotificationData);

        IEnumerable<string> propertyNames = dataType
            .GetProperties()
            .Select(p => p.Name);

        propertyNames.ShouldNotContain(name => ContainsSecretMarker(name));
    }

    private static bool ContainsSecretMarker(string fieldName) =>
        fieldName.Contains("Hash", StringComparison.OrdinalIgnoreCase)
        || fieldName.Contains("Secret", StringComparison.OrdinalIgnoreCase)
        || fieldName.Equals("Value", StringComparison.OrdinalIgnoreCase)
        || fieldName.Contains("RawKey", StringComparison.OrdinalIgnoreCase);
}
