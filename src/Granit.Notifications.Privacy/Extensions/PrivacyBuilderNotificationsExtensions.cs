using Granit.Notifications.Privacy.DataExport;
using Granit.Privacy;

namespace Granit.Notifications.Privacy.Extensions;

/// <summary>
/// <see cref="GranitPrivacyBuilder"/> extensions that register the notifications privacy provider.
/// </summary>
public static class PrivacyBuilderNotificationsExtensions
{
    /// <summary>
    /// Registers <see cref="NotificationsPrivacyDataProvider"/> and adds <c>"notifications"</c>
    /// to the scatter-gather registry.
    /// </summary>
    /// <remarks>
    /// The matching Wolverine handler is discovered automatically. Requires
    /// <see cref="GranitNotificationsPrivacyModule"/> to be loaded.
    /// </remarks>
    public static GranitPrivacyBuilder AddGranitNotificationsPrivacyProvider(
        this GranitPrivacyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddDataProvider<NotificationsPrivacyDataProvider>();
    }
}
