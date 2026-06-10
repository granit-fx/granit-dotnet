using Azure.AI.OpenAI;
using Granit.AI.AzureOpenAI.Handlers;
using Granit.AI.AzureOpenAI.Internal;
using Granit.AI.Tenancy;
using Granit.Settings.Events;
using Shouldly;

namespace Granit.AI.AzureOpenAI.Tests;

public sealed class AzureOpenAICredentialCacheInvalidationHandlerTests
{
    private const string ApiKey = "azure-key";
    private const string Endpoint = "https://example.openai.azure.com";

    private static AzureOpenAIClientCache NewCache() =>
        new(new TestHttpClientFactory(), Microsoft.Extensions.Options.Options.Create(TestFixtures.DefaultOptions()));

    private static SettingChangedEvent Changed(string settingName) =>
        new(settingName, "G", ProviderKey: null, OldValue: "old", NewValue: "new", DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task HandleAsync_AzureOpenAICredentialSetting_FlushesCache()
    {
        AzureOpenAIClientCache cache = NewCache();
        AzureOpenAIClient first = cache.GetOrCreate(ApiKey, Endpoint);
        cache.GetOrCreate(ApiKey, Endpoint).ShouldBeSameAs(first, "precondition: same key returns the cached client");

        AzureOpenAICredentialCacheInvalidationHandler handler = new(cache);
        await handler.HandleAsync(Changed(AISettingNames.AzureOpenAI.ApiKey), TestContext.Current.CancellationToken);

        cache.GetOrCreate(ApiKey, Endpoint).ShouldNotBeSameAs(first, "cache was flushed, so the client is rebuilt");
    }

    [Fact]
    public async Task HandleAsync_OpenAISetting_LeavesCacheIntact()
    {
        // Guards the prefix overlap: "Granit.AI.OpenAI." must not match "Granit.AI.AzureOpenAI.".
        AzureOpenAIClientCache cache = NewCache();
        AzureOpenAIClient first = cache.GetOrCreate(ApiKey, Endpoint);

        AzureOpenAICredentialCacheInvalidationHandler handler = new(cache);
        await handler.HandleAsync(Changed(AISettingNames.OpenAI.ApiKey), TestContext.Current.CancellationToken);

        cache.GetOrCreate(ApiKey, Endpoint).ShouldBeSameAs(first, "an OpenAI setting must not evict Azure OpenAI clients");
    }

    [Fact]
    public async Task HandleAsync_NullEvent_Throws() =>
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await new AzureOpenAICredentialCacheInvalidationHandler(NewCache())
                .HandleAsync(null!, TestContext.Current.CancellationToken));
}
