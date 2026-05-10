namespace Granit.Browsing.Permissions;

/// <summary>
/// Permission constants exposed by <c>Granit.Browsing</c> — gating page acquisition,
/// navigation, script injection, CSP bypass and the privileged <c>file://</c> scheme
/// used by the PDF viewer capability.
/// </summary>
public static class BrowsingPermissions
{
    /// <summary>Permission group name (used as the resource-key prefix for localisation).</summary>
    public const string GroupName = "Granit.Browsing";

    /// <summary>Permissions on browser pages.</summary>
    public static class Pages
    {
        /// <summary>Acquire a page from the headless browser pool.</summary>
        public const string Acquire = "Granit.Browsing.Pages.Acquire";

        /// <summary>Navigate a page to a URL.</summary>
        public const string Navigate = "Granit.Browsing.Pages.Navigate";

        /// <summary>Inject a script or evaluate an expression in the page context.</summary>
        public const string InjectScript = "Granit.Browsing.Pages.InjectScript";

        /// <summary>Bypass the page's Content-Security-Policy headers (requires explicit opt-in by the sandbox).</summary>
        public const string BypassCsp = "Granit.Browsing.Pages.BypassCsp";

        /// <summary>Use the <c>file://</c> scheme for navigation (e.g. PDF viewer capability).</summary>
        public const string UseFileScheme = "Granit.Browsing.Pages.UseFileScheme";
    }
}
