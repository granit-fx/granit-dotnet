using Granit.Domain.ValueObjects;
using Granit.MultiTenancy;
using Granit.Notifications.MobilePush.Domain;
using Granit.Notifications.MobilePush.Privacy.DataExport;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.Privacy.Tests.DataExport;

public sealed class MobilePushPrivacyDataProviderTests
{
    private static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IMobilePushTokenReader _reader = Substitute.For<IMobilePushTokenReader>();
    private readonly IStagedFragmentBuilder _fragmentBuilder = Substitute.For<IStagedFragmentBuilder>();

    private MobilePushPrivacyDataProvider CreateSut() =>
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
            EntryPath = "mobile-push-tokens.json",
            ContentType = "application/json",
            IntegrityTag = "v1:stub",
            StagedBlob = BlobReference.Create(Guid.NewGuid().ToString()),
        };

    [Fact]
    public async Task HasData_TokensExist_ReturnsTrue()
    {
        _reader.GetTokensAsync(User.ToString(), null, Arg.Any<CancellationToken>())
            .Returns([MobilePushToken.Create(User.ToString(), "device-token-abcd1234", "hash", MobilePlatform.Android)]);

        (await CreateSut().HasDataAsync(Context(), TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task HasData_NoTokens_ReturnsFalse()
    {
        _reader.GetTokensAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        (await CreateSut().HasDataAsync(Context(), TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Export_MasksTheDeviceToken_PlaintextNeverLeaves()
    {
        _reader.GetTokensAsync(User.ToString(), null, Arg.Any<CancellationToken>())
            .Returns([MobilePushToken.Create(User.ToString(), "device-token-abcd1234", "hash", MobilePlatform.Android)]);

        MobilePushExportFragment? captured = null;
        _fragmentBuilder.BuildJsonAsync(
                Arg.Any<PrivacyExportContext>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Do<MobilePushExportFragment>(o => captured = o), Arg.Any<CancellationToken>())
            .Returns(StubFragment());

        await foreach (ExportFragment _ in CreateSut().ExportAsync(Context(), TestContext.Current.CancellationToken))
        {
            // Intentionally empty: drain the async stream so the export pipeline runs and
            // the fragment builder captures the wire shape asserted below.
        }

        captured.ShouldNotBeNull();
        MobilePushExportFragment fragment = captured;
        MobilePushTokenFragment token = fragment.Tokens.ShouldHaveSingleItem();
        token.DeviceTokenPreview.ShouldBe("…1234");
        token.DeviceTokenPreview.ShouldNotContain("device-token");
        token.Platform.ShouldBe("Android");
    }

    [Fact]
    public async Task Export_NoTokens_YieldsNothing()
    {
        _reader.GetTokensAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        List<object> fragments = [];
        await foreach (ExportFragment f in CreateSut().ExportAsync(Context(), TestContext.Current.CancellationToken))
        {
            fragments.Add(f);
        }

        fragments.ShouldBeEmpty();
    }
}
