using Granit.Authentication.ApiKeys.BackgroundJobs.Services;
using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Events;
using Granit.Authentication.ApiKeys.Options;
using Granit.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.BackgroundJobs.Tests.Services;

public sealed class ExpiringApiKeyScannerServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 27, 7, 0, 0, TimeSpan.Zero);

    private readonly IApiKeyAdminStore _adminStore = Substitute.For<IApiKeyAdminStore>();
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly ApiKeysOptions _options = new() { ExpirationLeadTimeDays = 14 };

    private ExpiringApiKeyScannerService CreateService() =>
        new(_adminStore,
            _eventBus,
            _time,
            Microsoft.Extensions.Options.Options.Create(_options),
            NullLogger<ExpiringApiKeyScannerService>.Instance);

    [Fact]
    public async Task ExecuteAsync_when_no_expiring_keys_should_return_without_publishing()
    {
        _adminStore.ListExpiringSoonAsync(
            Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns([]);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Any<ApiKeyExpiringSoonEto>(),
            Arg.Any<CancellationToken>());
        await _adminStore.DidNotReceive().SaveAsync(
            Arg.Any<ApiKeyEntry>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_should_publish_eto_then_stamp_notified_at_for_each_match()
    {
        ApiKeyEntry entry = CreateEntryExpiringIn(days: 10);

        _adminStore.ListExpiringSoonAsync(
            Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns([entry]);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<ApiKeyExpiringSoonEto>(e =>
                e.KeyId == entry.Id
                && e.KeyName == entry.Name
                && e.KeyType == entry.Type
                && e.ExpiresAt == entry.ExpiresAt!.Value),
            Arg.Any<CancellationToken>());

        await _adminStore.Received(1).SaveAsync(entry, Arg.Any<CancellationToken>());
        entry.LastExpirationNotifiedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task ExecuteAsync_should_pass_lead_time_window_and_dedupe_cutoff_to_store()
    {
        _options.ExpirationLeadTimeDays = 30;
        DateTimeOffset capturedNow = default;
        DateTimeOffset capturedWindow = default;
        DateTimeOffset capturedDedupe = default;

        _adminStore
            .ListExpiringSoonAsync(
                Arg.Do<DateTimeOffset>(n => capturedNow = n),
                Arg.Do<DateTimeOffset>(w => capturedWindow = w),
                Arg.Do<DateTimeOffset>(d => capturedDedupe = d),
                Arg.Any<CancellationToken>())
            .Returns([]);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        capturedNow.ShouldBe(Now);
        capturedWindow.ShouldBe(Now.AddDays(30));
        capturedDedupe.ShouldBe(Now - ExpiringApiKeyScannerService.DedupeWindow);
    }

    [Fact]
    public async Task ExecuteAsync_should_skip_entries_whose_expires_at_is_null_defensively()
    {
        var entry = ApiKeyEntry.Create(
            Guid.NewGuid(), "Eternal", ApiKeyType.Secret, "test",
            "hash", "gk_test_sk_", "abcd");
        // ExpiresAt stays null — query shouldn't return it but we defend anyway.

        _adminStore.ListExpiringSoonAsync(
            Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns([entry]);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Any<ApiKeyExpiringSoonEto>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The Eto MUST NOT carry the on-the-wire prefix, hash, or raw value. Even if a
    /// future change introduces such a property on the entity, this assertion fails
    /// fast.
    /// </summary>
    [Fact]
    public void Eto_carries_only_public_safe_metadata()
    {
        Type t = typeof(ApiKeyExpiringSoonEto);
        IEnumerable<string> propNames = t.GetProperties().Select(p => p.Name);

        propNames.ShouldNotContain(name => name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
        propNames.ShouldNotContain(name => name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
        propNames.ShouldNotContain(name => name.Equals("Value", StringComparison.OrdinalIgnoreCase));
        propNames.ShouldNotContain(name => name.Contains("RawKey", StringComparison.OrdinalIgnoreCase));
        propNames.ShouldNotContain(name => name.Equals("Prefix", StringComparison.OrdinalIgnoreCase));
    }

    private ApiKeyEntry CreateEntryExpiringIn(int days)
    {
        var entry = ApiKeyEntry.Create(
            Guid.NewGuid(),
            "Partner Lab X",
            ApiKeyType.Secret,
            "test",
            hashedKey: "hash",
            prefix: "gk_test_sk_",
            lastFourChars: "abcd");
        entry.SetExpiration(_time.GetUtcNow().AddDays(days));
        return entry;
    }
}
