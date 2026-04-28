using Granit.DataLookup.Descriptors;
using Granit.DataLookup.Sources;

namespace Granit.DataLookup.Registry;

/// <summary>
/// Optional marker for <see cref="ILookupSource"/> implementations that want to
/// advertise a specific <see cref="LookupKind"/> in the <c>GET /lookups</c>
/// manifest. Implementations that do not implement this interface default to
/// <see cref="LookupKind.Simple"/>.
/// </summary>
/// <remarks>
/// Public so that adapter packages outside <c>Granit.DataLookup</c> (e.g.
/// <c>Granit.ReferenceData</c>, <c>Granit.DataLookup.EntityFrameworkCore</c>) can
/// declare their source kind without needing <c>InternalsVisibleTo</c>.
/// </remarks>
public interface IKindProviderLookupSource
{
    /// <summary>The kind advertised for this source in the manifest.</summary>
    LookupKind Kind { get; }
}
