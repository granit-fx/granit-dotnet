using System;
using Granit.DataProtection;
using Granit.Events;

namespace Granit.Browsing.Diagnostics;

/// <summary>
/// Local domain event raised when a page is acquired from the pool. Consumed by audit /
/// observability sinks, providing an auditable trail for browser activity.
/// </summary>
/// <param name="PageId">Stable identifier the provider attaches to the page.</param>
/// <param name="EngineName">Engine name (<c>"chromium-puppeteer"</c>, …).</param>
/// <param name="TenantId">Current tenant identifier (lowercase <c>N</c> Guid form) or <c>"host"</c>.</param>
/// <param name="UserAgent">User agent applied to the page, if any.</param>
/// <param name="AcquiredAt">Acquisition timestamp.</param>
public sealed record BrowserPageAcquiredEvent(
    Guid PageId,
    string EngineName,
    string? TenantId,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Hash)] string? UserAgent,
    DateTimeOffset AcquiredAt) : IDomainEvent;
