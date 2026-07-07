using Granit.Encryption;
using Granit.Modularity;
using Granit.Wolverine.Encryption.Internal;
using Granit.Wolverine.Internal;
using Wolverine;

namespace Granit.Wolverine.Encryption;

/// <summary>
/// Granit module that wires <see cref="EncryptedAttribute"/>-driven JSON
/// encryption into the Wolverine serializer pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Registers <see cref="WolverineEncryptionExtension"/>, applied by Wolverine at
/// bootstrap with the host-resolved <see cref="IStringEncryptionService"/>. From
/// that point on, every saga state, command and integration event Wolverine
/// serializes through System.Text.Json has its
/// <see cref="EncryptedAttribute"/>-marked properties transparently encrypted
/// on the wire.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitEncryptionModule), typeof(GranitWolverineModule))]
public sealed class GranitWolverineEncryptionModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Fail fast on mis-ordering: the holder is registered as
        // ImplementationInstance by GranitWolverineModule's UseWolverine() —
        // readable from the descriptor list without building a provider. Without
        // AddGranitWolverine() the extension below would silently never apply
        // and PII would reach the outbox in plaintext.
        _ = context.Services
            .Select(d => d.ImplementationInstance)
            .OfType<WolverineOptionsHolder>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "GranitWolverineModule must be configured before "
                + "GranitWolverineEncryptionModule. Ensure the [DependsOn] chain "
                + "is preserved.");

        context.Services.AddWolverineExtension<WolverineEncryptionExtension>();
    }
}
