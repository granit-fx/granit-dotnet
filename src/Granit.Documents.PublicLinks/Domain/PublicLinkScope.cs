namespace Granit.Documents.PublicLinks.Domain;

/// <summary>Authorisation scope granted to the bearer of a public link.</summary>
public enum PublicLinkScope
{
    /// <summary>The bearer may download the binary content.</summary>
    Download = 0,

    /// <summary>The bearer may view (inline render) the content but not download.</summary>
    View = 1,
}
