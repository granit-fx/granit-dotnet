using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport.Events;

namespace Granit.AI.Prompts.Privacy.DataExport;

/// <summary>
/// Wolverine handler that bridges <see cref="PersonalDataRequestedEto"/> to the
/// <see cref="PromptTemplatePrivacyDataProvider"/> via <see cref="PrivacyFragmentUploader"/>.
/// Discovered automatically by assembly scanning.
/// </summary>
public class AIPromptsPersonalDataExportHandler
{
    public static Task HandleAsync(
        PersonalDataRequestedEto request,
        PromptTemplatePrivacyDataProvider provider,
        PrivacyFragmentUploader uploader,
        CancellationToken cancellationToken) =>
        uploader.UploadAsync(request, provider, cancellationToken);
}
