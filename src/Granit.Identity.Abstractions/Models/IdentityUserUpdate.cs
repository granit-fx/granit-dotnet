using Granit.DataProtection;

namespace Granit.Identity.Models;

/// <summary>
/// Represents the fields that can be updated on an identity provider user.
/// All properties are optional — only non-null values are applied.
/// </summary>
/// <remarks>
/// Embedded in <c>IdentityUserProfileUpdatedEto</c>, which is published through the
/// Wolverine outbox. The PII properties (<see cref="Email"/>, <see cref="FirstName"/>,
/// <see cref="LastName"/>) are marked <see cref="SensitiveDataAttribute"/> so they are
/// redacted in audit logs, MCP output, and diagnostics.
/// </remarks>
/// <param name="Email">New email address, or <c>null</c> to leave unchanged.</param>
/// <param name="FirstName">New first name, or <c>null</c> to leave unchanged.</param>
/// <param name="LastName">New last name, or <c>null</c> to leave unchanged.</param>
/// <param name="Attributes">
/// Custom attributes to set or remove. A <c>null</c> value removes the attribute.
/// Only provided attributes are modified — existing attributes not in the dictionary are left unchanged.
/// </param>
public sealed record IdentityUserUpdate(
    [property: SensitiveData(Level = Sensitivity.Restricted, Mode = SensitiveDataMode.Omit)]
    string? Email = null,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Mask)]
    string? FirstName = null,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Mask)]
    string? LastName = null,
    IReadOnlyDictionary<string, string?>? Attributes = null);
