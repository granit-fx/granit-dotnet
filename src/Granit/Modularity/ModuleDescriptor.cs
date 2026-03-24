namespace Granit.Modularity;

/// <summary>
/// Descripteur interne associant un type de module a son instance
/// et ses dependances declarees via <see cref="DependsOnAttribute"/>.
/// </summary>
internal sealed class ModuleDescriptor(Type moduleType, GranitModule instance, Type[] dependencies)
{
    public Type ModuleType { get; } = moduleType;
    public GranitModule Instance { get; } = instance;
    public Type[] Dependencies { get; } = dependencies;

    /// <summary>
    /// Whether the module is enabled. Set during <see cref="GranitApplication.ConfigureServices"/>
    /// after calling <see cref="GranitModule.IsEnabled"/>. Defaults to <c>true</c>.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
