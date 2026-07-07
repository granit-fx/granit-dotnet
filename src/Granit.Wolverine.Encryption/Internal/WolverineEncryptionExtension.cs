using Granit.Encryption;
using Granit.Wolverine.Encryption.Extensions;
using Wolverine;

namespace Granit.Wolverine.Encryption.Internal;

/// <summary>
/// Wolverine extension that activates
/// <see cref="WolverineEncryptionOptionsExtensions.UseEncryptedSensitiveData"/> at
/// bootstrap time, with the <see cref="IStringEncryptionService"/> resolved from
/// the host's container.
/// </summary>
/// <remarks>
/// Registered via <c>services.AddWolverineExtension&lt;T&gt;()</c> by
/// <see cref="GranitWolverineEncryptionModule"/>. Wolverine applies registered
/// <see cref="IWolverineExtension"/> instances when the runtime is constructed,
/// so the extension observes the exact singleton the rest of the application
/// uses — no throwaway service provider, no risk of divergent key material if
/// the encryption graph ever becomes stateful (Vault-backed rotation, key
/// caches). Unlike the persistence providers, this extension only mutates
/// serializer settings and never registers services, so the Wolverine 3.0
/// read-only-IServiceCollection restriction does not apply.
/// </remarks>
internal sealed class WolverineEncryptionExtension(IStringEncryptionService encryption)
    : IWolverineExtension
{
    public void Configure(WolverineOptions options) =>
        options.UseEncryptedSensitiveData(encryption);
}
