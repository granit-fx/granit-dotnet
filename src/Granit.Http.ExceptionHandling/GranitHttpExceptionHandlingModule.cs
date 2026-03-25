using Granit.Http.ExceptionHandling.Extensions;
using Granit.Modularity;

namespace Granit.Http.ExceptionHandling;

/// <summary>
/// Granit module for centralized exception handling.
/// Registers <c>AddGranitExceptionHandling()</c>.
/// </summary>
/// <remarks>
/// Call <c>app.UseGranitExceptionHandling()</c> in <c>Program.cs</c>
/// <b>as the first middleware</b>, before routing and authentication.
/// </remarks>
public sealed class GranitHttpExceptionHandlingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitExceptionHandling();
}
