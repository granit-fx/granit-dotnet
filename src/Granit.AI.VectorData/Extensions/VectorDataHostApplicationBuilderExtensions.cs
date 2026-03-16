using System.Diagnostics.CodeAnalysis;
using Granit.AI.VectorData.Internal;
using Granit.AI.VectorData.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.AI.VectorData.Extensions;

/// <summary>
/// Extension methods for registering Granit.AI.VectorData services.
/// </summary>
[ExcludeFromCodeCoverage]
public static class VectorDataHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit AI vector data services, including <see cref="ISemanticSearchService"/>.
    /// </summary>
    /// <remarks>
    /// A concrete <see cref="IVectorCollectionFactory"/> implementation must be registered
    /// by a provider package (e.g. <c>Granit.AI.VectorData.PgVector</c>).
    /// An <see cref="IEmbeddingGeneratorFactory"/> must also be registered
    /// by an AI provider package.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The host application builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitAIVectorData(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<VectorDataOptions>()
            .BindConfiguration(VectorDataOptions.SectionName);

        builder.Services.TryAddSingleton<ISemanticSearchService, DefaultSemanticSearchService>();

        return builder;
    }
}
