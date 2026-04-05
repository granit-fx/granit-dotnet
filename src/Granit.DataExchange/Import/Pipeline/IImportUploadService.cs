namespace Granit.DataExchange.Import.Pipeline;

/// <summary>
/// Validates and stores an uploaded file, then creates an <see cref="Domain.ImportJob"/>.
/// </summary>
public interface IImportUploadService
{
    /// <summary>
    /// Validates the file against the import definition constraints, stores it, and creates an import job.
    /// </summary>
    /// <param name="fileName">Original file name.</param>
    /// <param name="contentType">MIME type of the file.</param>
    /// <param name="fileSize">File size in bytes.</param>
    /// <param name="fileStream">The file content stream.</param>
    /// <param name="definitionName">Name of the import definition to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Either a successful result with the job, or an error with a detail message.</returns>
    Task<ImportUploadResult> UploadAsync(
        string fileName,
        string contentType,
        long fileSize,
        Stream fileStream,
        string definitionName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an import file upload operation.
/// </summary>
public sealed record ImportUploadResult
{
    private ImportUploadResult() { }

    /// <summary>The created import job (null when failed).</summary>
    public Domain.ImportJob? Job { get; private init; }

    /// <summary>Error detail message (null when succeeded).</summary>
    public string? ErrorDetail { get; private init; }

    /// <summary>Whether the upload succeeded.</summary>
    public bool Succeeded => Job is not null;

    /// <summary>Creates a successful result.</summary>
    public static ImportUploadResult Success(Domain.ImportJob job) => new() { Job = job };

    /// <summary>Creates a failed result.</summary>
    public static ImportUploadResult Failure(string detail) => new() { ErrorDetail = detail };
}
