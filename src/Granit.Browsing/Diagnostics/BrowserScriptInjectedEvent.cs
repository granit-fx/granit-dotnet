using Granit.DataProtection;
using Granit.Events;

namespace Granit.Browsing.Diagnostics;

/// <summary>
/// Local domain event raised whenever a script (or style, or <c>EvaluateAsync</c> body)
/// is injected into a page. The script body is never persisted — only its SHA-256 hash —
/// to keep audit usable without leaking sensitive injection payloads.
/// </summary>
/// <param name="PageId">Stable identifier the provider attaches to the page.</param>
/// <param name="EngineName">Engine name (<c>"chromium-puppeteer"</c>, …).</param>
/// <param name="Kind"><c>"script"</c>, <c>"style"</c>, or <c>"evaluate"</c>.</param>
/// <param name="ScriptHash">SHA-256 hash of the injected payload (hex-lowercase).</param>
/// <param name="InjectedAt">Event timestamp.</param>
public sealed record BrowserScriptInjectedEvent(
    Guid PageId,
    string EngineName,
    string Kind,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Hash)] string ScriptHash,
    DateTimeOffset InjectedAt) : IDomainEvent;
