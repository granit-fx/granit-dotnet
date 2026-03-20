using Granit.Bundle.Essentials;
using Granit.Caching.FusionCache;
using Granit.Core.Modularity;
using Granit.Http.ApiDocumentation;
using Granit.Http.ApiVersioning;
using Granit.Http.Cors;
using Granit.Http.Idempotency;
using Granit.Localization;

namespace Granit.Bundle.Api;

/// <summary>
/// Extension methods on <see cref="GranitBuilder"/> for adding the Api bundle.
/// </summary>
public static class GranitBuilderApiExtensions
{
    /// <summary>
    /// Adds the Api bundle: Essentials + ApiVersioning, ApiDocumentation,
    /// Cors, Idempotency, Localization, Caching.
    /// </summary>
    public static GranitBuilder AddApi(this GranitBuilder builder)
    {
        builder.AddEssentials();
        builder.AddModule<GranitHttpApiVersioningModule>();
        builder.AddModule<GranitHttpApiDocumentationModule>();
        builder.AddModule<GranitHttpCorsModule>();
        builder.AddModule<GranitIdempotencyModule>();
        builder.AddModule<GranitLocalizationModule>();
        builder.AddModule<GranitCachingFusionCacheModule>();
        return builder;
    }
}
