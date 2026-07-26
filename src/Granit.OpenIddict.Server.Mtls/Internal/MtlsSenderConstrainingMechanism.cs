using Granit.OpenIddict.Options;
using Granit.OpenIddict.Server.SenderConstraining;

namespace Granit.OpenIddict.Server.Mtls.Internal;

/// <summary>
/// Declares that this package provides the <see cref="SenderConstrainingMode.Mtls"/> mechanism,
/// so the server's <c>SenderConstrainingOptionsValidator</c> accepts <c>SenderConstraining = Mtls</c>.
/// </summary>
internal sealed class MtlsSenderConstrainingMechanism : ISenderConstrainingMechanism
{
    public SenderConstrainingMode Mode => SenderConstrainingMode.Mtls;
}
