using Bogus;
using Granit.Core.Domain;

namespace Granit.Testing.Generators;

/// <summary>
/// Bogus <see cref="Faker{T}"/> extension methods for populating audit and
/// multi-tenancy fields on Granit domain entities.
/// </summary>
public static class GranitEntityFakerExtensions
{
    /// <summary>
    /// Adds rules for <see cref="Entity.Id"/> and <see cref="CreationAuditedEntity"/>
    /// audit fields (<c>CreatedAt</c>, <c>CreatedBy</c>).
    /// </summary>
    public static Faker<T> RuleForCreationAudit<T>(this Faker<T> faker)
        where T : CreationAuditedEntity
    {
        ArgumentNullException.ThrowIfNull(faker);
        return faker
            .RuleFor(e => e.Id, f => f.Random.Guid())
            .RuleFor(e => e.CreatedAt, f => f.Date.RecentOffset(30))
            .RuleFor(e => e.CreatedBy, f => f.Random.Guid().ToString());
    }

    /// <summary>
    /// Adds rules for <see cref="AuditedEntity"/> fields
    /// (<c>ModifiedAt</c>, <c>ModifiedBy</c>) on top of creation audit fields.
    /// </summary>
    public static Faker<T> RuleForAudit<T>(this Faker<T> faker)
        where T : AuditedEntity
    {
        ArgumentNullException.ThrowIfNull(faker);
        return faker
            .RuleForCreationAudit()
            .RuleFor(e => e.ModifiedAt, f => f.Date.RecentOffset(7).OrNull(f, 0.3f))
            .RuleFor(e => e.ModifiedBy, f => f.Random.Guid().ToString().OrNull(f, 0.3f));
    }

    /// <summary>
    /// Adds rules for <see cref="FullAuditedEntity"/> fields
    /// (<c>IsDeleted</c>, <c>DeletedAt</c>, <c>DeletedBy</c>) on top of audit fields.
    /// Non-deleted state by default.
    /// </summary>
    public static Faker<T> RuleForFullAudit<T>(this Faker<T> faker)
        where T : FullAuditedEntity
    {
        ArgumentNullException.ThrowIfNull(faker);
        return faker
            .RuleForAudit()
            .RuleFor(e => e.IsDeleted, _ => false)
            .RuleFor(e => e.DeletedAt, _ => (DateTimeOffset?)null)
            .RuleFor(e => e.DeletedBy, _ => (string?)null);
    }

    /// <summary>
    /// Adds a rule for the <see cref="IMultiTenant.TenantId"/> field.
    /// </summary>
    /// <param name="faker">The Bogus faker instance.</param>
    /// <param name="tenantId">
    /// Fixed tenant ID to use, or <c>null</c> to generate a random one.
    /// </param>
    public static Faker<T> RuleForMultiTenant<T>(this Faker<T> faker, Guid? tenantId = null)
        where T : class, IMultiTenant
    {
        ArgumentNullException.ThrowIfNull(faker);
        return faker.RuleFor(e => e.TenantId, f => tenantId ?? f.Random.Guid());
    }
}
