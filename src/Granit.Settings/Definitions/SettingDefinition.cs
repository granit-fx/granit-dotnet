using System.Globalization;
using System.Text.Json;

namespace Granit.Settings.Definitions;

/// <summary>
/// Describes a system setting (static metadata).
/// </summary>
public sealed class SettingDefinition
{
    /// <summary>Unique setting name (lookup key).</summary>
    public string Name { get; }

    /// <summary>Default value returned when no provider supplies a value.</summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Indicates whether the value must be encrypted at rest (via IStringEncryptionService).
    /// The cache stores plain text — encryption applies only at the store layer.
    /// </summary>
    public bool IsEncrypted { get; init; }

    /// <summary>When true, the value can be exposed to clients (public API).</summary>
    public bool IsVisibleToClients { get; init; }

    /// <summary>
    /// When true (default), a lower-priority provider inherits the value from a higher-priority one
    /// when its own value is null. E.g. Tenant inherits Global when IsInherited = true.
    /// </summary>
    public bool IsInherited { get; init; } = true;

    /// <summary>
    /// Allow-list of provider names authorized to store this setting.
    /// Empty list = all providers are authorized.
    /// </summary>
    public IList<string> Providers { get; } = [];

    /// <summary>Display label (UI).</summary>
    public string? DisplayName { get; init; }

    /// <summary>Long description of the setting (UI, documentation).</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Shape of the value (how consumers interpret the raw string). Default is <see cref="ValueKind.String"/>.
    /// Orthogonal to <see cref="IsEncrypted"/>.
    /// </summary>
    public ValueKind ValueKind { get; init; } = ValueKind.String;

    /// <summary>
    /// Optional closed list of permitted values. When non-empty, any write must match one of these
    /// entries (string equality). Applicable to any <see cref="ValueKind"/> (e.g. an Int setting
    /// constrained to <c>["1","5","10"]</c>). Dynamic option lists (cultures, timezones) should be
    /// served by a dedicated module endpoint instead.
    /// </summary>
    public IReadOnlyList<string>? AllowedValues { get; init; }

    public SettingDefinition(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// Verifies configuration-time invariants. Invoked by
    /// <see cref="SettingDefinitionManager"/> at registration to fail fast on misconfigured
    /// definitions.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="DefaultValue"/> is not parseable as <see cref="ValueKind"/>, or
    /// is not a member of a non-empty <see cref="AllowedValues"/>.
    /// </exception>
    public void ValidateInvariants()
    {
        if (DefaultValue is not null && !TryParseAs(DefaultValue, ValueKind))
        {
            throw new InvalidOperationException(
                $"Setting '{Name}': DefaultValue '{DefaultValue}' is not parseable as {ValueKind}.");
        }

        if (AllowedValues is { Count: > 0 } &&
            DefaultValue is not null &&
            !AllowedValues.Contains(DefaultValue))
        {
            throw new InvalidOperationException(
                $"Setting '{Name}': DefaultValue '{DefaultValue}' is not a member of AllowedValues.");
        }

        if (AllowedValues is { Count: > 0 })
        {
            foreach (string allowed in AllowedValues)
            {
                if (!TryParseAs(allowed, ValueKind))
                {
                    throw new InvalidOperationException(
                        $"Setting '{Name}': AllowedValues entry '{allowed}' is not parseable as {ValueKind}.");
                }
            }
        }
    }

    /// <summary>
    /// Checks whether <paramref name="value"/> is acceptable for this definition —
    /// i.e. parseable as <see cref="ValueKind"/> and, if <see cref="AllowedValues"/> is
    /// non-empty, a member of it. <see langword="null"/> is always acceptable (clears the
    /// override and falls back to the default / higher-priority provider).
    /// </summary>
    public bool IsValidValue(string? value)
    {
        if (value is null)
        {
            return true;
        }

        if (!TryParseAs(value, ValueKind))
        {
            return false;
        }

        if (AllowedValues is { Count: > 0 } && !AllowedValues.Contains(value))
        {
            return false;
        }

        return true;
    }

    internal static bool TryParseAs(string value, ValueKind kind) => kind switch
    {
        ValueKind.String => true,
        ValueKind.Bool => bool.TryParse(value, out _),
        ValueKind.Int => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
        ValueKind.Double => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
        ValueKind.Json => TryParseJson(value),
        _ => false,
    };

    private static bool TryParseJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
