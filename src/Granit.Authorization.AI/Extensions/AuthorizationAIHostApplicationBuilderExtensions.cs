using System.Diagnostics.CodeAnalysis;
using Granit.Authorization.AI.Internal;
using Granit.Authorization.AI.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Authorization.AI.Extensions;

/// <summary>
/// Extension methods for registering AI access anomaly detection services.
/// </summary>
[ExcludeFromCodeCoverage]
public static class AuthorizationAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds AI-powered access anomaly detection to the application.
    /// </summary>
    /// <remarks>
    /// Binds <see cref="AuthorizationAIOptions"/> from the <c>AI:Authorization</c> configuration section
    /// and registers <see cref="IAIAccessAnomalyDetector"/> as a scoped service backed by an LLM.
    /// Requires an AI provider to be registered (e.g. <c>AddGranitAIOpenAI()</c>).
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitAuthorizationAI(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<AuthorizationAIOptions>()
            .BindConfiguration(AuthorizationAIOptions.SectionName);

        builder.Services.TryAddScoped<IAIAccessAnomalyDetector, LlmAccessAnomalyDetector>();

        return builder;
    }
}
