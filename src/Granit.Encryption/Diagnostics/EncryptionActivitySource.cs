using System.Diagnostics;

namespace Granit.Encryption.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Encryption distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class EncryptionActivitySource
{
    /// <summary>The name of the Granit.Encryption <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Encryption";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string KeyCreate = "encryption.key-create";
    internal const string KeyRetrieve = "encryption.key-retrieve";
    internal const string KeyDelete = "encryption.key-delete";
    internal const string Shred = "encryption.shred";
    internal const string ShredBatch = "encryption.shred-batch";
}
