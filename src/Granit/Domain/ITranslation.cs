namespace Granit.Domain;

/// <summary>
/// Non-generic marker interface for all translation entities.
/// Exposes the <see cref="Culture"/> and <see cref="ParentId"/> properties
/// used by resolution logic and EF Core conventions.
/// </summary>
public interface ITranslation
{
    /// <summary>Foreign key to the parent entity.</summary>
    Guid ParentId { get; set; }

    /// <summary>BCP 47 culture tag (e.g. <c>"fr"</c>, <c>"en-US"</c>, <c>"nl-BE"</c>).</summary>
    string Culture { get; set; }
}

/// <summary>
/// Typed translation interface with a navigation property back to the parent entity.
/// Implemented by <see cref="Translation{TParent}"/> and <see cref="AuditedTranslation{TParent}"/>.
/// </summary>
/// <remarks>
/// EF Core conventions in <c>ApplyGranitConventions()</c> detect this interface
/// and automatically configure the foreign key, cascade delete, and unique index
/// on (<see cref="ITranslation.ParentId"/>, <see cref="ITranslation.Culture"/>).
/// </remarks>
/// <typeparam name="TParent">The parent entity type that owns the translations.</typeparam>
public interface ITranslation<TParent> : ITranslation where TParent : Entity
{
    /// <summary>Navigation property to the parent entity.</summary>
    TParent? Parent { get; set; }
}
