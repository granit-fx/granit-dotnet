using Granit.Bundle.Essentials;
using Granit.Caching.StackExchangeRedis;
using Granit.Http.ApiDocumentation;
using Granit.Http.ApiVersioning;
using Granit.Http.Hosting;
using Granit.Http.Idempotency;
using Granit.Localization;
using Granit.Modularity;

namespace Granit.Bundle.Api;

/// <summary>
/// Extension methods on <see cref="GranitBuilder"/> for adding the Api bundle.
/// </summary>
public static class GranitBuilderApiExtensions
{
    /// <summary>
    /// Adds the Api bundle: Essentials + ApiVersioning, ApiDocumentation,
    /// Hosting (CORS + compression), Idempotency, Localization, Caching + Redis.
    /// </summary>
    public static GranitBuilder AddApi(this GranitBuilder builder)
    {
        builder.AddEssentials();
        builder.AddModule<GranitHttpApiVersioningModule>();
        builder.AddModule<GranitHttpApiDocumentationModule>();
        builder.AddModule<GranitHttpHostingModule>();
        builder.AddModule<GranitHttpIdempotencyModule>();
        builder.AddModule<GranitLocalizationModule>();
        builder.AddModule<GranitCachingStackExchangeRedisModule>();
        return builder;
    }
}
