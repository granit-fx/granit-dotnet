namespace Granit.Guids;

/// <summary>
/// <see cref="IGuidGenerator"/> implementation backed by <see cref="Guid.NewGuid()"/>.
/// Available via <see cref="Instance"/> for contexts without DI.
/// </summary>
public sealed class SimpleGuidGenerator : IGuidGenerator
{
    /// <summary>
    /// Static instance for use outside of a dependency injection container.
    /// </summary>
    public static SimpleGuidGenerator Instance { get; } = new();

    /// <inheritdoc />
#pragma warning disable GRSEC002 // Guid.NewGuid is the backing implementation
    public Guid Create() => Guid.NewGuid();
#pragma warning restore GRSEC002
}
