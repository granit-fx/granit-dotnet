using System.Linq.Expressions;
using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Identity;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Import.Identity;

/// <summary>
/// Shared implementation for <see cref="BusinessKeyResolver{TEntity, TContext}"/> and
/// <see cref="CompositeKeyResolver{TEntity, TContext}"/> — both resolve identity purely from the
/// declared business key property values, whether there is one property or several.
/// </summary>
/// <remarks>
/// Key extractors are compiled once from <see cref="ImportDefinition{TEntity}.GetBusinessKeyProperties"/>.
/// Every key produces an <see cref="RecordOperation.Upsert"/> — the executor resolves insert vs.
/// update by prefetching existing rows for the batch. A key already seen earlier in the same
/// import (tracked in <see cref="_seenKeys"/>, scoped to this resolver instance) is skipped as a
/// duplicate rather than silently overwritten twice.
/// </remarks>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TContext">The application DbContext type (kept for API symmetry with the other resolvers).</typeparam>
internal class KeyBasedRecordIdentityResolver<TEntity, TContext> : IRecordIdentityResolver<TEntity>
    where TEntity : class
    where TContext : DbContext
{
    private readonly Func<TEntity, object?>[] _keyExtractors;
    private readonly HashSet<EntityKey> _seenKeys = [];

    protected KeyBasedRecordIdentityResolver(ImportDefinition<TEntity> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _keyExtractors = [.. definition.GetBusinessKeyProperties().Select(CompileExtractor)];
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<RecordIdentity>> ResolveBatchAsync(
        IReadOnlyList<TEntity> batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        List<RecordIdentity> results = new(batch.Count);

        foreach (TEntity entity in batch)
        {
            results.Add(Resolve(entity));
        }

        return Task.FromResult<IReadOnlyList<RecordIdentity>>(results);
    }

    private RecordIdentity Resolve(TEntity entity)
    {
        if (_keyExtractors.Length == 0)
        {
            return RecordIdentity.Insert();
        }

        object?[] components = new object?[_keyExtractors.Length];
        bool missing = false;

        for (int i = 0; i < _keyExtractors.Length; i++)
        {
            object? value = _keyExtractors[i](entity);
            if (IsMissing(value))
            {
                missing = true;
            }

            components[i] = value;
        }

        if (missing)
        {
            return RecordIdentity.Ambiguous(IdentityReasonCodes.MissingKeyComponent);
        }

        EntityKey key = new(components);
        return _seenKeys.Add(key)
            ? RecordIdentity.Upsert(key, EntityKeyKind.BusinessKey)
            : RecordIdentity.Skip(IdentityReasonCodes.DuplicateKeyInFile);
    }

    private static bool IsMissing(object? value) =>
        value is null || (value is string text && string.IsNullOrEmpty(text));

    private static Func<TEntity, object?> CompileExtractor(string propertyName)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        MemberExpression property = Expression.Property(parameter, propertyName);
        UnaryExpression converted = Expression.Convert(property, typeof(object));
        return Expression.Lambda<Func<TEntity, object?>>(converted, parameter).Compile();
    }
}
