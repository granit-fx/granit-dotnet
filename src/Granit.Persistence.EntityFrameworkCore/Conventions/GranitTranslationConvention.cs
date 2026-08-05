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

            IConventionProperty? culture = entityType.FindProperty(nameof(ITranslation.Culture));
            if (culture is not null)
            {
                if (culture.GetMaxLength() is null)
                {
                    culture.SetMaxLength(20);
                }

                culture.SetIsNullable(false);
            }

            Type parentType = translationInterface.GetGenericArguments()[0];
            IConventionEntityType? parentEntityType = modelBuilder.Metadata.FindEntityType(parentType);
            IConventionProperty? parentId = entityType.FindProperty(nameof(ITranslation.ParentId));

            if (parentEntityType is not null && parentId is not null)
            {
                IConventionForeignKeyBuilder? fkBuilder = entityType.Builder.HasRelationship(
                    parentEntityType, [parentId], parentEntityType.FindPrimaryKey()!);
                fkBuilder?.Metadata.SetDeleteBehavior(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);
                fkBuilder?.Metadata.SetDependentToPrincipal(
                    entityType.ClrType.GetProperty(nameof(ITranslation<Entity>.Parent)));
            }

            IConventionProperty? cultureProperty = entityType.FindProperty(nameof(ITranslation.Culture));
            if (parentId is not null && cultureProperty is not null)
            {
                IConventionProperty[] indexProperties = [parentId, cultureProperty];
                bool exists = entityType.GetIndexes().Any(i =>
                    i.Properties.Select(p => p.Name).SequenceEqual(indexProperties.Select(p => p.Name)));
                if (!exists)
                {
                    entityType.Builder.HasIndex(indexProperties)?.Metadata.SetIsUnique(true);
                }
            }
        }
    }
}
