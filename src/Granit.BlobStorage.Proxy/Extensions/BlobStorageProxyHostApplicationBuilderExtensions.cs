using System.Diagnostics.CodeAnalysis;
using Granit.BlobStorage.Internal;
using Granit.BlobStorage.Proxy.Diagnostics;
using Granit.BlobStorage.Proxy.Internal;
using Granit.BlobStorage.Proxy.Options;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.Proxy.Extensions;

/// <summary>
/// Extension methods for registering the blob storage proxy provider.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class BlobStorageProxyHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <c>Granit.BlobStorage.Proxy</c> services: token store, proxy URL provider,
    /// and the <see cref="IBlobStorage"/> orchestrator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The proxy does <b>not</b> register <see cref="IBlobStoreProvider"/> or
    /// <see cref="IBlobKeyStrategy"/>. The concrete storage provider (FileSystem, Database)
    /// must register these separately.
    /// </para>
    /// <para>
    /// Reads <see cref="ProxyBlobOptions"/> from the <c>"BlobStorage:Proxy"</c> configuration section.
    /// An <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> implementation
    /// must be registered by the application (e.g. <c>AddDistributedMemoryCache()</c> or Redis).
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitBlobStorageProxy(
        this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(BlobStorageProxyActivitySource.Name);

        builder.Services
            .AddOptions<ProxyBlobOptions>()
            .BindConfiguration(ProxyBlobOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<ProxyBlobOptions>, ProxyBlobOptionsValidator>();

        builder.Services.TryAddSingleton<IBlobProxyTokenStore, DistributedBlobProxyTokenStore>();
        builder.Services.TryAddScoped<IPresignedUrlProvider, ProxyPresignedUrlProvider>();
        builder.Services.TryAddScoped<IBlobStorage, DefaultBlobStorage>();

        return builder;
    }
}
