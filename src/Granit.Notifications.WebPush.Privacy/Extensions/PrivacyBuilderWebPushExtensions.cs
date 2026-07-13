using Granit.Notifications.WebPush.Privacy.DataExport;
using Granit.Privacy;
using Granit.Privacy.BlobStorage.Extensions;

namespace Granit.Notifications.WebPush.Privacy.Extensions;

/// <summary>Registers the browser Web Push subscriptions privacy provider with the privacy framework.</summary>
public static class PrivacyBuilderWebPushExtensions
{
    /// <summary>
    /// Adds the Art. 15 export provider (the Art. 17 deletion handler is discovered by
    /// Wolverine from this assembly). The deletion saga will wait for this provider's
    /// acknowledgement once registered.
    /// </summary>
    public static GranitPrivacyBuilder AddGranitWebPushPrivacyProvider(this GranitPrivacyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddGranitPrivacyBlobStorage();
        return builder.AddDataProvider<WebPushPrivacyDataProvider>();
    }
}
