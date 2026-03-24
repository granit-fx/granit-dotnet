using Granit.Encryption.Extensions;
using Granit.Modularity;

namespace Granit.Encryption;

/// <summary>
/// Granit module for string encryption.
/// Default provider: AES-256-CBC (key derived via PBKDF2 from Vault config).
/// </summary>
public sealed class GranitEncryptionModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitEncryption();
}
