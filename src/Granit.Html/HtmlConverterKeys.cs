namespace Granit.Html;

/// <summary>
/// Keyed-services identifiers for the two posture profiles of
/// <see cref="IHtmlToPlainTextConverter"/>.
/// </summary>
/// <remarks>
/// Consumers MUST inject via <c>[FromKeyedServices(HtmlConverterKeys.Trusted)]</c> or
/// <c>[FromKeyedServices(HtmlConverterKeys.Untrusted)]</c> — the choice encodes the SSRF
/// posture explicitly at the call site, removing the historical "default profile is
/// whatever the provider picked" ambiguity.
/// </remarks>
public static class HtmlConverterKeys
{
    /// <summary>
    /// Profile for host-owned HTML (e.g. Granit-rendered email templates). May enable
    /// external resource resolution if the provider chooses to.
    /// </summary>
    public const string Trusted = "trusted";

    /// <summary>
    /// Profile for HTML that did not originate from the host (user uploads, indexed
    /// documents, incoming email bodies). Providers MUST NOT resolve external resources
    /// (SSRF guard).
    /// </summary>
    public const string Untrusted = "untrusted";
}
