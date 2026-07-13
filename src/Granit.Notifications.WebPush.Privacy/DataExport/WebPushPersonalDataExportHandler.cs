using System.Diagnostics.CodeAnalysis;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport.Events;

namespace Granit.Notifications.WebPush.Privacy.DataExport;

/// <summary>
/// Wolverine handler that bridges <see cref="PersonalDataRequestedEto"/> to the
/// <see cref="WebPushPrivacyDataProvider"/> via <see cref="PrivacyFragmentUploader"/>.
/// </summary>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class WebPushPersonalDataExportHandler
{
    public static Task HandleAsync(
        PersonalDataRequestedEto request,
        WebPushPrivacyDataProvider provider,
        PrivacyFragmentUploader uploader,
        CancellationToken cancellationToken) =>
        uploader.UploadAsync(request, provider, cancellationToken);
}
