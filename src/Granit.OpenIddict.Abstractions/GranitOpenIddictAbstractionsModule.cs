using Granit.Modularity;

namespace Granit.OpenIddict;

/// <summary>
/// Granit module for the runtime-agnostic OpenIddict contracts (<see cref="Services.ISigningKeyStore"/>,
/// <see cref="Services.IKeyRotationService"/>, <see cref="Services.IClaimsDestinationProvider"/>,
/// <see cref="Domain.SigningKey"/>, and the permission/feature/setting name catalogues).
/// </summary>
/// <remarks>
/// Carries no service registrations: the concrete implementations live in <c>Granit.OpenIddict</c>
/// (claims-destination provider, key-rotation service) and <c>Granit.OpenIddict.EntityFrameworkCore</c>
/// (signing-key store). Depend on this module to consume the contracts without the runtime payload.
/// </remarks>
public sealed class GranitOpenIddictAbstractionsModule : GranitModule;
