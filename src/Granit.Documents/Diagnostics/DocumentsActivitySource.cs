using System.Diagnostics;

namespace Granit.Documents.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Documents distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class DocumentsActivitySource
{
    /// <summary>The name of the Granit.Documents <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Documents";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────────────────────────────────────────────────────
    // Operations populate as the corresponding stories land. Phase-1 baseline:
    internal const string DocumentUpload = "documents.upload";
    internal const string DocumentDownload = "documents.download";
    internal const string DocumentShareGrant = "documents.share.grant";
    internal const string AclResolve = "documents.acl.resolve";
}
