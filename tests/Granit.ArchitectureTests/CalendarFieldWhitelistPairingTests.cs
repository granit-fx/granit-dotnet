using Granit.ArchitectureTests.Internal;
using Granit.Entities;
using Granit.Entities.Layouts;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Calendar counterpart to <see cref="KanbanCardWhitelistPairingTests"/> — every
/// property a calendar surfaces (<c>StartPropertyName</c> + optional
/// <c>EndPropertyName</c> / <c>TitlePropertyName</c> / <c>ColorByPropertyName</c>)
/// must appear in the matching <c>QueryDefinition&lt;TEntity&gt;.GetColumns()</c>
/// whitelist. The range-query endpoint reads each event's start / end / title /
/// colour via the same projection — a non-whitelisted property either renders
/// blank or leaks data the security model intentionally hid.
/// </summary>
/// <remarks>
/// An entity with a calendar layout but NO <c>QueryDefinition</c> at all is a
/// separate hard failure (the calendar can't render) — also reported.
/// </remarks>
public sealed class CalendarFieldWhitelistPairingTests
{
    [Fact]
    public void Every_calendar_property_must_be_whitelisted_in_the_matching_QueryDefinition()
    {
        List<IEntityDefinitionDescriptor> entities = EntityDefinitionScan.ScanEntityDefinitions();
        Dictionary<Type, IQueryDefinitionDescriptor> queriesByEntityType = EntityDefinitionScan.ScanQueryDefinitions();

        List<string> violations = [];

        foreach (IEntityDefinitionDescriptor entity in entities)
        {
            EntityDefinitionDescriptor descriptor = entity.Descriptor;
            CalendarLayoutDescriptor? calendar = descriptor.ListLayouts
                .OfType<CalendarLayoutDescriptor>()
                .FirstOrDefault();

            if (calendar is null)
            {
                continue;
            }

            if (!queriesByEntityType.TryGetValue(descriptor.EntityType, out IQueryDefinitionDescriptor? query))
            {
                violations.Add(
                    $"Entity '{descriptor.Name}' declares a CalendarView but no matching QueryDefinition<{descriptor.EntityType.Name}> was found. "
                    + "The calendar reads its rows from the entity's QueryDefinition projection — without one, the board can't render. "
                    + "Add a QueryDefinition<T> in the same module's Queries/ folder and register it via AddQueryDefinition<T, TDefinition>().");
                continue;
            }

            HashSet<string> whitelistedProperties = EntityDefinitionScan.ReadColumnPropertyNames(query);

            CheckProperty(calendar.StartPropertyName, "StartField", required: true);
            CheckProperty(calendar.EndPropertyName, "EndField");
            CheckProperty(calendar.TitlePropertyName, "TitleField");
            CheckProperty(calendar.ColorByPropertyName, "ColorBy");

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
                        $"Entity '{descriptor.Name}' calendar {severity}{dslName}='{propertyName}' is not in the QueryDefinition column whitelist. "
                        + $"Add `.Column(e => e.{propertyName})` in the matching QueryDefinition or change the calendar {dslName} to a whitelisted property.");
                }
            }
        }

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }
}
