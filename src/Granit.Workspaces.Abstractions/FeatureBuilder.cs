namespace Granit.Workspaces;

/// <summary>
/// Fluent builder used by <see cref="IFeatureProvider"/> implementations to
/// describe a feature (per ADR-057). The builder collects the catalog entry
/// fields and produces an immutable <see cref="FeatureDescriptor"/> at boot.
/// </summary>
public sealed class FeatureBuilder
{
    private readonly string _name;
    private string? _permission;
    private string? _routeName;
    private string? _defaultIcon;
    private string? _displayKey;

    /// <summary>
    /// Constructs a new builder for the supplied feature name. Public so
    /// tests and tooling can capture provider invocations without going
    /// through <see cref="IFeatureCatalogBuilder"/>; production code rarely
    /// needs to instantiate one directly.
    /// </summary>
    public FeatureBuilder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
    }

    /// <summary>
    /// Permission gate that controls feature visibility. The composer drops the
    /// feature from the tree for users who don't hold it (ADR-040 §6).
    /// </summary>
    public FeatureBuilder Permission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        _permission = permission;
        return this;
    }

    /// <summary>
    /// Logical frontend route identifier (per ADR-057 §5). When omitted the
    /// route name falls back to the feature name — the convention covers ~95 %
    /// of cases.
    /// </summary>
    public FeatureBuilder RouteName(string routeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeName);
        _routeName = routeName;
        return this;
    }

    /// <summary>Default Lucide icon. Per-placement overrides win.</summary>
    public FeatureBuilder DefaultIcon(string icon)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icon);
        _defaultIcon = icon;
        return this;
    }

    /// <summary>
    /// Localisation key for the feature label (e.g.
    /// <c>"InvoicingEndpoints:Invoices.List"</c>). Mandatory in all 18 cultures.
    /// </summary>
    public FeatureBuilder DisplayKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _displayKey = key;
        return this;
    }

    /// <summary>
    /// Materialises the configured fields into an immutable
    /// <see cref="FeatureDescriptor"/>. Throws when permission or display key
    /// are missing — those two are non-optional per the framework convention.
    /// </summary>
    public FeatureDescriptor Build()
    {
        if (string.IsNullOrWhiteSpace(_permission))
        {
            throw new InvalidOperationException(
                $"Feature '{_name}' must declare a permission via .Permission(...). " +
                "Features without permissions are not supported (see ADR-057).");
        }
        if (string.IsNullOrWhiteSpace(_displayKey))
        {
            throw new InvalidOperationException(
                $"Feature '{_name}' must declare a display key via .DisplayKey(...). " +
                "Features without a label are not renderable.");
        }

        return new FeatureDescriptor(
            _name,
            _permission,
            _routeName ?? _name,
            _defaultIcon,
            _displayKey);
    }
}
