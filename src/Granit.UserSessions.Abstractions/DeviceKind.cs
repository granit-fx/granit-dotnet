namespace Granit.UserSessions;

/// <summary>
/// The kind of client a session or device represents. Determined by the <strong>authentication
/// context</strong> — the OIDC client's declared kind, with a heuristic fallback (redirect-URI scheme,
/// grant type) — not by User-Agent parsing, which only yields a form factor. The auth flow, the
/// stability of the device's identity, and how it is labelled all differ by kind.
/// </summary>
public enum DeviceKind
{
    /// <summary>Unknown or not yet classified.</summary>
    Unknown = 0,

    /// <summary>A web browser or single-page app — a cookie or BFF session. Identity is ephemeral (a cookie).</summary>
    Browser,

    /// <summary>A browser extension — its own OIDC client and <c>chrome-extension://</c>/<c>moz-extension://</c> redirect, distinct from a web-page session.</summary>
    BrowserExtension,

    /// <summary>A native smartphone or tablet application (authorization code + PKCE public client).</summary>
    MobileApp,

    /// <summary>A native desktop application.</summary>
    DesktopApp,

    /// <summary>A wrist-worn companion device (smartwatch) — a native app on a constrained wearable OS.</summary>
    Wearable,

    /// <summary>A TV, set-top box or other input-constrained device (device authorization grant, RFC 8628).</summary>
    Tv,

    /// <summary>An embedded physical device — IoT, point-of-sale, industrial — typically authenticating via mTLS, MQTT or a device flow rather than as a backend service.</summary>
    Embedded,

    /// <summary>A CLI, service or machine client (client credentials or API key).</summary>
    ApiClient,
}
