using Granit.Notifications.MobilePush.Domain;
using Granit.Notifications.MobilePush.Extensions;
using Granit.Notifications.MobilePush.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.Tests;

public sealed class MobilePushNotificationsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsMobilePush_ResolvesReaderAndWriterToSameInstance()
    {
        using ServiceProvider sp = BuildProvider();

        IMobilePushTokenReader reader = sp.GetRequiredService<IMobilePushTokenReader>();
        IMobilePushTokenWriter writer = sp.GetRequiredService<IMobilePushTokenWriter>();

        // The in-memory store keeps its tokens in an instance-level dictionary. Registering the two
        // interfaces against the impl type independently would hand out two stores → the round-trip
        // below silently returns nothing. Guard the shared-instance wiring explicitly.
        reader.ShouldBeSameAs(writer);
    }

    [Fact]
    public async Task AddGranitNotificationsMobilePush_TokenWrittenViaWriter_IsVisibleViaReader()
    {
        await using ServiceProvider sp = BuildProvider();

        IMobilePushTokenReader reader = sp.GetRequiredService<IMobilePushTokenReader>();
        IMobilePushTokenWriter writer = sp.GetRequiredService<IMobilePushTokenWriter>();

        await writer.RegisterAsync(
            "user-1", "device-token-1", MobilePlatform.Android, tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<MobilePushToken> tokens =
            await reader.GetTokensAsync("user-1", null, TestContext.Current.CancellationToken);

        tokens.ShouldHaveSingleItem();
        tokens[0].DeviceToken.ShouldBe("device-token-1");
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        // Pre-register a stub hasher so the real HMAC hasher (which fails fast without a configured
        // pepper) is not constructed. TryAdd inside the extension respects this registration.
        services.AddSingleton<IMobilePushTokenHasher, StubHasher>();
        services.AddGranitNotificationsMobilePush();

        return services.BuildServiceProvider();
    }

    /// <summary>Identity hasher — uniqueness is the only contract the in-memory store relies on.</summary>
    private sealed class StubHasher : IMobilePushTokenHasher
    {
        public string? ComputeHash(string? deviceToken) =>
            string.IsNullOrEmpty(deviceToken) ? null : $"hash:{deviceToken}";
    }
}
