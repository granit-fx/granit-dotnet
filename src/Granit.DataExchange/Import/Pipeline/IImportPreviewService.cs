using Granit.DataExchange.Import.Mapping;

namespace Granit.DataExchange.Import.Pipeline;

/// <summary>
/// Extracts file headers, preview rows, and mapping suggestions for an import job,
/// then transitions the job to <see cref="Domain.ImportJobStatus.Previewed"/>.
/// </summary>
public interface IImportPreviewService
{
    /// <summary>
    /// Generates a preview for the given import job: headers, sample rows, field metadata,
    /// and AI-assisted mapping suggestions.
    /// </summary>
    /// <param name="jobId">The import job identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The preview result, or <c>null</c> if the job or its definition/parser cannot be found.</returns>
    Task<ImportPreviewResult?> PreviewAsync(Guid jobId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an import preview operation.
/// </summary>
public sealed record ImportPreviewResult(
    IReadOnlyList<string> Headers,
    IReadOnlyList<string[]> PreviewRows,
    IReadOnlyList<ImportColumnMapping> Suggestions,
    IReadOnlyList<ImportFieldMetadata> FieldMetadata);
