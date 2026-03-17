// Placed in Wolverine.Http namespace to match the reflection check
// in WolverineOpenApiOperationTransformer.GetWolverineAttributeName().

// ReSharper disable CheckNamespace
namespace Wolverine.Http;

/// <summary>
/// Fake attribute that mimics Wolverine's <c>WolverinePostAttribute</c> type name
/// pattern and <c>Name</c> property. The transformer uses reflection to read
/// <c>Name</c> from types whose <c>FullName</c> starts with
/// <c>Wolverine.Http.Wolverine</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class WolverinePostAttribute : Attribute
{
    public string? Name { get; set; }
}
