namespace Granit.Identity.AnomalyDetection.Internal;

/// <summary>Coarse device-family extraction from a User-Agent, stable across browser/OS version changes.</summary>
internal static class DeviceFingerprint
{
    /// <summary>Returned when the family cannot be determined.</summary>
    public const string Unknown = "unknown";

    public static string Family(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return Unknown;
        }

        if (Contains(userAgent, "Android"))
        {
            return "android";
        }

        if (Contains(userAgent, "iPhone") || Contains(userAgent, "iPad"))
        {
            return "ios";
        }

        if (Contains(userAgent, "Windows"))
        {
            return "windows";
        }

        if (Contains(userAgent, "Macintosh") || Contains(userAgent, "Mac OS"))
        {
            return "macos";
        }

        return Contains(userAgent, "Linux") ? "linux" : Unknown;
    }

    private static bool Contains(string value, string token) =>
        value.Contains(token, StringComparison.OrdinalIgnoreCase);
}
