using System.Globalization;
using Granit.Features.ValueTypes;

namespace Granit.Features.Definitions;

/// <summary>
/// Groups related <see cref="FeatureDefinition"/> instances under a named category.
/// </summary>
/// <remarks>
/// Created by <see cref="IFeatureDefinitionContext.AddGroup"/> inside a
/// <see cref="IFeatureDefinitionProvider.Define"/> implementation.
/// Use the fluent <c>AddToggle</c>, <c>AddNumeric</c>, and <c>AddSelection</c>
/// methods to declare individual features.
/// </remarks>
public sealed class FeatureGroupDefinition
{
    /// <summary>Group name (e.g. <c>"Acme"</c>).</summary>
    public string Name { get; }

    /// <summary>Display label (for admin UI).</summary>
    public string? DisplayName { get; }

    private readonly List<FeatureDefinition> _features = [];

    /// <summary>Features declared in this group.</summary>
    public IReadOnlyList<FeatureDefinition> Features => _features;

    internal FeatureGroupDefinition(string name, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        DisplayName = displayName;
    }

    /// <summary>
    /// Declares a boolean feature with a <c>"true"</c>/<c>"false"</c> default.
    /// </summary>
    /// <param name="name">Unique feature name (e.g. <c>"Acme.VideoConference"</c>).</param>
    /// <param name="defaultValue">Default value.</param>
    /// <param name="displayName">Optional display label.</param>
    /// <returns>This group for chaining.</returns>
    public FeatureGroupDefinition AddToggle(
        string name,
        bool defaultValue = false,
        string? displayName = null)
    {
        _features.Add(new FeatureDefinition(
            name,
            defaultValue ? "true" : "false",
            FeatureValueType.Toggle)
        {
            DisplayName = displayName,
        });
        return this;
    }

    /// <summary>
    /// Declares an integer feature with optional min/max bounds.
    /// </summary>
    /// <param name="name">Unique feature name (e.g. <c>"Acme.MaxUsersCount"</c>).</param>
    /// <param name="defaultValue">Default integer value.</param>
    /// <param name="min">Minimum allowed value (inclusive, default 0).</param>
    /// <param name="max">Maximum allowed value (inclusive, default <see cref="long.MaxValue"/>).</param>
    /// <param name="displayName">Optional display label.</param>
    /// <returns>This group for chaining.</returns>
    public FeatureGroupDefinition AddNumeric(
        string name,
        long defaultValue,
        long min = 0,
        long max = long.MaxValue,
        string? displayName = null)
    {
        _features.Add(new FeatureDefinition(
            name,
            defaultValue.ToString(CultureInfo.InvariantCulture),
            FeatureValueType.Numeric)
        {
            DisplayName = displayName,
            NumericConstraint = new NumericConstraint(min, max),
        });
        return this;
    }

    /// <summary>
    /// Declares a selection feature constrained to a fixed set of allowed values.
    /// </summary>
    /// <param name="name">Unique feature name.</param>
    /// <param name="defaultValue">Default value (must be in <paramref name="allowedValues"/>).</param>
    /// <param name="allowedValues">Exhaustive list of accepted values.</param>
    /// <param name="displayName">Optional display label.</param>
    /// <returns>This group for chaining.</returns>
    public FeatureGroupDefinition AddSelection(
        string name,
        string defaultValue,
        string[] allowedValues,
        string? displayName = null)
    {
        _features.Add(new FeatureDefinition(
            name,
            defaultValue,
            FeatureValueType.Selection)
        {
            DisplayName = displayName,
            SelectionValues = new SelectionValues([.. allowedValues]),
        });
        return this;
    }
}
