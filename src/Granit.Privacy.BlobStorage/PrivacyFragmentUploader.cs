using System.Net.Http.Headers;
using Granit.BlobStorage;
using Granit.Events;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Events;
using Microsoft.Extensions.Logging;

namespace Granit.Privacy.BlobStorage;

/// <summary>
/// Reusable utility that drives the scatter-gather provider-side upload:
/// <list type="number">
///   <item>Asks the provider for its bytes.</item>
///   <item>If empty, publishes a <see cref="PersonalDataPreparedEto"/> with the
///   <see cref="PrivacyExportContainerNames.EmptyFragmentPrefix"/> sentinel and returns —
///   no blob is created for empty providers.</item>
///   <item>Otherwise runs the presigned upload dance
///   (<see cref="IBlobStorage.InitiateUploadAsync"/> → HTTP PUT → <see cref="IBlobStorage.ConfirmUploadAsync"/>)
///   and publishes a <see cref="PersonalDataPreparedEto"/> carrying the confirmed blob id.</item>
/// </list>
/// </summary>
/// <remarks>
/// Every provider-side Wolverine handler forwards to <see cref="UploadAsync"/>. Handlers stay
/// one-liners and the boilerplate lives here, where it can be unit-tested in isolation.
/// </remarks>
public sealed partial class PrivacyFragmentUploader(
    IBlobStorage blobStorage,
    IHttpClientFactory httpClientFactory,
    IDistributedEventBus eventBus,
    ILogger<PrivacyFragmentUploader> logger)
{
    /// <summary>Named <see cref="HttpClient"/> used to PUT fragment bytes to the presigned URL.</summary>
    public const string HttpClientName = "Granit.Privacy.FragmentUpload";

    /// <summary>
    /// Executes the upload-and-publish flow for a single provider.
    /// </summary>
    public async Task UploadAsync<TProvider>(
        PersonalDataRequestedEto request,
        TProvider provider,
        CancellationToken cancellationToken)
        where TProvider : class, IPrivacyDataProvider
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(provider);

        ReadOnlyMemory<byte> payload = await provider
            .ExportAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (payload.IsEmpty)
        {
            LogEmptyFragment(logger, TProvider.ProviderName, request.UserId, request.RequestId);

            await eventBus.PublishAsync(
                new PersonalDataPreparedEto(
                    request.RequestId,
                    TProvider.ProviderName,
                    $"{PrivacyExportContainerNames.EmptyFragmentPrefix}{request.RequestId}",
                    TProvider.ContentType),
                cancellationToken).ConfigureAwait(false);

            return;
        }

        PresignedUploadTicket ticket = await blobStorage.InitiateUploadAsync(
            PrivacyExportContainerNames.FragmentContainer,
            new BlobUploadRequest(
                FileName: TProvider.FileName(request.RequestId),
                ContentType: TProvider.ContentType,
                MaxAllowedBytes: payload.Length),
            cancellationToken).ConfigureAwait(false);

        HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName);
        using ByteArrayContent content = new(payload.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue(TProvider.ContentType);
        foreach ((string key, string value) in ticket.RequiredHeaders)
        {
            content.Headers.TryAddWithoutValidation(key, value);
        }

        using HttpRequestMessage httpRequest = new(new HttpMethod(ticket.HttpMethod), ticket.UploadUrl)
        {
            Content = content,
        };
        using HttpResponseMessage response = await httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await blobStorage.ConfirmUploadAsync(
            PrivacyExportContainerNames.FragmentContainer,
            ticket.BlobId,
            cancellationToken).ConfigureAwait(false);

        LogFragmentUploaded(logger, TProvider.ProviderName, request.UserId, request.RequestId, ticket.BlobId, payload.Length);

        await eventBus.PublishAsync(
            new PersonalDataPreparedEto(
                request.RequestId,
                TProvider.ProviderName,
                ticket.BlobId.ToString(),
                TProvider.ContentType),
            cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Privacy export: provider {Provider} has no data for user {UserId} (request {RequestId}); emitting empty sentinel")]
    private static partial void LogEmptyFragment(ILogger logger, string provider, Guid userId, Guid requestId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Privacy export: provider {Provider} uploaded {Bytes} bytes for user {UserId} (request {RequestId}), blob {BlobId}")]
    private static partial void LogFragmentUploaded(ILogger logger, string provider, Guid userId, Guid requestId, Guid blobId, int bytes);
}
