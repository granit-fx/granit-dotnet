namespace Granit.UserSessions.Notifications.Internal;

/// <summary>
/// Produces a short, human-readable "Browser on OS" label (e.g. <c>"Chrome on Windows"</c>,
/// <c>"Safari on iPhone"</c>) from a raw User-Agent string, for display in the sign-in alert email.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately coarse and self-contained: a handful of common browser and OS tokens, no version numbers, no
/// device model, no third-party UA-parsing dependency. This is a courtesy label for a security email, not a
/// fingerprint — keeping it coarse is also the privacy-conscious choice.
/// </para>
/// <para>
/// Token order matters: several browsers embed each other's names in their UA (Edge and Opera both contain
/// "Chrome"; Chrome contains "Safari"; "CriOS"/"FxiOS" are Chrome/Firefox on iOS atop the WebKit engine), so the
/// more specific token is checked first. The OS-token approach mirrors the coarse
/// <c>DeviceFingerprint.Family</c> in <c>Granit.UserSessions.AnomalyDetection</c> (which is internal to that
/// module, hence re-implemented here).
/// </para>
/// </remarks>
internal static class UserAgentDescriptor
{
    /// <summary>
    /// Maps <paramref name="userAgent"/> to a short "Browser on OS" label. Degrades gracefully:
    /// returns just the OS when the browser is unrecognised, just the browser when the OS is unrecognised,
    /// and <see langword="null"/> when neither can be derived (or the input is blank).
    /// </summary>
    public static string? Describe(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        string? browser = Browser(userAgent);
        string? os = OperatingSystem(userAgent);

        return (browser, os) switch
        {
            ({ } b, { } o) => $"{b} on {o}",
            ({ } b, null) => b,
            (null, { } o) => o,
            _ => null,
        };
    }

    private static string? Browser(string ua)
    {
        // Developer / API clients.
        if (Has(ua, "PostmanRuntime"))
        {
            return "Postman";
        }

        if (Has(ua, "curl/"))
        {
            return "cURL";
        }

        // Edge ("Edg/") and Opera ("OPR/") both carry "Chrome"; check them first.
        if (Has(ua, "Edg/") || Has(ua, "Edge/") || Has(ua, "EdgiOS/") || Has(ua, "EdgA/"))
        {
            return "Edge";
        }

        if (Has(ua, "OPR/") || Has(ua, "Opera"))
        {
            return "Opera";
        }

        if (Has(ua, "SamsungBrowser"))
        {
            return "Samsung Internet";
        }

        // Firefox on iOS reports "FxiOS"; desktop/Android report "Firefox".
        if (Has(ua, "Firefox") || Has(ua, "FxiOS"))
        {
            return "Firefox";
        }

        // Chrome on iOS reports "CriOS"; everywhere else "Chrome" (which also appears in WebView UAs).
        if (Has(ua, "Chrome") || Has(ua, "CriOS"))
        {
            return "Chrome";
        }

        // Safari embeds "Safari" but so does Chrome — only reached once Chrome is ruled out above.
        return Has(ua, "Safari") ? "Safari" : null;
    }

    private static string? OperatingSystem(string ua)
    {
        if (Has(ua, "iPhone"))
        {
            return "iPhone";
        }

        if (Has(ua, "iPad"))
        {
            return "iPad";
        }

        if (Has(ua, "Android"))
        {
            return "Android";
        }

        if (Has(ua, "Windows"))
        {
            return "Windows";
        }

        if (Has(ua, "Macintosh") || Has(ua, "Mac OS"))
        {
            return "macOS";
        }

        // CrOS (ChromeOS) also contains "Linux"; surface the more specific name first.
        if (Has(ua, "CrOS"))
        {
            return "ChromeOS";
        }

        return Has(ua, "Linux") ? "Linux" : null;
    }

    private static bool Has(string value, string token) =>
        value.Contains(token, StringComparison.OrdinalIgnoreCase);
}
