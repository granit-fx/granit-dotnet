using Microsoft.Extensions.DependencyInjection;

namespace Granit.AI.Tools;

/// <summary>
/// Fluent surface for an application to opt tools in. Obtained from
/// <c>AddGranitAITools(builder =&gt; ...)</c>. Each <c>Add</c> registers an
/// <see cref="IAITool"/> with the container; the <see cref="IAIToolRegistry"/> then
/// aggregates them for the current scope.
/// </summary>
public sealed class AIToolRegistrationBuilder(IServiceCollection services)
{
    /// <summary>The underlying service collection, for advanced registration scenarios.</summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Registers a tool type, resolved per scope so it can use scoped dependencies
    /// (DbContext, current user, …) and therefore stay bound to the caller's ACLs.
    /// </summary>
    /// <typeparam name="TTool">The tool implementation.</typeparam>
    public AIToolRegistrationBuilder Add<TTool>()
        where TTool : class, IAITool
    {
        Services.AddScoped<IAITool, TTool>();
        return this;
    }

    /// <summary>
    /// Registers a pre-built tool instance as a singleton. Use only for stateless tools
    /// with no scoped dependencies; prefer <see cref="Add{TTool}"/> otherwise.
    /// </summary>
    /// <param name="tool">The tool instance.</param>
    public AIToolRegistrationBuilder Add(IAITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        Services.AddSingleton(tool);
        return this;
    }
}
