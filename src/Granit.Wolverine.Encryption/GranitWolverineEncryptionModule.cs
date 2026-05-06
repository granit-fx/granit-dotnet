using Granit.Encryption;
using Granit.Modularity;
using Granit.Wolverine.Encryption.Extensions;
using Granit.Wolverine.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Wolverine.Encryption;

/// <summary>
/// Granit module that wires <see cref="EncryptedAttribute"/>-driven JSON
/// encryption into the Wolverine serializer pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Resolves the registered <see cref="IStringEncryptionService"/> and the
/// <c>WolverineOptionsHolder</c> placed by
/// <see cref="GranitWolverineModule"/>, then calls
/// <see cref="WolverineEncryptionOptionsExtensions.UseEncryptedSensitiveData"/>
/// once the host has finished registering services. From that point on, every
/// saga state, command and integration event Wolverine serializes through
/// System.Text.Json has its <see cref="EncryptedAttribute"/>-marked properties
/// transparently encrypted on the wire.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitWolverineModule), typeof(GranitEncryptionModule))]
public sealed class GranitWolverineEncryptionModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The holder is registered as ImplementationInstance by
        // GranitWolverineModule's UseWolverine() — readable from the descriptor
        // list without building a provider.
        WolverineOptionsHolder holder = context.Services
            .Select(d => d.ImplementationInstance)
            .OfType<WolverineOptionsHolder>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "GranitWolverineModule must be configured before "
                + "GranitWolverineEncryptionModule. Ensure the [DependsOn] chain "
                + "is preserved.");

        // IStringEncryptionService is a stateless delegator to
        // IStringEncryptionProvider (key + algorithm captured from options).
        // Build a transient provider once, resolve the singleton, then dispose.
        // Cipher round-trip is identical with the eventual host-resolved
        // instance because both share the same configuration-derived provider.
        using ServiceProvider sp = context.Services.BuildServiceProvider();
        IStringEncryptionService encryption = sp.GetRequiredService<IStringEncryptionService>();

        holder.Options.UseEncryptedSensitiveData(encryption);
    }
}
