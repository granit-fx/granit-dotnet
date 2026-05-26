namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// Optional body for <c>POST /privacy/exports</c>. All fields are optional —
/// posting an empty body (or no body at all) defaults to "everything visible to me".
/// </summary>
/// <param name="Scopes">Subset of provider names the subject wants in the archive.
/// Unknown / hidden entries are silently dropped server-side (VULN-202: no enumeration
/// leak). <see langword="null"/> or empty means "all visible scopes" — Takeout default.</param>
public sealed record PrivacyExportRequest(IReadOnlyList<string>? Scopes = null);
