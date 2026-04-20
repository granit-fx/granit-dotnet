using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport.Events;

namespace Granit.Identity.Local.Privacy.DataExport;

/// <summary>
/// Wolverine handler that bridges <see cref="PersonalDataRequestedEto"/> to the
/// <see cref="IdentityLocalPrivacyDataProvider"/> via <see cref="PrivacyFragmentUploader"/>.
/// </summary>
/// <remarks>
/// Discovered automatically by Wolverine via assembly scanning — no explicit registration
/// needed. Must be a <c>public class</c> (non-static) with a <c>public static Handle*</c>
/// method so Wolverine picks it up without a <c>[WolverineHandler]</c> attribute dependency.
/// </remarks>
public class IdentityLocalPersonalDataExportHandler
{
    public static Task HandleAsync(
        PersonalDataRequestedEto request,
        IdentityLocalPrivacyDataProvider provider,
        PrivacyFragmentUploader uploader,
        CancellationToken cancellationToken) =>
        uploader.UploadAsync(request, provider, cancellationToken);
}
