using Anthropic;
using Granit.AI.Anthropic.Handlers;
using Granit.AI.Anthropic.Internal;
using Granit.AI.Tenancy;
using Granit.Settings.Events;
using Shouldly;

namespace Granit.AI.Anthropic.Tests;

public sealed class AnthropicCredentialCacheInvalidationHandlerTests
{
    private const string ApiKey = "sk-ant-key";

    private static AnthropicClientCache NewCache() =>
        new(new TestHttpClientFactory(), Microsoft.Extensions.Options.Options.Create(TestFixtures.DefaultOptions()));

    private static SettingChangedEvent Changed(string settingName) =>
        new(settingName, "G", ProviderKey: null, OldValue: "old", NewValue: "new", DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task HandleAsync_AnthropicCredentialSetting_FlushesCache()
    {
        AnthropicClientCache cache = NewCache();
        AnthropicClient first = cache.GetOrCreate(ApiKey);
        cache.GetOrCreate(ApiKey).ShouldBeSameAs(first, "precondition: same key returns the cached client");

        AnthropicCredentialCacheInvalidationHandler handler = new(cache);
        await handler.HandleAsync(Changed(AISettingNames.Anthropic.ApiKey), TestContext.Current.CancellationToken);

        cache.GetOrCreate(ApiKey).ShouldNotBeSameAs(first, "cache was flushed, so the client is rebuilt");
    }

    [Fact]
    public async Task HandleAsync_UnrelatedSetting_LeavesCacheIntact()
    {
        AnthropicClientCache cache = NewCache();
        AnthropicClient first = cache.GetOrCreate(ApiKey);

        AnthropicCredentialCacheInvalidationHandler handler = new(cache);
        await handler.HandleAsync(Changed("App.Theme"), TestContext.Current.CancellationToken);

        cache.GetOrCreate(ApiKey).ShouldBeSameAs(first, "a non-Anthropic setting must not evict cached clients");
    }

    [Fact]
    public async Task HandleAsync_NullEvent_Throws() =>
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await new AnthropicCredentialCacheInvalidationHandler(NewCache())
                .HandleAsync(null!, TestContext.Current.CancellationToken));
}
