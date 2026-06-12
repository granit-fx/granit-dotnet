using System.Text;
using System.Text.RegularExpressions;
using Granit.Privacy.Exceptions;

namespace Granit.Privacy.DataExport.Sanitization;

/// <summary>
/// Validates and normalizes <see cref="Fragments.ExportFragment.EntryPath"/> values before
/// they reach the ZIP archive. Guards against zip-slip — a malicious provider or compromised
/// source data cannot inject a path that escapes the extraction directory on the recipient's
/// machine.
/// </summary>
/// <remarks>
/// <para>
/// Rules (rejection):
/// <list type="bullet">
///   <item><c>..</c> path segments (parent traversal).</item>
///   <item>Absolute paths (leading <c>/</c> or <c>\</c>).</item>
///   <item>Windows drive letters (e.g. <c>C:</c>).</item>
///   <item>NUL, CR, LF, and other control characters (&lt; 0x20).</item>
///   <item>Windows reserved names — <c>CON</c>, <c>PRN</c>, <c>AUX</c>, <c>NUL</c>,
///   <c>COM[0-9]</c>, <c>LPT[0-9]</c> — case-insensitive, with or without extension.</item>
/// </list>
/// </para>
/// <para>
/// Rules (normalization, applied before reject checks):
/// <list type="bullet">
///   <item>Backslashes converted to forward slashes.</item>
///   <item>Consecutive slashes collapsed (<c>"a//b"</c> → <c>"a/b"</c>).</item>
///   <item>Trailing slash stripped.</item>
///   <item>Trailing dots/spaces stripped from each segment (Windows compatibility).</item>
/// </list>
/// </para>
/// <para>
/// Limits (rejection on overflow):
/// <list type="bullet">
///   <item>Each segment ≤ 255 bytes UTF-8 (NTFS / ext4 single-component limit).</item>
///   <item>Full path ≤ 4096 bytes UTF-8 (typical filesystem PATH_MAX).</item>
/// </list>
/// </para>
/// </remarks>
public static partial class EntryPathSanitizer
{
    /// <summary>Maximum bytes for a single path segment (NTFS / ext4 limit).</summary>
    public const int MaxSegmentBytes = 255;

    /// <summary>Maximum bytes for the full path.</summary>
    public const int MaxFullPathBytes = 4096;

    /// <summary>
    /// Returns a normalized, validated path safe to use as a ZIP entry name.
    /// Throws <see cref="InvalidExportEntryPathException"/> on rejection.
    /// </summary>
    public static string Sanitize(string entryPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(entryPath);

        // Normalize first so subsequent checks operate on canonical form.
        string normalized = Normalize(entryPath);

        if (string.IsNullOrEmpty(normalized))
        {
            throw new InvalidExportEntryPathException(entryPath, "path is empty after normalization");
        }

        // Absolute path / drive letter rejection.
        if (normalized.StartsWith('/'))
        {
            throw new InvalidExportEntryPathException(entryPath, "absolute paths are not allowed");
        }

        if (HasDriveLetter(normalized))
        {
            throw new InvalidExportEntryPathException(entryPath, "Windows drive letters are not allowed");
        }

        // Control characters anywhere in the string.
        if (normalized.Cast<char?>().FirstOrDefault(c => c < 0x20 || c == 0x7F) is { } control)
        {
            throw new InvalidExportEntryPathException(
                entryPath, $"control character 0x{(int)control:X2} not allowed");
        }

        ValidateSegments(entryPath, normalized);

        int fullByteLength = Encoding.UTF8.GetByteCount(normalized);
        if (fullByteLength > MaxFullPathBytes)
        {
            throw new InvalidExportEntryPathException(
                entryPath,
                $"path exceeds {MaxFullPathBytes} bytes ({fullByteLength})");
        }

        return normalized;
    }

    private static void ValidateSegments(string entryPath, string normalized)
    {
        foreach (string segment in normalized.Split('/'))
        {
            if (segment.Length == 0)
            {
                throw new InvalidExportEntryPathException(entryPath, "empty path segment");
            }

            if (segment == "..")
            {
                throw new InvalidExportEntryPathException(entryPath, "parent-directory segment '..' not allowed");
            }

            if (segment == ".")
            {
                throw new InvalidExportEntryPathException(entryPath, "current-directory segment '.' not allowed");
            }

            int segmentByteLength = Encoding.UTF8.GetByteCount(segment);
            if (segmentByteLength > MaxSegmentBytes)
            {
                throw new InvalidExportEntryPathException(
                    entryPath,
                    $"segment '{segment}' exceeds {MaxSegmentBytes} bytes ({segmentByteLength})");
            }

            if (IsWindowsReservedName(segment))
            {
                throw new InvalidExportEntryPathException(
                    entryPath,
                    $"segment '{segment}' uses a Windows reserved name");
            }
        }
    }

    private static string Normalize(string entryPath)
    {
        // Convert backslashes to forward slashes (ZIP entries are always /-separated).
        string converted = entryPath.Replace('\\', '/');

        // Collapse consecutive slashes.
        converted = CollapseSlashesRegex().Replace(converted, "/");

        // Strip trailing slash.
        if (converted.Length > 1 && converted[^1] == '/')
        {
            converted = converted[..^1];
        }

        // Per-segment trailing dot/space strip (Windows rejects those when unpacking).
        // Segments that are entirely '.'s (e.g. "..", ".") are left alone so the
        // parent-traversal / current-dir checks downstream can still flag them — trimming
        // would silently rewrite ".." into "" and let "a/../b" collapse to "a//b".
        string[] segments = converted.Split('/');
        char[] trailingChars = ['.', ' '];
        bool anyTrim = false;
        for (int i = 0; i < segments.Length; i++)
        {
            string trimmed = segments[i].TrimEnd(trailingChars);
            if (trimmed.Length > 0 && trimmed.Length != segments[i].Length)
            {
                segments[i] = trimmed;
                anyTrim = true;
            }
        }

        return anyTrim ? string.Join('/', segments) : converted;
    }

    private static bool HasDriveLetter(string path)
    {
        // Match patterns like "C:", "C:/", "C:foo".
        if (path.Length < 2)
        {
            return false;
        }

        char first = path[0];
        return path[1] == ':'
            && ((first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z'));
    }

    private static bool IsWindowsReservedName(string segment)
    {
        // Strip extension for the comparison.
        int dotIndex = segment.IndexOf('.', StringComparison.Ordinal);
        ReadOnlySpan<char> stem = dotIndex < 0 ? segment.AsSpan() : segment.AsSpan(0, dotIndex);

        return WindowsReservedNamesRegex().IsMatch(stem);
    }

    [GeneratedRegex("/{2,}")]
    private static partial Regex CollapseSlashesRegex();

    [GeneratedRegex("^(?:CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WindowsReservedNamesRegex();
}
