using Granit.Entities.Customization.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Entities.Customization.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IEntityCustomizationWriter"/>. Upsert
/// is full-replace: a write replaces the previous delta list in its entirety.
/// Revert-to-default = delete the row.
/// </summary>
internal sealed class EfEntityCustomizationWriter(
    IDbContextFactory<CustomizationDbContext> contextFactory) : IEntityCustomizationWriter
{
    public async Task UpsertAsync(EntityCustomization customization, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customization);
        await using CustomizationDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        EntityCustomization? existing = await context.EntityCustomizations
            .FirstOrDefaultAsync(
                x => x.EntityName == customization.EntityName
                  && x.LayoutKind == customization.LayoutKind,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            context.EntityCustomizations.Add(customization);
        }
        else
        {
            existing.Replace(customization.Deltas);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using CustomizationDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        EntityCustomization? entity = await context.EntityCustomizations
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        context.EntityCustomizations.Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
