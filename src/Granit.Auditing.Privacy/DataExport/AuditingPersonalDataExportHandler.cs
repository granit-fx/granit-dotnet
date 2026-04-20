using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport.Events;

namespace Granit.Auditing.Privacy.DataExport;

/// <summary>
/// Wolverine handler that bridges <see cref="PersonalDataRequestedEto"/> to the
/// <see cref="AuditingPrivacyDataProvider"/> via <see cref="PrivacyFragmentUploader"/>.
/// </summary>
public class AuditingPersonalDataExportHandler
{
    public static Task HandleAsync(
        PersonalDataRequestedEto request,
        AuditingPrivacyDataProvider provider,
        PrivacyFragmentUploader uploader,
        CancellationToken cancellationToken) =>
        uploader.UploadAsync(request, provider, cancellationToken);
}
