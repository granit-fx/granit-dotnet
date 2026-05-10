using System.Collections.Generic;

namespace Granit.Browsing.Options;

/// <summary>Per-page configuration applied at acquisition time.</summary>
public sealed record BrowserPageOptions
{
    /// <summary>Viewport dimensions in CSS pixels. <c>null</c> uses the provider default (typically 1280×720).</summary>
    public ViewportSize? Viewport { get; init; }

    /// <summary>Device pixel ratio. <c>1.0</c> is standard density; <c>2.0</c> emulates a Retina display.</summary>
    public double DeviceScaleFactor { get; init; } = 1.0;

    /// <summary>Custom user agent. <c>null</c> keeps the engine default.</summary>
    public string? UserAgent { get; init; }

    /// <summary>BCP-47 locale used for the <c>Accept-Language</c> header and JavaScript <c>Intl</c> APIs.</summary>
    public string? Locale { get; init; }

    /// <summary>IANA timezone identifier (<c>"Europe/Brussels"</c>, <c>"UTC"</c>, …).</summary>
    public string? TimezoneId { get; init; }

    /// <summary>Disables JavaScript execution on the page when <c>false</c>.</summary>
    public bool JavaScriptEnabled { get; init; } = true;

    /// <summary>Bypasses the page's <c>Content-Security-Policy</c> headers when <c>true</c> — required for some script injection scenarios.</summary>
    public bool BypassCsp { get; init; }

    /// <summary>Extra headers attached to every request originating from the page.</summary>
    public IReadOnlyDictionary<string, string>? ExtraHeaders { get; init; }

    /// <summary>Cookies pre-loaded into the page's storage before navigation.</summary>
    public IReadOnlyList<CookieParam>? Cookies { get; init; }

    /// <summary>Color scheme emulation: <c>"light"</c>, <c>"dark"</c>, <c>"no-preference"</c>.</summary>
    public string? ColorScheme { get; init; }

    /// <summary>Media type emulation: <c>"screen"</c> or <c>"print"</c>. Drives <c>@media print</c> styles.</summary>
    public string? MediaType { get; init; }
}

/// <summary>Viewport dimensions in CSS pixels.</summary>
/// <param name="Width">Width in pixels — must be strictly positive.</param>
/// <param name="Height">Height in pixels — must be strictly positive.</param>
public sealed record ViewportSize(int Width, int Height);

/// <summary>A cookie pre-loaded into a page session.</summary>
/// <param name="Name">Cookie name.</param>
/// <param name="Value">Cookie value.</param>
/// <param name="Domain">Cookie domain. Either <see cref="Domain"/> + <see cref="Path"/> or <see cref="Url"/> must be supplied.</param>
/// <param name="Path">Cookie path.</param>
/// <param name="Url">Anchor URL — alternative to domain/path that the engine resolves itself.</param>
/// <param name="Expires">Unix timestamp of the cookie expiry; <c>null</c> for a session cookie.</param>
/// <param name="HttpOnly">When <c>true</c>, the cookie is not exposed to JavaScript.</param>
/// <param name="Secure">When <c>true</c>, the cookie is sent only over TLS.</param>
/// <param name="SameSite">SameSite policy — <c>"Strict"</c>, <c>"Lax"</c>, or <c>"None"</c>.</param>
public sealed record CookieParam(
    string Name,
    string Value,
    string? Domain = null,
    string? Path = null,
    string? Url = null,
    long? Expires = null,
    bool HttpOnly = false,
    bool Secure = false,
    string? SameSite = null);
