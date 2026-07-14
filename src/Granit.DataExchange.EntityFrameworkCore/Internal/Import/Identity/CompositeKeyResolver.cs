using Granit.DataExchange.Import;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Import.Identity;

/// <summary>
/// Resolves entity identity using multiple business key properties declared via <c>HasCompositeKey()</c>.
/// Touches no <see cref="DbContext"/> — the prefetch of existing rows is the executor's job.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TContext">The application DbContext type (kept for API symmetry with the other resolvers).</typeparam>
internal sealed class CompositeKeyResolver<TEntity, TContext>(ImportDefinition<TEntity> definition)
    : KeyBasedRecordIdentityResolver<TEntity, TContext>(definition)
    where TEntity : class
    where TContext : DbContext;
