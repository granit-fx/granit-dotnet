namespace Granit.Notifications.Rendering;

/// <summary>
/// Renders localized notification content from templates — one pipeline shared by every
/// channel instead of each channel hand-building strings.
/// </summary>
/// <remarks>
/// <para>
/// Resolution is two-tier: a template named exactly like the notification type
/// (<c>"privacy.export_ready"</c>) first, then the channel-agnostic
/// <c>"Notifications.Default"</c> fallback. The effective culture is
/// <c>context.Culture ?? recipient?.PreferredCulture</c> (explicit trigger override wins),
/// walking the culture chain down to the neutral template.
/// </para>
/// <para>
/// Returns <see langword="null"/> when templating is not configured, no template resolves,
/// or rendering fails — callers keep their minimal hardcoded fallback. Implementations must
/// never throw for content reasons: a broken template must not break delivery.
/// </para>
/// </remarks>
public interface INotificationContentRenderer
{
    /// <summary>Renders the content for one delivery, or <see langword="null"/> (see remarks).</summary>
    /// <param name="context">The delivery being rendered.</param>
    /// <param name="recipient">Resolved recipient, when the channel has one (culture, display name).</param>
    /// <param name="format">Target shape required by the calling channel.</param>
    /// <param name="extraModelData">
    /// Channel-specific template variables merged into the model (e.g. the email channel's
    /// <c>unsubscribe_url</c>). Existing payload keys are never overwritten.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<RenderedNotificationContent?> RenderAsync(
        NotificationDeliveryContext context,
        RecipientInfo? recipient,
        NotificationContentFormat format,
        IReadOnlyDictionary<string, object?>? extraModelData = null,
        CancellationToken cancellationToken = default);
}
