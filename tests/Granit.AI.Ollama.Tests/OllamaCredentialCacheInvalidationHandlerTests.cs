using Granit.AI.Ollama.Handlers;
using Granit.AI.Ollama.Internal;
using Granit.AI.Tenancy;
using Granit.Settings.Events;
using OllamaSharp;
using Shouldly;

namespace Granit.AI.Ollama.Tests;

public sealed class OllamaCredentialCacheInvalidationHandlerTests
{
    private const string Endpoint = "http://localhost:11434";
    private const string Model = "llama3.2";

    private static OllamaClientCache NewCache() =>
        new(new TestHttpClientFactory(), Microsoft.Extensions.Options.Options.Create(TestFixtures.DefaultOptions()));

    private static SettingChangedEvent Changed(string settingName) =>
        new(settingName, "G", ProviderKey: null, OldValue: "old", NewValue: "new", DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task HandleAsync_OllamaEndpointSetting_FlushesCache()
    {
        OllamaClientCache cache = NewCache();
        OllamaApiClient first = cache.GetOrCreate(Endpoint, Model, AIProviderCredentialScope.Host);
        cache.GetOrCreate(Endpoint, Model, AIProviderCredentialScope.Host).ShouldBeSameAs(first, "precondition: same key returns the cached client");

        OllamaCredentialCacheInvalidationHandler handler = new(cache);
        await handler.HandleAsync(Changed(AISettingNames.Ollama.Endpoint), TestContext.Current.CancellationToken);

        cache.GetOrCreate(Endpoint, Model, AIProviderCredentialScope.Host).ShouldNotBeSameAs(first, "cache was flushed, so the client is rebuilt");
    }

    [Fact]
    public async Task HandleAsync_UnrelatedSetting_LeavesCacheIntact()
    {
        OllamaClientCache cache = NewCache();
        OllamaApiClient first = cache.GetOrCreate(Endpoint, Model, AIProviderCredentialScope.Host);

        OllamaCredentialCacheInvalidationHandler handler = new(cache);
        await handler.HandleAsync(Changed("App.Theme"), TestContext.Current.CancellationToken);

        cache.GetOrCreate(Endpoint, Model, AIProviderCredentialScope.Host).ShouldBeSameAs(first, "a non-Ollama setting must not evict cached clients");
    }

    [Fact]
    public async Task HandleAsync_NullEvent_Throws() =>
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await new OllamaCredentialCacheInvalidationHandler(NewCache())
                .HandleAsync(null!, TestContext.Current.CancellationToken));
}
