using Granit.AI.Chat.Attachments;
using Granit.AI.Chat.Internal;
using Granit.AI.Chat.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.AI.Chat.Extensions;

/// <summary>
/// Application-facing registration for the Granit chat attachment seam.
/// </summary>
public static class AIChatAttachmentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the application's <see cref="IAIAttachmentSource"/> so attachments on a turn are
    /// resolved to bytes (over the app's transient blob store) and injected as untrusted context.
    /// </summary>
    /// <typeparam name="TSource">The application attachment source, resolved per scope.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional override of the attachment limits.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddGranitChatAttachments<TSource>(
        this IServiceCollection services,
        Action<GranitAIChatAttachmentOptions>? configure = null)
        where TSource : class, IAIAttachmentSource
    {
        ArgumentNullException.ThrowIfNull(services);

        AddCoreServices(services);
        services.AddScoped<IAIAttachmentSource, TSource>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }

    internal static void AddCoreServices(IServiceCollection services)
    {
        services.AddOptions<GranitAIChatAttachmentOptions>()
            .BindConfiguration(GranitAIChatAttachmentOptions.SectionName);

        services.TryAddScoped<IAIAttachmentSource, NullAIAttachmentSource>();
        services.TryAddScoped<IAIAttachmentTextResolver, AIAttachmentTextResolver>();
    }
}
