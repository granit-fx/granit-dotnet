using System.Linq.Expressions;

namespace Granit.Persistence;

/// <summary>
/// Specification with server-side projection to <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="T">The source entity type.</typeparam>
/// <typeparam name="TResult">The projected result type.</typeparam>
public abstract class Specification<T, TResult> : Specification<T>
    where T : class
{
    /// <summary>Projection expression (e.g., <c>Select(e =&gt; new Dto(e.Name))</c>).</summary>
    public Expression<Func<T, TResult>>? Selector { get; private set; }

    /// <summary>Sets the projection expression.</summary>
    protected void Select(Expression<Func<T, TResult>> selector) => Selector = selector;
}
