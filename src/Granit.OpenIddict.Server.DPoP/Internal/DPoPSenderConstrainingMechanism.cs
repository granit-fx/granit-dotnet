using Granit.OpenIddict.Options;
using Granit.OpenIddict.Server.SenderConstraining;

namespace Granit.OpenIddict.Server.DPoP.Internal;

/// <summary>
/// Declares that this package provides the <see cref="SenderConstrainingMode.DPoP"/> mechanism,
/// so the server's <c>SenderConstrainingOptionsValidator</c> accepts <c>SenderConstraining = DPoP</c>.
/// </summary>
internal sealed class DPoPSenderConstrainingMechanism : ISenderConstrainingMechanism
{
    public SenderConstrainingMode Mode => SenderConstrainingMode.DPoP;
}
