using System.Linq.Expressions;

namespace Granit.Persistence;

/// <summary>
/// Factory for inline, one-off specifications.
/// </summary>
/// <example>
/// <code>
/// ListAsync(Spec.For&lt;BlobDescriptor&gt;()
///     .Where(b =&gt; b.Status == BlobStatus.Pending)
///     .OrderBy(b =&gt; b.CreatedAt)
///     .Limit(100), ct);
/// </code>
/// </example>
public static class Spec
{
    /// <summary>Creates an inline specification builder for <typeparamref name="T"/>.</summary>
    public static InlineSpecification<T> For<T>() where T : class => new();
}

/// <summary>
/// Fluent inline specification — exposes <see cref="Specification{T}"/> methods
/// with chaining return type.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class InlineSpecification<T> : Specification<T> where T : class
{
    // Intentional method hiding — re-exposes protected base methods as public
    // with fluent return type. IDE warnings are expected and acceptable.

    /// <inheritdoc cref="Specification{T}.Where"/>
    public new InlineSpecification<T> Where(Expression<Func<T, bool>> predicate)
    { base.Where(predicate); return this; }

    /// <inheritdoc cref="Specification{T}.OrderBy"/>
    public new InlineSpecification<T> OrderBy(Expression<Func<T, object>> key)
    { base.OrderBy(key); return this; }

    /// <inheritdoc cref="Specification{T}.OrderByDescending"/>
    public new InlineSpecification<T> OrderByDescending(Expression<Func<T, object>> key)
    { base.OrderByDescending(key); return this; }

    /// <inheritdoc cref="Specification{T}.Paginate"/>
    public new InlineSpecification<T> Paginate(int page, int pageSize)
    { base.Paginate(page, pageSize); return this; }

    /// <inheritdoc cref="Specification{T}.Limit"/>
    public new InlineSpecification<T> Limit(int count)
    { base.Limit(count); return this; }

    /// <inheritdoc cref="Specification{T}.AsReadOnly"/>
    public new InlineSpecification<T> AsReadOnly()
    { base.AsReadOnly(); return this; }
}
