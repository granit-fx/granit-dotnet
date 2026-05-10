using System;
using Granit.DataProtection;
using Granit.Events;

namespace Granit.Browsing.Diagnostics;

/// <summary>
/// Local domain event raised whenever a navigation is attempted — including blocked
/// attempts so a tenant's render-time SSRF or pattern denial is visible in audit.
/// </summary>
/// <param name="PageId">Stable identifier the provider attaches to the page.</param>
/// <param name="EngineName">Engine name (<c>"chromium-puppeteer"</c>, …).</param>
/// <param name="Url">Target URL (masked through <see cref="SensitiveDataAttribute"/>).</param>
/// <param name="BlockedBySandbox"><c>true</c> when the sandbox short-circuited the navigation.</param>
/// <param name="BlockReason">Provider-supplied reason when blocked; <c>null</c> otherwise.</param>
/// <param name="NavigatedAt">Event timestamp.</param>
public sealed record BrowserUrlNavigatedEvent(
    Guid PageId,
    string EngineName,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Mask)] string Url,
    bool BlockedBySandbox,
    string? BlockReason,
    DateTimeOffset NavigatedAt) : IDomainEvent;
