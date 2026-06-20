using Granit.AI.Chat.Attachments;
using Granit.AI.Chat.BlobStorage.Extensions;
using Granit.BlobStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Chat.BlobStorage.Tests;

/// <summary>
/// Guards the DI wiring, not just the declaration: an architecture test confirms the module is
/// well-formed, but only resolving the container proves <see cref="IAIAttachmentSource"/> binds to
/// <see cref="BlobStorageAIAttachmentSource"/> rather than silently falling back to the Null default.
/// </summary>
public sealed class AIChatBlobStorageRegistrationTests
{
    private static ServiceProvider BuildProvider(Action<IServiceCollection>? arrange = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IBlobContentReader>());
        arrange?.Invoke(services);
        services.AddBlobStorageChatAttachments();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddBlobStorageChatAttachments_ResolvesBlobStorageSource()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        IAIAttachmentSource source = scope.ServiceProvider.GetRequiredService<IAIAttachmentSource>();

        source.ShouldBeOfType<BlobStorageAIAttachmentSource>();
    }

    [Fact]
    public void AddBlobStorageChatAttachments_OverridesPreRegisteredDefault()
    {
        // Mirrors the real module graph: GranitAIChatModule runs first and TryAdds a default source,
        // then GranitAIChatBlobStorageModule registers BlobStorage. The override must win — a
        // regression to TryAddScoped (or a module-ordering flip) would silently drop attachments.
        using ServiceProvider provider = BuildProvider(services =>
            services.TryAddScoped<IAIAttachmentSource, StubDefaultAttachmentSource>());
        using IServiceScope scope = provider.CreateScope();

        IAIAttachmentSource source = scope.ServiceProvider.GetRequiredService<IAIAttachmentSource>();

        source.ShouldBeOfType<BlobStorageAIAttachmentSource>();
    }

    private sealed class StubDefaultAttachmentSource : IAIAttachmentSource
    {
        public Task<AIAttachmentData?> GetAsync(string reference, CancellationToken cancellationToken = default) =>
            Task.FromResult<AIAttachmentData?>(null);
    }
}
