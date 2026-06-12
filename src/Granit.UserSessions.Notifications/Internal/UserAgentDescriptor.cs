namespace Granit.UserSessions.Notifications.Internal;

/// <summary>
/// Extracts a coarse browser family and operating-system family from a raw User-Agent string, for display in
/// the sign-in alert email. The two are returned <b>separately</b> (never joined in code) so the email template
/// can lay them out and label them in the recipient's language — a baked-in English connector like "X on Y"
/// would leak English into every localized email.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately coarse and self-contained: a handful of common browser/OS tokens, no version numbers, no device
/// model, no third-party UA-parsing dependency. A courtesy hint for a security email, not a fingerprint.
/// </para>
/// <para>
/// "Browser" is the right frame here because the value is parsed from a User-Agent and the only current source
/// (BFF sessions) is always <c>DeviceKind.Browser</c>. Non-browser clients (mobile/desktop apps, wearables, TVs
/// — arriving later from the OpenIddict/Keycloak sources, typically without a User-Agent) will need a kind-aware
/// label derived from <c>DeviceKind</c> instead.
/// </para>
/// <para>
/// Token order matters: several browsers embed each other's names (Edge and Opera both contain "Chrome"; Chrome
/// contains "Safari"; "CriOS"/"FxiOS" are Chrome/Firefox on iOS), so the more specific token is checked first.
/// </para>
/// </remarks>
internal static class UserAgentDescriptor
{
    /// <summary>Coarse browser family (e.g. <c>"Chrome"</c>), or <see langword="null"/> when unrecognised/blank.</summary>
    public static string? Browser(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        // Developer / API clients.
        if (Has(userAgent, "PostmanRuntime"))
        {
            return "Postman";
        }

        if (Has(userAgent, "curl/"))
        {
            return "cURL";
        }

        // Edge ("Edg/") and Opera ("OPR/") both carry "Chrome"; check them first.
        if (Has(userAgent, "Edg/") || Has(userAgent, "Edge/") || Has(userAgent, "EdgiOS/") || Has(userAgent, "EdgA/"))
        {
            return "Edge";
        }

        if (Has(userAgent, "OPR/") || Has(userAgent, "Opera"))
        {
            return "Opera";
        }

        if (Has(userAgent, "SamsungBrowser"))
        {
            return "Samsung Internet";
        }

        // Firefox on iOS reports "FxiOS"; desktop/Android report "Firefox".
        if (Has(userAgent, "Firefox") || Has(userAgent, "FxiOS"))
        {
            return "Firefox";
        }

        // Chrome on iOS reports "CriOS"; everywhere else "Chrome" (which also appears in WebView UAs).
        if (Has(userAgent, "Chrome") || Has(userAgent, "CriOS"))
        {
            return "Chrome";
        }

        // Safari embeds "Safari" but so does Chrome — only reached once Chrome is ruled out above.
        return Has(userAgent, "Safari") ? "Safari" : null;
    }

    /// <summary>Coarse operating-system family (e.g. <c>"Windows"</c>), or <see langword="null"/> when unrecognised/blank.</summary>
    public static string? OperatingSystem(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        if (Has(userAgent, "iPhone"))
        {
            return "iPhone";
        }

        if (Has(userAgent, "iPad"))
        {
            return "iPad";
        }

        if (Has(userAgent, "Android"))
        {
            return "Android";
        }

        if (Has(userAgent, "Windows"))
        {
            return "Windows";
        }

        if (Has(userAgent, "Macintosh") || Has(userAgent, "Mac OS"))
        {
            return "macOS";
        }

        // CrOS (ChromeOS) also contains "Linux"; surface the more specific name first.
        if (Has(userAgent, "CrOS"))
        {
            return "ChromeOS";
        }

        return Has(userAgent, "Linux") ? "Linux" : null;
    }

    private static bool Has(string value, string token) =>
        value.Contains(token, StringComparison.OrdinalIgnoreCase);
}
