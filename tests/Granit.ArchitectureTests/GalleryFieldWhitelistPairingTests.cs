using Granit.ArchitectureTests.Internal;
using Granit.Entities;
using Granit.Entities.Layouts;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Gallery counterpart to <see cref="CalendarFieldWhitelistPairingTests"/> — every
/// property a gallery card surfaces (<c>ImagePropertyName</c> + optional
/// <c>TitlePropertyName</c> / <c>SubtitlePropertyName</c>) must appear in the
/// matching <c>QueryDefinition&lt;TEntity&gt;.GetColumns()</c> whitelist. The
/// renderer reads each card's image / title / subtitle via the same projection —
/// a non-whitelisted property either renders blank or leaks data the security
/// model intentionally hid.
/// </summary>
/// <remarks>
/// An entity with a gallery layout but NO <c>QueryDefinition</c> at all is a
/// separate hard failure (the gallery can't render) — also reported.
/// </remarks>
public sealed class GalleryFieldWhitelistPairingTests
{
    [Fact]
    public void Every_gallery_property_must_be_whitelisted_in_the_matching_QueryDefinition()
    {
        List<IEntityDefinitionDescriptor> entities = EntityDefinitionScan.ScanEntityDefinitions();
        Dictionary<Type, IQueryDefinitionDescriptor> queriesByEntityType = EntityDefinitionScan.ScanQueryDefinitions();

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

            if (!queriesByEntityType.TryGetValue(descriptor.EntityType, out IQueryDefinitionDescriptor? query))
            {
                violations.Add(
                    $"Entity '{descriptor.Name}' declares a GalleryView but no matching QueryDefinition<{descriptor.EntityType.Name}> was found. "
                    + "The gallery reads its rows from the entity's QueryDefinition projection — without one, the grid can't render. "
                    + "Add a QueryDefinition<T> in the same module's Queries/ folder and register it via AddQueryDefinition<T, TDefinition>().");
                continue;
            }

            HashSet<string> whitelistedProperties = EntityDefinitionScan.ReadColumnPropertyNames(query);

            CheckProperty(gallery.ImagePropertyName, "ImageField", required: true);
            CheckProperty(gallery.TitlePropertyName, "TitleField");
            CheckProperty(gallery.SubtitlePropertyName, "SubtitleField");

            void CheckProperty(string? propertyName, string dslName, bool required = false)
            {
                if (propertyName is null)
                {
                    return;
                }

                if (!whitelistedProperties.Contains(propertyName))
                {
                    string severity = required ? "required " : string.Empty;
                    violations.Add(
                        $"Entity '{descriptor.Name}' gallery {severity}{dslName}='{propertyName}' is not in the QueryDefinition column whitelist. "
                        + $"Add `.Column(e => e.{propertyName})` in the matching QueryDefinition or change the gallery {dslName} to a whitelisted property.");
                }
            }
        }

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }
}
