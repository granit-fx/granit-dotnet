using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport.Events;

namespace Granit.Notifications.Privacy.DataExport;

/// <summary>
/// Wolverine handler that bridges <see cref="PersonalDataRequestedEto"/> to the
/// <see cref="NotificationsPrivacyDataProvider"/> via <see cref="PrivacyFragmentUploader"/>.
/// </summary>
public class NotificationsPersonalDataExportHandler
{
    public static Task HandleAsync(
        PersonalDataRequestedEto request,
        NotificationsPrivacyDataProvider provider,
        PrivacyFragmentUploader uploader,
        CancellationToken cancellationToken) =>
        uploader.UploadAsync(request, provider, cancellationToken);
}
