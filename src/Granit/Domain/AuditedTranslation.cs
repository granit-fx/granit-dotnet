namespace Granit.Domain;

/// <summary>
/// Translation with full creation and modification audit trail (ISO 27001 compliant).
/// Inherits from <see cref="AuditedEntity"/> and implements <see cref="ITranslation{TParent}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Use this base class for translations on entities subject to ISO 27001 3-year audit
/// trail requirements. Fields <c>CreatedAt</c>, <c>CreatedBy</c>, <c>ModifiedAt</c>,
/// <c>ModifiedBy</c> are populated automatically by <c>AuditedEntityInterceptor</c>.
/// </para>
/// <para>
/// The (<see cref="ParentId"/>, <see cref="Culture"/>) pair is enforced unique
/// by <c>ApplyGranitConventions()</c> in <c>Granit.Persistence</c>.
/// </para>
/// </remarks>
/// <typeparam name="TParent">The parent entity type.</typeparam>
public abstract class AuditedTranslation<TParent> : AuditedEntity, ITranslation<TParent>
    where TParent : Entity
{
    /// <inheritdoc />
    public Guid ParentId { get; set; }

    /// <inheritdoc />
    public string Culture { get; set; } = string.Empty;

    /// <inheritdoc />
    public TParent? Parent { get; set; }
}
