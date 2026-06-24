using System.Diagnostics.CodeAnalysis;
using Granit.Validation.AI.Internal;
using Granit.Validation.AI.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Validation.AI.Extensions;

/// <summary>
/// Extension methods for registering AI content moderation services.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ValidationAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds AI-powered content moderation to the application.
    /// </summary>
    /// <remarks>
    /// Binds <see cref="ValidationAIOptions"/> from the <c>Validation:AI</c> configuration section
    /// and registers <see cref="IAIContentModerator"/> as a scoped service backed by an LLM.
    /// Requires an AI provider to be registered (e.g. <c>AddGranitAIOpenAI()</c>).
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitValidationAI(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<ValidationAIOptions>()
            .BindConfiguration(ValidationAIOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.TryAddScoped<IAIContentModerator, LlmContentModerator>();

        return builder;
    }
}
