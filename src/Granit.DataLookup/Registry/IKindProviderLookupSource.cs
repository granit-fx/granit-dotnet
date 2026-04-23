using Granit.DataLookup.Descriptors;

namespace Granit.DataLookup.Registry;

/// <summary>
/// Optional marker for <see cref="Sources.ILookupSource"/> implementations that want to
/// advertise a specific <see cref="LookupKind"/> in the <c>GET /api/granit/lookups</c>
/// manifest. Implementations that do not implement this interface default to
/// <see cref="LookupKind.Simple"/>.
/// </summary>
internal interface IKindProviderLookupSource
{
    /// <summary>The kind advertised for this source in the manifest.</summary>
    LookupKind Kind { get; }
}
