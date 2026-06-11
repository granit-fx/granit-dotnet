using System.Diagnostics.CodeAnalysis;
using Granit.BlobStorage.AI.Internal;
using Granit.BlobStorage.AI.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.AI.Extensions;

/// <summary>
/// Extension methods for registering AI blob classification services.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class BlobStorageAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <c>Granit.BlobStorage.AI</c> services: AI-powered blob classifier and
    /// the <see cref="IBlobValidator"/> that runs at Order = 100 in the validation pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads <see cref="BlobStorageAIOptions"/> from the <c>"BlobStorage:AI"</c> configuration section.
    /// Requires <c>Granit.AI</c> core services (<c>AddGranitAI()</c>) and at least one AI provider
    /// to be registered beforehand.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitBlobStorageAI(
        this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<BlobStorageAIOptions>()
            .BindConfiguration(BlobStorageAIOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<BlobStorageAIOptions>, BlobStorageAIOptionsValidator>();

        // Single instance serves both IAIBlobClassifier and IBlobValidator.
        builder.Services.AddSingleton<AIBlobClassifierService>();
        builder.Services.AddSingleton<IAIBlobClassifier>(sp => sp.GetRequiredService<AIBlobClassifierService>());
        builder.Services.AddSingleton<IBlobValidator>(sp => sp.GetRequiredService<AIBlobClassifierService>());

        return builder;
    }
}
