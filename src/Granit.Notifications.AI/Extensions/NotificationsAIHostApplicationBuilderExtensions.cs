using System.Diagnostics.CodeAnalysis;
using Granit.Notifications.AI.Internal;
using Granit.Notifications.AI.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.Notifications.AI.Extensions;

/// <summary>
/// Extension methods for registering AI notification content generation and channel selection services.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class NotificationsAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <c>Granit.Notifications.AI</c> services: AI-powered notification content generation
    /// via <see cref="IAINotificationContentGenerator"/> and smart channel routing via
    /// <see cref="IAIChannelSelector"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads <see cref="NotificationsAIOptions"/> from the <c>"AI:Notifications"</c> configuration section.
    /// Requires <c>Granit.AI</c> core services (<c>AddGranitAI()</c>) and at least one AI provider
    /// to be registered beforehand.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitNotificationsAI(
        this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<NotificationsAIOptions>()
            .BindConfiguration(NotificationsAIOptions.SectionName);

        // Scoped, not singleton: both depend on the scoped IStructuredCompletion primitive
        // (ADR-064), so a singleton here would capture a stale scope.
        builder.Services.AddScoped<IAINotificationContentGenerator, LlmNotificationContentGenerator>();
        builder.Services.AddScoped<IAIChannelSelector, LlmChannelSelector>();

        return builder;
    }
}
