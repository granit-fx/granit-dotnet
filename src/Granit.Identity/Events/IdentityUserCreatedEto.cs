using Granit.DataProtection;
using Granit.Encryption;
using Granit.Events;

namespace Granit.Identity.Events;

/// <summary>
/// Published after a new user is created in the identity provider.
/// </summary>
/// <remarks>
/// <see cref="Username"/> and <see cref="Email"/> are PII. They are marked
/// <see cref="SensitiveDataAttribute"/> so they are redacted in audit logs, MCP
/// output, and diagnostics. The Wolverine outbox should be pruned frequently to
/// minimise plaintext exposure of these fields in the database.
/// </remarks>
/// <param name="UserId">The provider-assigned user ID.</param>
/// <param name="Username">The username of the created user (may be null).</param>
/// <param name="Email">The email of the created user (may be null).</param>
public sealed record IdentityUserCreatedEto(
    string UserId,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Mask), Encrypted]
    string? Username,
    [property: SensitiveData(Level = Sensitivity.Restricted, Mode = SensitiveDataMode.Omit), Encrypted]
    string? Email) : IIntegrationEvent;
