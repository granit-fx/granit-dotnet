using Granit.Domain;

namespace Granit.Testing.Persistence.Domain;

/// <summary>Conformance entity for the <see cref="IActive"/> named query filter.</summary>
public sealed class ConformanceToggle : Entity, IActive
{
    /// <inheritdoc/>
    public bool Activated { get; set; }

    /// <summary>Free label so tests can tag their own rows.</summary>
    public string Label { get; set; } = string.Empty;
}
