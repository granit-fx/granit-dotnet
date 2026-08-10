using Granit.Domain;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Granit.Persistence.EntityFrameworkCore.Conventions;

/// <summary>
/// Native form of the translation pass (#3158), non-filter half: for every
/// <see cref="ITranslation{TParent}"/> entity — FK <c>ParentId → Parent.Id</c> with cascade
/// delete, unique index on <c>(ParentId, Culture)</c>, and <c>Culture</c> capped at 20 chars
/// (BCP 47). The soft-delete/active filter mirrors stay in <c>GranitDbContext</c>
/// (this-bound, #3178).
/// </summary>
internal sealed class GranitTranslationConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (IConventionEntityType entityType in modelBuilder.Metadata.GetEntityTypes().ToList())
        {
            Type? translationInterface = entityType.ClrType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType
                    && i.GetGenericTypeDefinition() == typeof(ITranslation<>));

            if (translationInterface is null)
            {
                continue;
            }

            ConfigureCulture(entityType);
            ConfigureParentRelationship(modelBuilder, entityType, translationInterface);
            ConfigureUniqueIndex(entityType);
        }
    }

    private static void ConfigureCulture(IConventionEntityType entityType)
    {
        IConventionProperty? culture = entityType.FindProperty(nameof(ITranslation.Culture));
        if (culture is null)
        {
            return;
        }

        if (culture.GetMaxLength() is null)
        {
            culture.SetMaxLength(20);
        }

        culture.SetIsNullable(false);
    }

    private static void ConfigureParentRelationship(
        IConventionModelBuilder modelBuilder,
        IConventionEntityType entityType,
        Type translationInterface)
    {
        Type parentType = translationInterface.GetGenericArguments()[0];
        IConventionEntityType? parentEntityType = modelBuilder.Metadata.FindEntityType(parentType);
        IConventionProperty? parentId = entityType.FindProperty(nameof(ITranslation.ParentId));

        if (parentEntityType is null || parentId is null)
        {
            return;
        }

        IConventionForeignKeyBuilder? fkBuilder = entityType.Builder.HasRelationship(
            parentEntityType, [parentId], parentEntityType.FindPrimaryKey()!);
        fkBuilder?.Metadata.SetDeleteBehavior(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);
        fkBuilder?.Metadata.SetDependentToPrincipal(
            entityType.ClrType.GetProperty(nameof(ITranslation<Entity>.Parent)));
    }

    private static void ConfigureUniqueIndex(IConventionEntityType entityType)
    {
        IConventionProperty? parentId = entityType.FindProperty(nameof(ITranslation.ParentId));
        IConventionProperty? culture = entityType.FindProperty(nameof(ITranslation.Culture));

        if (parentId is null || culture is null)
        {
            return;
        }

        IConventionProperty[] indexProperties = [parentId, culture];
        bool exists = entityType.GetIndexes().Any(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(indexProperties.Select(p => p.Name)));

        if (!exists)
        {
            entityType.Builder.HasIndex(indexProperties)?.Metadata.SetIsUnique(true);
        }
    }
}
