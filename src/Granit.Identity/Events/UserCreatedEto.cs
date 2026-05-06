using Granit.DataProtection;
using Granit.Encryption;
using Granit.Events;
using Granit.Identity.Domain;

namespace Granit.Identity.Events;

/// <summary>
/// Distributed event raised by the canonical
/// <see cref="User"/> aggregate the moment a row is materialised — by
/// <c>LocalIdentityManager.CreateAsync</c> (local-side, B-step 2.5),
/// <c>CachedUserLookupService.EnsureCachedAndUserAsync</c>
/// (federated-side, B-step 3.5), or any future host-side path that
/// constructs a <see cref="User"/> through <see cref="User.Create"/>.
/// </summary>
/// <remarks>
/// <para>
/// The <c>Granit.Identity.Parties</c> bridge handler subscribes to this
/// event to materialise a corresponding <c>Party</c> of kind
/// <c>Person</c> when both modules are loaded (per ADR-051's bridge
/// contract, B-step 5). Tiny apps without <c>Granit.Parties</c> simply
/// have no subscriber and the event is a no-op.
/// </para>
/// <para>
/// PII fields (<see cref="DisplayName"/>, <see cref="Email"/>,
/// <see cref="FirstName"/>, <see cref="LastName"/>,
/// <see cref="PhoneNumber"/>) carry <see cref="SensitiveDataAttribute"/>
/// so they are redacted in audit logs, MCP output, and diagnostics.
/// The Wolverine outbox should be pruned frequently to limit plaintext
/// PII exposure in the database.
/// </para>
/// </remarks>
/// <param name="UserId">Canonical <see cref="User.Id"/>.</param>
/// <param name="DisplayName">Display label.</param>
/// <param name="Email">Primary login email.</param>
/// <param name="FirstName">Given name (optional).</param>
/// <param name="LastName">Family name (optional).</param>
/// <param name="PhoneNumber">E.164 phone number (optional).</param>
/// <param name="TenantId">Tenant scope, or <see langword="null"/> for host-level users.</param>
public sealed record UserCreatedEto(
    Guid UserId,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Mask), Encrypted]
    string DisplayName,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Omit), Encrypted]
    string Email,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Mask), Encrypted]
    string? FirstName,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Mask), Encrypted]
    string? LastName,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Omit), Encrypted]
    string? PhoneNumber,
    Guid? TenantId) : IIntegrationEvent;
