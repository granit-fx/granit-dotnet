using System.Diagnostics;

namespace Granit.IO.Diagnostics;

/// <summary>
/// Process-wide <see cref="ActivitySource"/> for the <c>Granit.IO</c> module.
/// </summary>
internal static class IOActivitySource
{
    /// <summary>Source name (<c>Granit.IO</c>).</summary>
    public const string Name = "Granit.IO";

    /// <summary>Singleton <see cref="ActivitySource"/> instance.</summary>
    public static readonly ActivitySource Instance = new(Name);
}
