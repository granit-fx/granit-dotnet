using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Granit.Encryption.EntityFrameworkCore;

/// <summary>
/// Default implementation of <see cref="IReEncryptionService"/> backed by EF Core.
/// </summary>
/// <remarks>
/// <para>
/// The service loads entities in batches, marks each <see cref="EncryptedAttribute"/>
/// property as modified, and saves. On save, the EF Core value converter runs
/// <see cref="Granit.Encryption.IStringEncryptionService.Encrypt"/> — producing
/// ciphertext with the current key version.
/// </para>
/// <para>
/// This approach is safe and idempotent. When using the Vault Transit provider,
/// each save re-encrypts with the latest key version. When using the AES provider,
/// each save produces a fresh ciphertext (new IV).
/// </para>
/// <para>
/// <b>Multi-tenancy:</b> In multi-tenant deployments with per-tenant encryption keys,
/// callers MUST ensure the correct tenant context is active before invoking
/// <see cref="ReEncryptAsync{TEntity}"/>. EF Core named query filters will scope the
/// query to the current tenant if the entity implements <c>IMultiTenant</c>. For
/// system-wide re-encryption, iterate tenants explicitly with
/// <c>ICurrentTenant.Change(tenantId)</c> scopes.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The <see cref="DbContext"/> type that owns the entities.</typeparam>
public sealed class DefaultReEncryptionService<TContext>(IDbContextFactory<TContext> contextFactory)
    : IReEncryptionService
    where TContext : DbContext
{
    /// <inheritdoc />
    public async Task ReEncryptAsync<TEntity>(int batchSize = 500, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        PropertyInfo[] encryptedProperties = typeof(TEntity)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string)
                        && p.GetCustomAttribute<EncryptedAttribute>() is not null)
            .ToArray();

        if (encryptedProperties.Length == 0)
        {
            return;
        }

        int offset = 0;
        int count;

        do
        {
            await using TContext ctx = await contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            List<TEntity> batch = await ctx.Set<TEntity>()
                .OrderBy(e => EF.Property<Guid>(e, "Id"))
                .Skip(offset)
                .Take(batchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            count = batch.Count;

            foreach (EntityEntry<TEntity> entry in batch.Select(entity => ctx.Entry(entity)))
            {
                foreach (PropertyInfo prop in encryptedProperties)
                {
                    entry.Property(prop.Name).IsModified = true;
                }
            }

            await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            offset += count;
        }
        while (count == batchSize);
    }
}
