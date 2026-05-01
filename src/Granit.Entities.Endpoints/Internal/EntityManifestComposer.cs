using Granit.Entities.Actions;
using Granit.Entities.Details;
using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.Forms;
using Granit.Entities.Layouts;
using Granit.Entities.Relations;

namespace Granit.Entities.Endpoints.Internal;

/// <summary>
/// Pure projection from <see cref="EntityDefinitionDescriptor"/> + the resolved
/// permission snapshot to the wire-shape <see cref="EntityManifestResponse"/>.
/// Defense-in-depth: fields, side panels, and form sections gated by a
/// permission the caller does NOT hold are dropped from the payload entirely
/// (per ADR-040 §6 and story #1549) — never just hidden.
/// </summary>
internal static class EntityManifestComposer
{
    /// <summary>Manifest schema version. Bump on breaking shape changes.</summary>
    public const int SchemaVersion = 1;

    public static EntityManifestResponse Compose(
        EntityDefinitionDescriptor definition,
        EntityPermissionSnapshot permissions,
        IReadOnlySet<string> grantedPermissions,
        EntityFacets facets,
        Guid? defaultViewId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(grantedPermissions);

        EntityIdentitySection? identity = facets.HasFlag(EntityFacets.Identity)
            ? ComposeIdentity(definition)
            : null;

        EntityPermissionsSection? perms = facets.HasFlag(EntityFacets.Permissions)
            ? ComposePermissions(permissions)
            : null;

        IReadOnlyList<EntityFormManifest>? forms = facets.HasFlag(EntityFacets.Forms)
            ? ComposeForms(definition, grantedPermissions)
            : null;

        IReadOnlyList<EntityDetailManifest>? details = facets.HasFlag(EntityFacets.Details)
            ? ComposeDetails(definition, grantedPermissions)
            : null;

        EntityCollectionsSection? collections =
            facets.HasFlag(EntityFacets.Collections)
            || facets.HasFlag(EntityFacets.Exports)
            || facets.HasFlag(EntityFacets.Dashboards)
                ? ComposeCollections(definition, grantedPermissions, defaultViewId)
                : null;

        IReadOnlyList<EntityRelationManifest>? relations = facets.HasFlag(EntityFacets.Relations)
            ? ComposeRelations(definition, grantedPermissions)
            : null;

        IReadOnlyList<EntityActionManifest>? actions = facets.HasFlag(EntityFacets.Actions)
            ? ComposeActions(definition, grantedPermissions)
            : null;

        return new EntityManifestResponse(
            SchemaVersion,
            identity,
            perms,
            forms,
            details,
            collections,
            relations,
            actions);
    }

    private static List<EntityActionManifest> ComposeActions(
        EntityDefinitionDescriptor d, IReadOnlySet<string> granted)
    {
        if (d.Actions.Count == 0)
        {
            return [];
        }

        List<EntityActionManifest> actions = new(d.Actions.Count);
        foreach (EntityActionDescriptor action in d.Actions)
        {
            if (action.RequiresPermission is { } perm && !granted.Contains(perm))
            {
                // Drop entirely — defense in depth (per ADR-040 §6).
                continue;
            }

            actions.Add(new EntityActionManifest(
                action.Name,
                action.Kind,
                action.DisplayKey,
                action.Icon,
                action.Order,
                action.UrlTemplate,
                action.HttpMethod,
                action.ConfirmationKey,
                action.WorkflowTransitionName,
                action.ContributorAssemblyName));
        }
        return actions;
    }

    private static List<EntityRelationManifest> ComposeRelations(
        EntityDefinitionDescriptor d, IReadOnlySet<string> granted)
    {
        if (d.Relations.Count == 0)
        {
            return [];
        }

        List<EntityRelationManifest> relations = new(d.Relations.Count);
        foreach (RelationDescriptor relation in d.Relations)
        {
            if (relation.RequiresPermission is { } perm && !granted.Contains(perm))
            {
                // Drop entirely — defense in depth (story #1562).
                continue;
            }

            List<EntityRelationAggregateManifest> aggregates = new(relation.Aggregates.Count);
            foreach (RelationAggregateDescriptor agg in relation.Aggregates)
            {
                aggregates.Add(new EntityRelationAggregateManifest(
                    agg.Kind, agg.PropertyName, agg.LabelKey, agg.Format));
            }

            relations.Add(new EntityRelationManifest(
                relation.Name,
                relation.Cardinality,
                relation.Display,
                relation.TargetEntityName,
                relation.DisplayKey,
                relation.Icon,
                relation.Order,
                relation.QueryDefinitionName,
                aggregates,
                relation.ContributorAssemblyName));
        }

        return relations;
    }

