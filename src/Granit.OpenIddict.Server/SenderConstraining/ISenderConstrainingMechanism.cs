using Granit.OpenIddict.Options;

namespace Granit.OpenIddict.Server.SenderConstraining;

/// <summary>
/// Marker registered by a sender-constraining mechanism package (e.g.
/// <c>Granit.OpenIddict.Server.DPoP</c>) to declare that it wires the corresponding
/// proof-of-possession binding into the OpenIddict server pipeline. The startup validator
/// uses it to confirm that the configured <see cref="GranitOpenIddictOptions.SenderConstraining"/>
/// mode has its mechanism package referenced.
/// </summary>
public interface ISenderConstrainingMechanism
{
    /// <summary>The mode this mechanism provides.</summary>
    SenderConstrainingMode Mode { get; }
}
