using Granit.Entities.Customization.Domain;
using Granit.Entities.Customization.Domain.Deltas;
using Granit.Entities.Forms;

namespace Granit.Entities.Customization.Internal;

/// <summary>
/// Validates a delta payload against the compiled
/// <see cref="EntityDefinitionDescriptor"/> resolved through
/// <see cref="IEntityDefinitionRegistry"/>. Shape-level invariants are
/// enforced upstream by the FluentValidation validator; this layer enforces
/// the **semantic** rules from ADR-053:
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>FieldName</c> resolves in the compiled layout (form / detail).</item>
///   <item><see cref="ReorderDelta"/> anchors (<c>Before</c> / <c>After</c>) reference compiled fields.</item>
///   <item><see cref="RegroupDelta.GroupKey"/> resolves in the compiled section catalogue.</item>
/// </list>
/// <para>
/// For <see cref="LayoutKind.List"/> / <see cref="LayoutKind.Calendar"/> /
/// <see cref="LayoutKind.Gallery"/> the descriptor exposes the compiled types
/// only as <see cref="Type"/> references; semantic validation against those
/// layouts is deferred to the manifest composer (story B4) which has the
/// concrete descriptor instance. This layer rejects only the form / detail
/// dangling-field cases that we can detect from <c>EntityDefinitionDescriptor</c>
/// alone.
/// </para>
/// </remarks>
internal sealed class DescriptorDeltaValidator(IEntityDefinitionRegistry registry)
{
    public ValidationResult Validate(string entityName, LayoutKind layoutKind, IReadOnlyList<LayoutDelta> deltas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentNullException.ThrowIfNull(deltas);

        IEntityDefinitionDescriptor? entity = registry.GetByName(entityName);
        if (entity is null)
        {
            return ValidationResult.Failure($"Unknown entity '{entityName}'.");
        }

        (HashSet<string> fields, HashSet<string> groups) = ResolveCatalogues(entity.Descriptor, layoutKind);

        if (fields.Count == 0)
        {
            // Layout kind not statically resolvable from EntityDefinitionDescriptor
            // (List / Calendar / Gallery); accept the deltas — composer (B4) is the
            // authoritative validator.
            return ValidationResult.Success;
        }

        List<string> errors = [];
        foreach (LayoutDelta delta in deltas)
        {
            if (!fields.Contains(delta.FieldName))
            {
                errors.Add($"Field '{delta.FieldName}' does not exist on layout '{layoutKind}' of entity '{entityName}'.");
                continue;
            }
            switch (delta)
            {
                case ReorderDelta reorder:
                    if (reorder.BeforeFieldName is not null && !fields.Contains(reorder.BeforeFieldName))
                    {
                        errors.Add($"ReorderDelta anchor '{reorder.BeforeFieldName}' does not exist on layout '{layoutKind}' of entity '{entityName}'.");
                    }
                    if (reorder.AfterFieldName is not null && !fields.Contains(reorder.AfterFieldName))
                    {
                        errors.Add($"ReorderDelta anchor '{reorder.AfterFieldName}' does not exist on layout '{layoutKind}' of entity '{entityName}'.");
                    }
                    break;

                case RegroupDelta regroup:
                    if (!groups.Contains(regroup.GroupKey))
                    {
                        errors.Add($"RegroupDelta target group '{regroup.GroupKey}' is not declared on layout '{layoutKind}' of entity '{entityName}'.");
                    }
                    break;
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success
            : ValidationResult.Failure(string.Join(' ', errors));
    }

    private static (HashSet<string> fields, HashSet<string> groups) ResolveCatalogues(
        EntityDefinitionDescriptor descriptor, LayoutKind layoutKind)
    {
        HashSet<string> fields = new(StringComparer.Ordinal);
        HashSet<string> groups = new(StringComparer.Ordinal);

        switch (layoutKind)
        {
            case LayoutKind.FormDefault:
                FormDescriptor? form = null;
                foreach (FormDescriptor candidate in descriptor.Forms)
                {
                    if (candidate.Name == "default")
                    {
                        form = candidate;
                        break;
                    }
                    form ??= candidate;
                }
                if (form is not null)
                {
                    foreach (SectionDescriptor section in form.Sections)
                    {
                        groups.Add(section.Key);
                        foreach (FieldDescriptor field in section.Fields)
                        {
                            fields.Add(field.PropertyName);
                        }
                    }
                }
                break;

            case LayoutKind.DetailDefault:
                // Detail descriptor exposes a similar (sections + fields) shape; the
                // mapping is handled by the manifest composer (B4) — for now, defer
                // semantic validation by leaving the catalogue empty so the validator
                // takes the "trust + composer enforces" path.
                break;
        }

        return (fields, groups);
    }
}

/// <summary>Result of <see cref="DescriptorDeltaValidator"/>.</summary>
internal readonly record struct ValidationResult(bool IsValid, string? Error)
{
    public static ValidationResult Success => new(true, null);
    public static ValidationResult Failure(string error) => new(false, error);
}
