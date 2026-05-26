namespace Granit.Privacy.DataExport.Sanitization;

/// <summary>
/// Thrown by <see cref="EntryPathSanitizer.Sanitize"/> when an
/// <see cref="Fragments.ExportFragment.EntryPath"/> fails validation. Closes <c>VULN-004</c>
/// (zip-slip via attacker-controlled path) by failing fast at the provider boundary.
/// </summary>
public sealed class InvalidExportEntryPathException(string entryPath, string reason)
    : Exception($"Invalid export entry path '{entryPath}': {reason}")
{
    /// <summary>Raw path that failed validation.</summary>
    public string EntryPath { get; } = entryPath;

    /// <summary>Sanitizer rule that triggered the rejection.</summary>
    public string Reason { get; } = reason;
}
