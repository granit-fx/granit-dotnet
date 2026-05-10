using System.Collections.Generic;

namespace Granit.Browsing;

/// <summary>
/// Declarative sandbox profile applied at page-acquisition time when registered. Allows
/// consumers that render untrusted content (user-uploaded HTML / PDFs) to enforce
/// resource and capability restrictions without touching every call site.
/// </summary>
/// <remarks>
/// Hosts register a profile with <c>services.AddSingleton&lt;IBrowserSandboxProfile&gt;(...)</c>
/// or scope it per consumer with a keyed registration (provider-specific). The provider
/// applies the profile through its native equivalents — request interception for
/// <see cref="BlockNetworkRequests"/>, <c>javaScriptEnabled = false</c> for
/// <see cref="DisableJavaScript"/>, etc.
/// </remarks>
public interface IBrowserSandboxProfile
{
    /// <summary>When <c>true</c>, the page is opened with JavaScript disabled.</summary>
    bool DisableJavaScript { get; }

    /// <summary>When <c>true</c>, the page cannot issue network requests (other than the initial document load).</summary>
    bool BlockNetworkRequests { get; }

    /// <summary>When <c>true</c>, image loading is suppressed — useful when only DOM structure is needed.</summary>
    bool DisableImages { get; }

    /// <summary>
    /// URL pattern list (provider-defined glob syntax) blocked unconditionally. Use to
    /// drop tracker domains while keeping the rest of the network open.
    /// </summary>
    IReadOnlyList<string>? BlockedUrlPatterns { get; }
}
