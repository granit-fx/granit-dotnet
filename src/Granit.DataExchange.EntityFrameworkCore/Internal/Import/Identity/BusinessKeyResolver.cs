using System.Linq.Expressions;
using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Identity;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Import.Identity;

/// <summary>
/// Resolves entity identity by querying the application DbContext using a single business key property.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TContext">The application DbContext type.</typeparam>
internal sealed class BusinessKeyResolver<TEntity, TContext>(
    IDbContextFactory<TContext> contextFactory,
    ImportDefinition<TEntity> definition) : IRecordIdentityResolver<TEntity>
    where TEntity : class
    where TContext : DbContext
{
    /// <inheritdoc/>
    public async Task<RecordIdentity<TEntity>> ResolveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> keyProperties = definition.GetBusinessKeyProperties();
        if (keyProperties.Count == 0)
        {
            return new RecordIdentity<TEntity> { Operation = RecordOperation.Insert };
        }

        string keyProperty = keyProperties[0];
        await using TContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        Expression<Func<TEntity, bool>> predicate = BuildPredicate(entity, keyProperty);
        TEntity? existing = await context.Set<TEntity>().FirstOrDefaultAsync(predicate, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return new RecordIdentity<TEntity> { Operation = RecordOperation.Insert };
        }

        return new RecordIdentity<TEntity>
        {
            Operation = RecordOperation.Update,
            ExistingEntity = existing,
        };
    }

    private static Expression<Func<TEntity, bool>> BuildPredicate(TEntity entity, string propertyName)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        MemberExpression property = Expression.Property(parameter, propertyName);
        object? value = typeof(TEntity).GetProperty(propertyName)?.GetValue(entity);
        ConstantExpression constant = Expression.Constant(value, property.Type);
        BinaryExpression equality = Expression.Equal(property, constant);
        return Expression.Lambda<Func<TEntity, bool>>(equality, parameter);
    }
}