    private static EntityIdentitySection ComposeIdentity(EntityDefinitionDescriptor d) =>
        new(
            d.Name,
            d.EntityType.FullName ?? d.EntityType.Name,
            d.DisplayKey,
            d.Icon,
            d.PermissionGroup,
            d.DisplayProperty);

    private static EntityPermissionsSection ComposePermissions(EntityPermissionSnapshot s) =>
        new(s.CanRead, s.CanCreate, s.CanUpdate, s.CanDelete, s.CanManage, s.CanExecute);

    private static List<EntityFormManifest> ComposeForms(
        EntityDefinitionDescriptor d, IReadOnlySet<string> granted)
    {
        if (d.Forms.Count == 0)
        {
            return [];
        }

        List<EntityFormManifest> forms = new(d.Forms.Count);
        foreach (FormDescriptor form in d.Forms)
        {
            List<EntityFormSectionManifest> sections = new(form.Sections.Count);
            foreach (SectionDescriptor section in form.Sections)
            {
                if (section.OwnedCollection is { } owned)
                {
                    EntityFormSectionManifest? ownedSection = ComposeOwnedCollectionSection(section, owned, granted);
                    if (ownedSection is not null)
                    {
                        sections.Add(ownedSection);
                    }
                    continue;
                }

                List<EntityFormFieldManifest> fields = FilterFields(section.Fields, granted);

                // A section with no surviving fields is dropped too — avoids
                // empty section headers when the user lacks permission for
                // every field in it.
                if (fields.Count == 0)
                {
                    continue;
                }

                sections.Add(new EntityFormSectionManifest(
                    section.Key,
                    section.LabelKey,
                    section.Order,
                    section.CollapsedByDefault,
                    fields));
            }

            forms.Add(new EntityFormManifest(form.Name, form.Customizable, sections));
        }

        return forms;
    }

    private static EntityFormSectionManifest? ComposeOwnedCollectionSection(
        SectionDescriptor section,
        OwnedCollectionDescriptor owned,
        IReadOnlySet<string> granted)
    {
        List<EntityFormFieldManifest> itemFields = FilterFields(owned.ItemFields, granted);

        // No surviving item field → drop the whole section (defense in depth).
        if (itemFields.Count == 0)
        {
            return null;
        }

        EntityFormOwnedCollectionManifest collection = new(
            owned.PropertyName,
            owned.ItemType.Name,
            itemFields,
            owned.ItemDisplayProperty,
            owned.MaxRendered);

        return new EntityFormSectionManifest(
            section.Key,
            section.LabelKey,
            section.Order,
            section.CollapsedByDefault,
            Fields: [],
            OwnedCollection: collection);
    }

    private static List<EntityFormFieldManifest> FilterFields(
        IReadOnlyList<FieldDescriptor> source,
        IReadOnlySet<string> granted)
    {
        List<EntityFormFieldManifest> fields = new(source.Count);
        foreach (FieldDescriptor field in source)
        {
            if (field.RequiresPermission is { } perm && !granted.Contains(perm))
            {
                continue;
            }

            fields.Add(new EntityFormFieldManifest(
                field.PropertyName,
                field.ClrType.Name,
                field.Widget,
                field.Config,
                field.LabelKey,
                field.HelpKey,
                field.Order,
                field.ReadOnly,
                field.VisibleIf));
        }
        return fields;
    }

    private static List<EntityDetailManifest> ComposeDetails(
        EntityDefinitionDescriptor d, IReadOnlySet<string> granted)
    {
        if (d.Details.Count == 0)
        {
            return [];
        }

        List<EntityDetailManifest> details = new(d.Details.Count);
        foreach (DetailDescriptor detail in d.Details)
        {
            List<EntityDetailSectionManifest> sections = new(detail.Sections.Count);
            foreach (DetailSectionDescriptor s in detail.Sections)
            {
                sections.Add(new EntityDetailSectionManifest(
                    s.Key,
                    s.LabelKey,
                    s.Order,
                    s.InheritsFromFormVariant,
                    s.Fields));
            }

            List<EntityDetailSidePanelManifest> sidePanels = new(detail.SidePanels.Count);
            foreach (SidePanelDescriptor panel in detail.SidePanels)
            {
                if (panel.RequiresPermission is { } perm
                    && !granted.Contains(perm))
                {
                    continue;
                }

                sidePanels.Add(new EntityDetailSidePanelManifest(panel.Kind, panel.Order));
            }

            details.Add(new EntityDetailManifest(detail.Name, sections, sidePanels));
        }

        return details;
    }

