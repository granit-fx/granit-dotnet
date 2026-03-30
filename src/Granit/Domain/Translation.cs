namespace Granit.Domain;

/// <summary>
/// Base class for translation entities. Provides <see cref="Entity.Id"/> (Guid),
/// <see cref="ParentId"/> (foreign key), and <see cref="Culture"/> (BCP 47).
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class for translations that do not require an ISO 27001 audit trail.
/// For ISO 27001-compliant translations with <c>CreatedAt/By</c> and <c>ModifiedAt/By</c>,
/// use <see cref="AuditedTranslation{TParent}"/> instead.
/// </para>
/// <para>
/// The (<see cref="ParentId"/>, <see cref="Culture"/>) pair is enforced unique
/// by <c>ApplyGranitConventions()</c> in <c>Granit.Persistence.EntityFrameworkCore</c>.
/// </para>
/// </remarks>
/// <typeparam name="TParent">The parent entity type.</typeparam>
public abstract class Translation<TParent> : Entity, ITranslation<TParent>
    where TParent : Entity
{
    /// <inheritdoc />
    public Guid ParentId { get; set; }

    /// <inheritdoc />
    public string Culture { get; set; } = string.Empty;

    /// <inheritdoc />
    public TParent? Parent { get; set; }
}
