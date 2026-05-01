using Granit.ArchitectureTests.Internal;
using Granit.Entities;
using Granit.Entities.Forms;
using Granit.Entities.Layouts;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Closes the Phase 2.A.2 deferred guard: every property a kanban card surfaces
/// (<c>Card.TitleProperty</c> + <c>Card.Fields[*].PropertyName</c>) must appear
/// in the matching <c>QueryDefinition&lt;TEntity&gt;.GetColumns()</c> whitelist.
/// </summary>
/// <remarks>
/// The kanban tile reads its rows from the entity's <c>QueryDefinition</c> projection.
/// Card fields outside the whitelist either render blank (the SELECT does not include
/// the column) or — worse — leak a property the security model intentionally hid from
/// the list endpoint. Either way the host has a bug; this test fails fast at boot
/// instead of letting the regression reach production.
/// <para>
/// An entity with a kanban layout but NO <c>QueryDefinition</c> at all is a separate
/// hard failure (the kanban can't render) — also reported by this test.
/// </para>
/// </remarks>
public sealed class KanbanCardWhitelistPairingTests
{
    [Fact]
    public void Every_kanban_card_property_must_be_whitelisted_in_the_matching_QueryDefinition()
    {
        List<IEntityDefinitionDescriptor> entities = EntityDefinitionScan.ScanEntityDefinitions();
        Dictionary<Type, IQueryDefinitionDescriptor> queriesByEntityType = EntityDefinitionScan.ScanQueryDefinitions();

        List<string> violations = [];

        foreach (IEntityDefinitionDescriptor entity in entities)
        {
            EntityDefinitionDescriptor descriptor = entity.Descriptor;
            KanbanLayoutDescriptor? kanban = descriptor.ListLayouts
                .OfType<KanbanLayoutDescriptor>()
                .FirstOrDefault();

            if (kanban is null)
            {
                continue;
            }

            if (!queriesByEntityType.TryGetValue(descriptor.EntityType, out IQueryDefinitionDescriptor? query))
            {
                violations.Add(
                    $"Entity '{descriptor.Name}' declares a KanbanView but no matching QueryDefinition<{descriptor.EntityType.Name}> was found. "
                    + "The kanban tile reads its rows from the entity's QueryDefinition projection — without one, the board can't render. "
                    + "Add a QueryDefinition<T> in the same module's Queries/ folder and register it via AddQueryDefinition<T, TDefinition>().");
                continue;
            }

            HashSet<string> whitelistedProperties = EntityDefinitionScan.ReadColumnPropertyNames(query);

            if (kanban.Card.TitleProperty is { } title && !whitelistedProperties.Contains(title))
            {
                violations.Add(
                    $"Entity '{descriptor.Name}' kanban Card.Title='{title}' is not in the QueryDefinition column whitelist. "
                    + $"Add `.Column(e => e.{title})` in the matching QueryDefinition or change the kanban Title to a whitelisted property.");
            }

            foreach (FieldDescriptor field in kanban.Card.Fields)
            {
                if (!whitelistedProperties.Contains(field.PropertyName))
                {
                    violations.Add(
                        $"Entity '{descriptor.Name}' kanban Card.Field '{field.PropertyName}' is not in the QueryDefinition column whitelist. "
                        + $"Add `.Column(e => e.{field.PropertyName})` in the matching QueryDefinition or remove the field from the card.");
                }
            }
        }

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }
}
