using Granit.OpenIddict.Options;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.Internal;

/// <summary>
/// Upgrades the key-rotation signing algorithm to <c>PS256</c> (RSASSA-PSS) when the FAPI 2.0
/// profile is enabled and the algorithm is still the RS256 default.
/// </summary>
/// <remarks>
/// <para>
/// FAPI 2.0 §5.2.2 requires PS256 or ES256. <see cref="GranitKeyRotationOptions.SigningAlgorithm"/>
/// documents this automatic override; this post-configure implements it. An explicit non-default
/// choice (for example ES256) is respected — only the RS256 default is upgraded.
/// </para>
/// <para>
/// The algorithm is read at key generation (<c>KeyRotationService.GenerateKeyAsync</c>), so a
/// FAPI 2.0 deployment mints PS256 keys without the operator having to restate the algorithm.
/// </para>
/// </remarks>
internal sealed class Fapi2SigningAlgorithmPostConfigure(
    IOptions<GranitOpenIddictOptions> openIddictOptions)
    : IPostConfigureOptions<GranitKeyRotationOptions>
{
    internal const string DefaultAlgorithm = "RS256";
    internal const string Fapi2Algorithm = "PS256";

    /// <inheritdoc/>
    public void PostConfigure(string? name, GranitKeyRotationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (openIddictOptions.Value.EnableFapi2Profile
            && string.Equals(options.SigningAlgorithm, DefaultAlgorithm, StringComparison.Ordinal))
        {
            options.SigningAlgorithm = Fapi2Algorithm;
        }
    }
}
