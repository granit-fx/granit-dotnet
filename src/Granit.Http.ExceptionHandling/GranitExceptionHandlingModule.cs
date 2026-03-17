using Granit.Core.Modularity;
using Granit.Http.ExceptionHandling.Extensions;

namespace Granit.Http.ExceptionHandling;

/// <summary>
/// Granit module for centralized exception handling.
/// Registers <c>AddGranitExceptionHandling()</c>.
/// </summary>
/// <remarks>
/// Call <c>app.UseGranitExceptionHandling()</c> in <c>Program.cs</c>
/// <b>as the first middleware</b>, before routing and authentication.
/// </remarks>
public sealed class GranitExceptionHandlingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitExceptionHandling();
}
