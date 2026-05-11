using System.Diagnostics;

namespace Granit.Documents.PublicLinks.Diagnostics;

/// <summary>Central <see cref="ActivitySource"/> for <c>Granit.Documents.PublicLinks</c>.</summary>
public static class DocumentsPublicLinksActivitySource
{
    /// <summary>The name of the public-links <see cref="ActivitySource"/>.</summary>
    public const string Name = "Granit.Documents.PublicLinks";

    /// <summary>Singleton <see cref="ActivitySource"/> instance.</summary>
    public static readonly ActivitySource Source = new(Name);

    /// <summary>Span name covering link creation.</summary>
    public const string LinkCreate = "documents.public_links.create";

    /// <summary>Span name covering link revocation.</summary>
    public const string LinkRevoke = "documents.public_links.revoke";

    /// <summary>Span name covering a single redemption.</summary>
    public const string LinkConsume = "documents.public_links.consume";
}
