using System.Reflection;
using Granit.ArchitectureTests.Internal;
using Granit.Domain.ValueObjects;
using Granit.Entities;
using Granit.Entities.Layouts;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Boot-time invariant: every entity exposing a gallery layout must reference an
/// <see cref="GalleryLayoutDescriptor.ImagePropertyName"/> that resolves to a
/// <see cref="BlobReference"/> (or <c>BlobReference?</c>) property on the entity.
/// The DSL signature already enforces this at compile time
/// (<c>Expression&lt;Func&lt;TEntity, BlobReference?&gt;&gt;</c>), but the archi test
/// keeps the invariant honest against any future code path that builds an
/// <c>EntityDefinition</c> through reflection or test fixtures.
/// </summary>
/// <remarks>
/// Mirrors the layered guard pattern from <see cref="GalleryFieldWhitelistPairingTests"/> —
/// type check here, whitelist pairing there.
/// </remarks>
public sealed class GalleryImageFieldTypeTests
{
    [Fact]
    public void Every_gallery_ImageField_must_be_a_BlobReference_property()
    {
        List<IEntityDefinitionDescriptor> entities = EntityDefinitionScan.ScanEntityDefinitions();
        List<string> violations = [];

        foreach (IEntityDefinitionDescriptor entity in entities)
        {
            EntityDefinitionDescriptor descriptor = entity.Descriptor;
            GalleryLayoutDescriptor? gallery = descriptor.ListLayouts
                .OfType<GalleryLayoutDescriptor>()
                .FirstOrDefault();

            if (gallery is null)
            {
                continue;
            }

            PropertyInfo? imageProperty = descriptor.EntityType.GetProperty(
                gallery.ImagePropertyName,
                BindingFlags.Instance | BindingFlags.Public);

            if (imageProperty is null)
            {
                violations.Add(
                    $"Entity '{descriptor.Name}' gallery ImageField '{gallery.ImagePropertyName}' is not a public instance property on '{descriptor.EntityType.FullName}'. "
                    + "The descriptor was likely built for a different type, or the property was renamed since the EntityDefinition was declared.");
                continue;
            }

            if (imageProperty.PropertyType != typeof(BlobReference))
            {
                violations.Add(
                    $"Entity '{descriptor.Name}' gallery ImageField '{gallery.ImagePropertyName}' is type '{imageProperty.PropertyType.FullName}', expected BlobReference (or nullable). "
                    + "Change the entity property to `BlobReference?` (the convention for opaque blob references — see src/Granit/Domain/ValueObjects/BlobReference.cs).");
            }
        }

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }
}
