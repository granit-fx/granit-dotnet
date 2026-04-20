using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport.Events;

namespace Granit.Identity.Federated.Privacy.DataExport;

/// <summary>
/// Wolverine handler that bridges <see cref="PersonalDataRequestedEto"/> to the
/// <see cref="IdentityFederatedPrivacyDataProvider"/> via <see cref="PrivacyFragmentUploader"/>.
/// </summary>
public class IdentityFederatedPersonalDataExportHandler
{
    public static Task HandleAsync(
        PersonalDataRequestedEto request,
        IdentityFederatedPrivacyDataProvider provider,
        PrivacyFragmentUploader uploader,
        CancellationToken cancellationToken) =>
        uploader.UploadAsync(request, provider, cancellationToken);
}
