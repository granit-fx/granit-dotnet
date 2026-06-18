using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport.Events;

namespace Granit.AI.Chat.Privacy.DataExport;

/// <summary>
/// Wolverine handler that bridges <see cref="PersonalDataRequestedEto"/> to the
/// <see cref="ConversationPrivacyDataProvider"/> via <see cref="PrivacyFragmentUploader"/>.
/// Discovered automatically by assembly scanning.
/// </summary>
public class AIChatPersonalDataExportHandler
{
    public static Task Handle(
        PersonalDataRequestedEto request,
        ConversationPrivacyDataProvider provider,
        PrivacyFragmentUploader uploader,
        CancellationToken cancellationToken) =>
        uploader.UploadAsync(request, provider, cancellationToken);
}
