using Granit.Notifications.MobilePush.Privacy.DataExport;
using Granit.Privacy;
using Granit.Privacy.BlobStorage.Extensions;

namespace Granit.Notifications.MobilePush.Privacy.Extensions;

/// <summary>Registers the mobile push device tokens privacy provider with the privacy framework.</summary>
public static class PrivacyBuilderMobilePushExtensions
{
    /// <summary>
    /// Adds the Art. 15 export provider (the Art. 17 deletion handler is discovered by
    /// Wolverine from this assembly). The deletion saga will wait for this provider's
    /// acknowledgement once registered.
    /// </summary>
    public static GranitPrivacyBuilder AddGranitMobilePushPrivacyProvider(this GranitPrivacyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddGranitPrivacyBlobStorage();
        return builder.AddDataProvider<MobilePushPrivacyDataProvider>();
    }
}
