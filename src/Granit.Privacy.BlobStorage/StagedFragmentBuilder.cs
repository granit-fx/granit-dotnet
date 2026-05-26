using System.Net.Http.Headers;
using System.Text.Json;
using Granit.BlobStorage;
using Granit.Domain.ValueObjects;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;
using Granit.Privacy.DataExport.Sanitization;
using Granit.Privacy.DataExport.Security;
using Granit.Privacy.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.BlobStorage;

/// <summary>
/// Default <see cref="IStagedFragmentBuilder"/> — serialises the DTO as JSON UTF-8, uploads
/// the bytes to the staging container via the existing presigned-PUT dance, signs the
/// integrity tag, and returns a fully-formed <see cref="StagedExportFragment"/>.
/// </summary>
public sealed partial class StagedFragmentBuilder(
    IBlobStorage blobStorage,
    IHttpClientFactory httpClientFactory,
    IExportHmacSigner hmacSigner,
    IOptions<GranitPrivacyOptions> options,
    TimeProvider timeProvider,
    ILogger<StagedFragmentBuilder> logger) : IStagedFragmentBuilder
{
    /// <summary>Named <see cref="HttpClient"/> used for the staging PUT.</summary>
    public const string HttpClientName = "Granit.Privacy.FragmentUpload";

    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    /// <inheritdoc />
    public async Task<StagedExportFragment> BuildJsonAsync<T>(
        PrivacyExportContext context,
        string providerName,
        string entryPath,
        T dto,
        CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(entryPath);
        ArgumentNullException.ThrowIfNull(dto);

        string safeEntryPath = EntryPathSanitizer.Sanitize(entryPath);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(dto, ExportJsonOptions);

        PresignedUploadTicket ticket = await blobStorage.InitiateUploadAsync(
            PrivacyExportContainerNames.FragmentContainer,
            new BlobUploadRequest(
                FileName: safeEntryPath,
                ContentType: "application/json",
                MaxAllowedBytes: payload.Length),
            cancellationToken).ConfigureAwait(false);

        HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName);
        using ByteArrayContent content = new(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
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

        LogStagedFragmentUploaded(logger, providerName, context.SubjectUserId, context.RequestId, ticket.BlobId, payload.Length);

        DateTimeOffset expiresAt = timeProvider.GetUtcNow()
            + TimeSpan.FromMinutes(options.Value.ExportTimeoutMinutes * 4);
        string integrityTag = hmacSigner.Sign(new ExportHmacParameters(
            RequestId: context.RequestId,
            SubjectUserId: context.SubjectUserId,
            ProviderName: providerName,
            FragmentKind: "staged",
            SourceContainer: PrivacyExportContainerNames.FragmentContainer,
            SourceBlobId: ticket.BlobId,
            EntryPath: safeEntryPath,
            ExpiresAt: expiresAt));

        var stagedBlob = BlobReference.Create(ticket.BlobId.ToString());

        return new StagedExportFragment
        {
            EntryPath = safeEntryPath,
            ContentType = "application/json",
            KnownSizeBytes = payload.Length,
            IntegrityTag = integrityTag,
            StagedBlob = stagedBlob,
        };
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Privacy export: staged JSON fragment for provider {Provider} (user {UserId}, request {RequestId}) uploaded as blob {BlobId} ({Bytes} bytes)")]
    private static partial void LogStagedFragmentUploaded(ILogger logger, string provider, Guid userId, Guid requestId, Guid blobId, int bytes);
}
