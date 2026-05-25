using AngleSharp;

namespace Granit.Html.AngleSharp;

/// <summary>
/// Factory for AngleSharp <see cref="IConfiguration"/> instances. Exposes two named
/// profiles so the SSRF posture is encoded explicitly at the call site instead of being
/// inferred from a default.
/// </summary>
/// <remarks>
/// <para>
/// Both profiles return <see cref="Configuration.Default"/> today — neither registers an
/// HTTP requester, so external resources (links, stylesheets, images) are never resolved.
/// The named factories exist so that future profile drift (e.g. attaching a same-origin
/// requester to the trusted profile) lands deterministically on Notifications email
/// templates only, leaving the untrusted ingest path locked down.
/// </para>
/// <para>
/// The architecture test <c>AngleSharpConfigurationTests.Source_must_never_call_WithDefaultLoader</c>
/// pins the SSRF guard at the source level — if either profile ever needs the AngleSharp
/// default loader, the test forces an explicit, documented opt-in.
/// </para>
/// </remarks>
public static class AngleSharpConfiguration
{
    /// <summary>
    /// Profile for host-owned HTML (e.g. Granit-rendered email templates).
    /// Returns <see cref="Configuration.Default"/> — parser only, no loader, no CSS engine,
    /// no JS engine. Consumed by Notifications.Email through the keyed
    /// <c>HtmlConverterKeys.Trusted</c> registration.
    /// </summary>
    public static IConfiguration BuildForTrustedTemplates() => Configuration.Default;

    /// <summary>
    /// Profile for HTML that did not originate from the host (user uploads, indexed
    /// documents, incoming email bodies). Returns <see cref="Configuration.Default"/> —
    /// identical to the trusted profile today, but isolated as its own factory so the SSRF
    /// posture can never silently inherit a future loader added to the trusted profile.
    /// Consumed by TextExtraction extractors through the keyed
    /// <c>HtmlConverterKeys.Untrusted</c> registration.
    /// </summary>
    public static IConfiguration BuildForUntrustedContent() => Configuration.Default;
}