    private static EntityCollectionsSection ComposeCollections(
        EntityDefinitionDescriptor d,
        IReadOnlySet<string> granted,
        Guid? defaultViewId)
    {
        EntityCollectionReference? query = d.QueryDefinitionType is { } q
            ? new EntityCollectionReference(InferDefinitionName(q), q.Name)
            : null;

        EntityCollectionReference? export = d.ExportDefinitionType is { } e
            ? new EntityCollectionReference(InferDefinitionName(e), e.Name)
            : null;

        IReadOnlyList<EntityCollectionReference> metrics = d.MetricDefinitionTypes
            .Select(t => new EntityCollectionReference(InferDefinitionName(t), t.Name))
            .ToList();

        IReadOnlyList<EntityCollectionReference> dashboards = d.DashboardDefinitionTypes
            .Select(t => new EntityCollectionReference(InferDefinitionName(t), t.Name))
            .ToList();

        IReadOnlyList<EntityListLayoutManifest> layouts = ComposeListLayouts(d.ListLayouts, granted);

        return new EntityCollectionsSection(query, export, metrics, dashboards, defaultViewId, layouts);
    }

    private static List<EntityListLayoutManifest> ComposeListLayouts(
        IReadOnlyList<EntityListLayoutDescriptor> source,
        IReadOnlySet<string> granted)
    {
        List<EntityListLayoutManifest> layouts = new(source.Count);
        foreach (EntityListLayoutDescriptor layout in source)
        {
            if (layout.RequiresPermission is { } perm && !granted.Contains(perm))
            {
                // Drop entirely — defense in depth.
                continue;
            }

            EntityKanbanLayoutManifest? kanban = layout switch
            {
                KanbanLayoutDescriptor k => ComposeKanban(k, granted),
                _ => null,
            };

            // Layout produced no usable shape (e.g. kanban whose card lost every
            // field to permission filtering). Drop the layout — empty switcher
            // tabs would be UX clutter.
            if (kanban is null && layout.Kind == EntityListLayoutKind.Kanban)
            {
                continue;
            }

            layouts.Add(new EntityListLayoutManifest(
                layout.Kind,
                layout.IsDefault,
                kanban));
        }
        return layouts;
    }

    private static EntityKanbanLayoutManifest? ComposeKanban(
        KanbanLayoutDescriptor descriptor,
        IReadOnlySet<string> granted)
    {
        List<EntityFormFieldManifest> cardFields = FilterFields(descriptor.Card.Fields, granted);

        // A kanban with no surviving body field is still useful when the title
        // fallback (entity DisplayProperty) carries the headline; only drop if
        // the explicit Title got filtered AND no body field survives.
        // (Title filtering is symmetric with form field permissions.)

        EntityKanbanCardManifest card = new(descriptor.Card.TitleProperty, cardFields);

        IReadOnlyList<EntityKanbanColumnManifest> columns = [.. descriptor.Columns
            .Select(c => new EntityKanbanColumnManifest(c.Value, c.Color, c.DefaultState))];

        return new EntityKanbanLayoutManifest(
            descriptor.GroupByPropertyName,
            descriptor.GroupByClrType.Name,
            card,
            columns);
    }

    /// <summary>
    /// Strips the conventional <c>Definition</c> suffix from a CLR type name to
    /// surface the wire identifier that the matching <c>Granit.{Module}</c>
    /// module declares (e.g. <c>InvoiceQueryDefinition</c> →
    /// <c>"Granit.Invoicing.InvoiceQuery"</c>). When a definition is registered
    /// with a different wire <c>Name</c> than its CLR type implies, the
    /// renderer reconciles via the registry — this is a hint, not a contract.
    /// </summary>
    private static string InferDefinitionName(Type t)
    {
        string fullName = t.FullName ?? t.Name;
        const string Suffix = "Definition";

        return fullName.EndsWith(Suffix, StringComparison.Ordinal)
            ? fullName[..^Suffix.Length]
            : fullName;
    }
}
