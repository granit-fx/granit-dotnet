using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;

namespace Granit.DataExchange.Endpoints.Dtos.Export;

/// <summary>
/// Response DTO for an export job summary.
/// </summary>
public sealed record ExportJobResponse(
    Guid Id,
    string DefinitionName,
    string Format,
    ExportJobStatus Status,
    int? RowCount,
    string? FileName,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ModifiedAt,
    string? ModifiedBy)
{
    /// <summary>
    /// Maps an <see cref="ExportJob"/> domain entity to a response DTO.
    /// </summary>
    internal static ExportJobResponse FromJob(ExportJob job) =>
        new(job.Id, job.DefinitionName, job.Format, job.Status, job.RowCount,
            job.FileName, job.ErrorMessage, job.CreatedAt, job.CompletedAt, job.ModifiedAt, job.ModifiedBy);
}
