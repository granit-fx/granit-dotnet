using System.Linq.Expressions;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// Replaces one parameter expression with another in an expression tree. Used to rebind
/// independently built predicate lambdas onto a single shared parameter so they can be
/// composed with <see cref="Expression.AndAlso(Expression, Expression)"/> /
/// <see cref="Expression.OrElse(Expression, Expression)"/>.
/// </summary>
internal sealed class ParameterReplacer(ParameterExpression oldParam, ParameterExpression newParam)
    : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node) =>
        node == oldParam ? newParam : base.VisitParameter(node);
}
