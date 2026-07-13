using Granit.Domain.ValueObjects;
using Granit.MultiTenancy;
using Granit.Notifications.WebPush.Privacy.DataExport;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Privacy.Tests.DataExport;

public sealed class WebPushPrivacyDataProviderTests
{
    private static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IWebPushSubscriptionReader _reader = Substitute.For<IWebPushSubscriptionReader>();
    private readonly IStagedFragmentBuilder _fragmentBuilder = Substitute.For<IStagedFragmentBuilder>();

    private WebPushPrivacyDataProvider CreateSut() =>
        new(_reader, Substitute.For<ICurrentTenant>(), _fragmentBuilder);

    private static PrivacyExportContext Context() =>
        new(
            RequestId: Guid.NewGuid(),
            SubjectUserId: User,
            CallerUserId: User,
            TenantId: null,
            Regulation: "EU_GDPR");

    private static StagedExportFragment StubFragment() =>
        new()
        {
            EntryPath = "web-push-subscriptions.json",
            ContentType = "application/json",
            IntegrityTag = "v1:stub",
            StagedBlob = BlobReference.Create(Guid.NewGuid().ToString()),
        };

    private static WebPushSubscriptionInfo Sub() => new()
    {
        Endpoint = "https://fcm.googleapis.com/fcm/send/secret-capability-path",
        P256dh = "p256dh-key-material",
        Auth = "auth-secret",
        ExpirationTime = 123L,
    };

    [Fact]
    public async Task HasData_SubscriptionsExist_ReturnsTrue()
    {
        _reader.GetSubscriptionsAsync(User.ToString(), null, Arg.Any<CancellationToken>())
            .Returns([Sub()]);

        (await CreateSut().HasDataAsync(Context(), TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task Export_MasksEndpoint_AndNeverExportsKeyMaterial()
    {
        _reader.GetSubscriptionsAsync(User.ToString(), null, Arg.Any<CancellationToken>())
            .Returns([Sub()]);

        WebPushExportFragment? captured = null;
        _fragmentBuilder.BuildJsonAsync(
                Arg.Any<PrivacyExportContext>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Do<WebPushExportFragment>(o => captured = o), Arg.Any<CancellationToken>())
            .Returns(StubFragment());

        await foreach (ExportFragment _ in CreateSut().ExportAsync(Context(), TestContext.Current.CancellationToken))
        {
            // Intentionally empty: drain the async stream so the export pipeline runs and
            // the fragment builder captures the wire shape asserted below.
        }

        captured.ShouldNotBeNull();
        WebPushSubscriptionFragment sub = captured.Subscriptions.ShouldHaveSingleItem();
        sub.EndpointOrigin.ShouldBe("https://fcm.googleapis.com/…");
        sub.EndpointOrigin.ShouldNotContain("secret-capability-path");
        // The wire shape has no key-material members at all — pin it structurally.
        typeof(WebPushSubscriptionFragment).GetProperties()
            .Select(pi => pi.Name)
            .ShouldBe(["EndpointOrigin", "ExpirationTime"], ignoreOrder: true);
    }

    [Fact]
    public async Task Export_NoSubscriptions_YieldsNothing()
    {
        _reader.GetSubscriptionsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        List<object> fragments = [];
        await foreach (ExportFragment f in CreateSut().ExportAsync(Context(), TestContext.Current.CancellationToken))
        {
            fragments.Add(f);
        }

        fragments.ShouldBeEmpty();
    }
}
