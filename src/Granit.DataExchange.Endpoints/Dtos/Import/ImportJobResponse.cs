using Granit.DataExchange.Import.Domain;

namespace Granit.DataExchange.Endpoints.Dtos.Import;

/// <summary>
/// Response DTO for an import job summary.
/// </summary>
/// <param name="Id">Unique job identifier.</param>
/// <param name="DefinitionName">Logical import definition name.</param>
/// <param name="EntityTypeName">Name of the target entity type being imported (e.g. <c>Patient</c>).</param>
/// <param name="OriginalFileName">Original uploaded file name.</param>
/// <param name="MimeType">MIME type of the uploaded file.</param>
/// <param name="FileSizeBytes">File size in bytes.</param>
/// <param name="Status">Current job status.</param>
/// <param name="CreatedAt">UTC timestamp of creation.</param>
/// <param name="CompletedAt">UTC timestamp of completion; <c>null</c> while in progress.</param>
/// <param name="ModifiedAt">UTC timestamp of the last state transition; <c>null</c> if never modified.</param>
/// <param name="ModifiedBy">Identity that last modified the job; <c>null</c> if never modified.</param>
/// <param name="ConcurrencyStamp">Opaque optimistic-concurrency token. Pass back in update requests to detect concurrent modifications (HTTP 409).</param>
public sealed record ImportJobResponse(
    Guid Id,
    string DefinitionName,
    string EntityTypeName,
    string OriginalFileName,
    string MimeType,
    long FileSizeBytes,
    ImportJobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ModifiedAt,
    string? ModifiedBy,
    string ConcurrencyStamp)
{
    /// <summary>
    /// Maps an <see cref="ImportJob"/> domain entity to a response DTO.
    /// </summary>
    internal static ImportJobResponse FromJob(ImportJob job) =>
        new(job.Id, job.DefinitionName, job.EntityTypeName, job.OriginalFileName, job.MimeType,
            job.FileSizeBytes, job.Status, job.CreatedAt, job.CompletedAt, job.ModifiedAt, job.ModifiedBy, job.ConcurrencyStamp);
}
