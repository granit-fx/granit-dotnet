namespace Granit.Privacy.BlobStorage.DataExport;

/// <summary>
/// <c>manifest.json</c> entry inside the assembled personal-data export ZIP.
/// Documents the request metadata and the per-provider fragment layout so the
/// downstream consumer (support team, auditor, data subject tooling) can correlate
/// the archive with the original request.
/// </summary>
/// <param name="RequestId">Correlation id of the export request.</param>
/// <param name="UserId">Data subject whose data was exported.</param>
/// <param name="Regulation">Regulation code (e.g. <c>EU_GDPR</c>, <c>BR_LGPD</c>).</param>
/// <param name="RequestedAt">When the request was recorded.</param>
/// <param name="CompletedAt">When the archive was assembled.</param>
/// <param name="IsPartial"><c>true</c> if the saga timed out.</param>
/// <param name="MissingProviders">Providers that never produced a fragment.</param>
/// <param name="EmptyProviders">Providers that produced the <c>empty:</c> sentinel (no user data).</param>
/// <param name="Fragments">One entry per fragment file present in the ZIP.</param>
public sealed record ExportManifest(
    Guid RequestId,
    Guid UserId,
    string Regulation,
    DateTimeOffset RequestedAt,
    DateTimeOffset CompletedAt,
    bool IsPartial,
    IReadOnlyList<string> MissingProviders,
    IReadOnlyList<string> EmptyProviders,
    IReadOnlyList<ExportManifestFragment> Fragments);

/// <summary>Describes a single fragment included in the archive.</summary>
/// <param name="ProviderName">Name of the data provider.</param>
/// <param name="FileName">ZIP entry path for the fragment.</param>
/// <param name="ContentType">Fragment MIME type (copied from <c>PersonalDataPreparedEto.ContentType</c>).</param>
/// <param name="BlobReferenceId">The original blob id used to fetch the fragment.</param>
public sealed record ExportManifestFragment(
    string ProviderName,
    string FileName,
    string ContentType,
    string BlobReferenceId);
