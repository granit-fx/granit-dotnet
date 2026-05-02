using Granit.DataProtection;
using Granit.Events;
using Granit.Identity.Domain;

namespace Granit.Identity.Events;

/// <summary>
/// Distributed event raised by <see cref="User.UpdateProfile"/> when
/// any of the canonical user's profile fields change. The
/// <c>Granit.Identity.Parties</c> bridge handler subscribes to this
/// event to keep the matching <c>Party</c> in sync (per ADR-051's
/// bridge contract, B-step 5).
/// </summary>
/// <remarks>
/// PII fields carry <see cref="SensitiveDataAttribute"/> so they are
/// redacted in audit logs, MCP output, and diagnostics. The Wolverine
/// outbox should be pruned frequently to limit plaintext PII exposure
/// in the database.
/// </remarks>
/// <param name="UserId">Canonical <see cref="User.Id"/>.</param>
/// <param name="DisplayName">Updated display label.</param>
/// <param name="Email">Updated login email.</param>
/// <param name="FirstName">Updated given name (optional).</param>
/// <param name="LastName">Updated family name (optional).</param>
/// <param name="PhoneNumber">Updated E.164 phone (optional).</param>
/// <param name="PreferredLocale">Updated BCP-47 locale (optional).</param>
/// <param name="Timezone">Updated IANA timezone (optional).</param>
/// <param name="TenantId">Tenant scope, or <see langword="null"/> for host-level users.</param>
public sealed record UserProfileChangedEto(
    Guid UserId,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Mask)]
    string DisplayName,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Omit)]
    string Email,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Mask)]
    string? FirstName,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Mask)]
    string? LastName,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Omit)]
    string? PhoneNumber,
    string? PreferredLocale,
    string? Timezone,
    Guid? TenantId) : IIntegrationEvent;
