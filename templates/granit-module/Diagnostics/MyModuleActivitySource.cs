using System.Diagnostics;

namespace Granit.MyModule.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.MyModule distributed tracing.
/// </summary>
internal static class MyModuleActivitySource
{
    internal const string Name = "Granit.MyModule";

    internal static readonly ActivitySource Source = new(Name);

    // Declare one constant per traced operation, e.g.:
    // internal const string DoSomething = "mymodule.do_something";
}
