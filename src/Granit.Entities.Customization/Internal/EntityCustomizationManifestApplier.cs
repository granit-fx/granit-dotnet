using Granit.Entities.Customization.Domain;
using Granit.Entities.Customization.Domain.Deltas;
using Granit.Entities.Internal;
using Granit.Entities.Manifests;
using Granit.MultiTenancy;

namespace Granit.Entities.Customization.Internal;

/// <summary>
/// Real <see cref="IManifestCustomizationApplier"/> — applies the active
/// <see cref="EntityCustomization"/> for the current tenant on top of the
/// composed manifest, mutating the form layout and tagging affected fields
/// with <see cref="EntityProvenance"/>.
/// </summary>
/// <remarks>
/// <para>
/// Targets <see cref="LayoutKind.FormDefault"/> in v1: only the default form
/// variant is customizable through the manifest pipeline. Other layout kinds
/// (List / Calendar / Gallery / DetailDefault) load their layout descriptors
/// from separate composition paths — those will gain customization in
/// follow-up stories without touching this applier.
/// </para>
/// <para>
/// Applies in declaration order — same as the persisted delta order. Reorder
/// targets the within-section position; cross-section moves are expressed as
/// <see cref="RegroupDelta"/>. Hidden fields are dropped from the section
/// they belong to and accumulated into <see cref="EntityFormManifest.HiddenByOverride"/>
/// so the dev-mode field-inspector overlay can render the "hidden by tenant"
/// badge.
/// </para>
/// </remarks>
internal sealed class EntityCustomizationManifestApplier(
    IEntityCustomizationReader reader,
    ICurrentTenant currentTenant) : IManifestCustomizationApplier
{
    public async Task<EntityManifestResponse> ApplyAsync(
        string entityName,
        EntityManifestResponse composedManifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentNullException.ThrowIfNull(composedManifest);

        if (composedManifest.Forms is null || composedManifest.Forms.Count == 0)
        {
            return composedManifest;
        }

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        EntityCustomization? customization = await reader.GetAsync(
            entityName, LayoutKind.FormDefault, tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (customization is null || customization.Deltas.Count == 0)
        {
            return composedManifest;
        }

        // Only the first form variant ("default") is customized in v1.
        EntityFormManifest defaultForm = composedManifest.Forms[0];
        EntityFormManifest customizedForm = ApplyToForm(defaultForm, customization);

        List<EntityFormManifest> newForms = [.. composedManifest.Forms];
        newForms[0] = customizedForm;

        return composedManifest with { Forms = newForms };
    }

    private static EntityFormManifest ApplyToForm(EntityFormManifest form, EntityCustomization customization)
    {
        // Mutable working state — section key → ordered field list.
        var bySection =
            form.Sections.ToDictionary(s => s.Key, s => s.Fields.ToList(), StringComparer.Ordinal);
        HashSet<string> hidden = new(StringComparer.Ordinal);

        EntityProvenance provenance = new(
            EntityProvenanceLayers.TenantCustomization, customization.Id);

        foreach (LayoutDelta delta in customization.Deltas)
        {
            (string sectionKey, int index, EntityFormFieldManifest field)? located = LocateField(bySection, delta.FieldName);
            if (located is null)
            {
                // Dangling reference — silently drop per ADR-053 §6 (composer is
                // the authoritative validator; B3 endpoint validates new writes
                // but historic deltas may reference a since-removed field).
                continue;
            }

            (string fromSection, int fromIndex, EntityFormFieldManifest target) = located.Value;

            switch (delta)
            {
                case HideDelta:
                    bySection[fromSection].RemoveAt(fromIndex);
                    hidden.Add(target.PropertyName);
                    break;

                case RegroupDelta regroup:
                    if (!bySection.TryGetValue(regroup.GroupKey, out List<EntityFormFieldManifest>? targetSection))
                    {
                        // Unknown target group — drop silently (defense in depth).
                        continue;
                    }
                    bySection[fromSection].RemoveAt(fromIndex);
                    targetSection.Add(target with { Provenance = provenance });
                    break;

                case ReorderDelta reorder:
                    bySection[fromSection].RemoveAt(fromIndex);
                    int newIndex = ResolveAnchorIndex(bySection[fromSection], reorder);
                    bySection[fromSection].Insert(newIndex, target with { Provenance = provenance });
                    break;
            }
        }

        List<EntityFormSectionManifest> newSections = new(form.Sections.Count);
        foreach (EntityFormSectionManifest section in form.Sections)
        {
            List<EntityFormFieldManifest> fields = bySection[section.Key];
            if (fields.Count == 0 && section.OwnedCollection is null)
            {
                // Section emptied by hides — drop to keep parity with the
                // existing "no surviving field → drop section" behaviour.
                continue;
            }
            newSections.Add(section with { Fields = fields });
        }

        return form with
        {
            Sections = newSections,
            HiddenByOverride = hidden.Count == 0 ? null : [.. hidden],
        };
    }

    private static (string sectionKey, int index, EntityFormFieldManifest field)? LocateField(
        Dictionary<string, List<EntityFormFieldManifest>> bySection,
        string fieldName)
    {
        foreach ((string sectionKey, List<EntityFormFieldManifest> fields) in bySection)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (string.Equals(fields[i].PropertyName, fieldName, StringComparison.Ordinal))
                {
                    return (sectionKey, i, fields[i]);
                }
            }
        }
        return null;
    }

    private static int ResolveAnchorIndex(List<EntityFormFieldManifest> fields, ReorderDelta reorder)
    {
        if (reorder.BeforeFieldName is { } before)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (string.Equals(fields[i].PropertyName, before, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return fields.Count;
        }

        if (reorder.AfterFieldName is { } after)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (string.Equals(fields[i].PropertyName, after, StringComparison.Ordinal))
                {
                    return i + 1;
                }
            }
            return fields.Count;
        }

        return fields.Count;
    }
}
