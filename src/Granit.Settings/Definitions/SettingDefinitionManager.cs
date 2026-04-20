namespace Granit.Settings.Definitions;

/// <summary>
/// Centralized registry of all setting definitions declared by modules.
/// </summary>
public sealed class SettingDefinitionManager
{
    private readonly IReadOnlyDictionary<string, SettingDefinition> _definitions;

    public SettingDefinitionManager(IEnumerable<ISettingDefinitionProvider> providers)
    {
        SettingDefinitionContext context = new();
        foreach (ISettingDefinitionProvider provider in providers)
        {
            provider.Define(context);
        }
        _definitions = context.Build();

        foreach (SettingDefinition definition in _definitions.Values)
        {
            definition.ValidateInvariants();
        }
    }

    /// <summary>
    /// Returns the definition for the given name.
    /// </summary>
    /// <exception cref="InvalidOperationException">If the setting is not declared.</exception>
    public SettingDefinition Get(string name) =>
        _definitions.TryGetValue(name, out SettingDefinition? def)
            ? def
            : throw new InvalidOperationException($"Setting '{name}' is not declared. Ensure an ISettingDefinitionProvider registers it.");

    /// <summary>Returns the definition or <c>null</c> if unknown.</summary>
    public SettingDefinition? GetOrNull(string name) =>
        _definitions.GetValueOrDefault(name);

    /// <summary>Returns all declared definitions.</summary>
    public IReadOnlyCollection<SettingDefinition> GetAll() =>
        (IReadOnlyCollection<SettingDefinition>)_definitions.Values;

    private sealed class SettingDefinitionContext : ISettingDefinitionContext
    {
        private readonly Dictionary<string, SettingDefinition> _defs = [];

        public void Add(SettingDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);
            _defs[definition.Name] = definition;
        }

        public SettingDefinition? GetOrNull(string name) =>
            _defs.GetValueOrDefault(name);

        public System.Collections.ObjectModel.ReadOnlyDictionary<string, SettingDefinition> Build() =>
            _defs.AsReadOnly();
    }
}
