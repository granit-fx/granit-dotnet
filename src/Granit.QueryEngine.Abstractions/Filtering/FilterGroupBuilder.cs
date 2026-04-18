using System.Linq.Expressions;

namespace Granit.QueryEngine.Filtering;

/// <summary>
/// Fluent builder for declaring presets within a filter group.
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
public sealed class FilterGroupBuilder<TEntity> where TEntity : class
{
    internal List<PresetDescriptor> Presets { get; } = [];

    /// <summary>
    /// Adds a preset to this filter group.
    /// </summary>
    /// <param name="name">Unique name of the preset.</param>
    /// <param name="predicate">The predicate expression to apply.</param>
    /// <param name="isDefault">Whether this preset is active by default.</param>
    public FilterGroupBuilder<TEntity> Preset(
        string name,
        Expression<Func<TEntity, bool>> predicate,
        bool isDefault = false)
    {
        Presets.Add(new PresetDescriptor
        {
            Name = name,
            Predicate = predicate,
            IsDefault = isDefault,
        });

        return this;
    }

    /// <summary>
    /// Adds a preset with a custom label to this filter group.
    /// </summary>
    /// <param name="name">Unique name of the preset.</param>
    /// <param name="label">User-facing label.</param>
    /// <param name="predicate">The predicate expression to apply.</param>
    /// <param name="isDefault">Whether this preset is active by default.</param>
    public FilterGroupBuilder<TEntity> Preset(
        string name,
        string label,
        Expression<Func<TEntity, bool>> predicate,
        bool isDefault = false)
    {
        Presets.Add(new PresetDescriptor
        {
            Name = name,
            Label = label,
            Predicate = predicate,
            IsDefault = isDefault,
        });

        return this;
    }
}
