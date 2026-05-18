using System.Linq.Expressions;
using System.Reflection;

namespace Granit.Entities.Details;

/// <summary>
/// Fluent builder for one detail-view section.
/// </summary>
/// <typeparam name="TEntity">The owning entity type.</typeparam>
public sealed class DetailSectionBuilder<TEntity>
{
    private readonly string _key;

    private readonly int _order;

    private string? _labelKey;
    private string? _inheritsFromFormVariant;
    private List<string>? _fields;

    internal DetailSectionBuilder(string key, int order)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _key = key;
        _order = order;
    }

    /// <summary>i18n key for the section header (resolved client-side).</summary>
    public DetailSectionBuilder<TEntity> Label(string labelKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(labelKey);
        _labelKey = labelKey;
        return this;
    }

    /// <summary>
    /// Inherit the section's structure from the named form variant. Mutually exclusive
    /// with <see cref="Field{TProperty}"/>.
    /// </summary>
    public DetailSectionBuilder<TEntity> InheritsFromForm(string variantName = "default")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variantName);
        if (_fields is { Count: > 0 })
        {
            throw new InvalidOperationException(
                "DetailSection cannot mix InheritsFromForm(...) with explicit Field(...) declarations.");
        }

        _inheritsFromFormVariant = variantName;
        return this;
    }

    /// <summary>
    /// Adds a free-form field reference to the section (mutually exclusive with
    /// <see cref="InheritsFromForm"/>).
    /// </summary>
    public DetailSectionBuilder<TEntity> Field<TProperty>(Expression<Func<TEntity, TProperty>> propertySelector)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);

        if (_inheritsFromFormVariant is not null)
        {
            throw new InvalidOperationException(
                "DetailSection cannot mix InheritsFromForm(...) with explicit Field(...) declarations.");
        }

        if (propertySelector.Body is not MemberExpression member
            || member.Member is not PropertyInfo property)
        {
            throw new ArgumentException(
                "Field selector must be a direct property access expression (e.g. x => x.Title).",
                nameof(propertySelector));
        }

        (_fields ??= []).Add(property.Name);
        return this;
    }

    internal DetailSectionDescriptor Build() =>
        new()
        {
            Key = _key,
            LabelKey = _labelKey,
            Order = _order,
            InheritsFromFormVariant = _inheritsFromFormVariant,
            Fields = _fields,
        };
}
