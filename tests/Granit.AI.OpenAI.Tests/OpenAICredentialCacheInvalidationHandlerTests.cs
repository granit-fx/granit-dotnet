using Granit.AI.OpenAI.Handlers;
using Granit.AI.OpenAI.Internal;
using Granit.AI.Tenancy;
using Granit.Settings.Events;
using OpenAI;
using Shouldly;

namespace Granit.AI.OpenAI.Tests;

public sealed class OpenAICredentialCacheInvalidationHandlerTests
{
    private const string ApiKey = "sk-openai-key";

    private static OpenAIClientCache NewCache() =>
        new(new TestHttpClientFactory(), Microsoft.Extensions.Options.Options.Create(TestFixtures.DefaultOptions()));

    private static SettingChangedEvent Changed(string settingName) =>
        new(settingName, "G", ProviderKey: null, OldValue: "old", NewValue: "new", DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task HandleAsync_OpenAICredentialSetting_FlushesCache()
    {
        OpenAIClientCache cache = NewCache();
        OpenAIClient first = cache.GetOrCreate(ApiKey, endpoint: null);
        cache.GetOrCreate(ApiKey, endpoint: null).ShouldBeSameAs(first, "precondition: same key returns the cached client");

        OpenAICredentialCacheInvalidationHandler handler = new(cache);
        await handler.HandleAsync(Changed(AISettingNames.OpenAI.ApiKey), TestContext.Current.CancellationToken);

        cache.GetOrCreate(ApiKey, endpoint: null).ShouldNotBeSameAs(first, "cache was flushed, so the client is rebuilt");
    }

    [Fact]
    public async Task HandleAsync_AzureOpenAISetting_LeavesCacheIntact()
    {
        // Guards the prefix overlap: "Granit.AI.AzureOpenAI." must not match "Granit.AI.OpenAI.".
        OpenAIClientCache cache = NewCache();
        OpenAIClient first = cache.GetOrCreate(ApiKey, endpoint: null);

        OpenAICredentialCacheInvalidationHandler handler = new(cache);
        await handler.HandleAsync(Changed(AISettingNames.AzureOpenAI.ApiKey), TestContext.Current.CancellationToken);

        cache.GetOrCreate(ApiKey, endpoint: null).ShouldBeSameAs(first, "an AzureOpenAI setting must not evict OpenAI clients");
    }

    [Fact]
    public async Task HandleAsync_NullEvent_Throws() =>
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await new OpenAICredentialCacheInvalidationHandler(NewCache())
                .HandleAsync(null!, TestContext.Current.CancellationToken));
}
