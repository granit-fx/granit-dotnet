using Granit.Notifications.Privacy.DataExport;
using Granit.Privacy;
using Granit.Privacy.BlobStorage.Extensions;

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
    /// The matching Wolverine handler is discovered automatically. This call also wires the
    /// <c>Granit.Privacy.BlobStorage</c> staging infrastructure the provider depends on
    /// (<c>IStagedFragmentBuilder</c>, uploader, assembler) — registration is idempotent
    /// (<c>TryAdd</c>), so the host no longer needs an explicit
    /// <c>[DependsOn(GranitPrivacyBlobStorageModule)]</c>. The provider still requires
    /// <c>GranitNotificationsModule</c> (for the notification readers) to be loaded.
    /// </remarks>
    public static GranitPrivacyBuilder AddGranitNotificationsPrivacyProvider(
        this GranitPrivacyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddGranitPrivacyBlobStorage();
        return builder.AddDataProvider<NotificationsPrivacyDataProvider>();
    }
}
