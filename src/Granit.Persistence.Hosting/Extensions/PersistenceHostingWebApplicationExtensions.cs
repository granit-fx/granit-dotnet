using Granit.Persistence.Hosting.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.Hosting.Extensions;

/// <summary>
/// Extension methods for migration support on <see cref="WebApplication"/>.
/// </summary>
public static class PersistenceHostingWebApplicationExtensions
{
    /// <summary>
    /// Checks whether the <c>--migrate</c> CLI flag is present in the application arguments.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns><c>true</c> if the migration flag is present; otherwise <c>false</c>.</returns>
    public static bool HasGranitMigrateFlag(this WebApplication app)
    {
        GranitMigrateOptions? options = app.Services.GetService<GranitMigrateOptions>();

        if (options is null)
        {
            return false;
        }

        return Environment.GetCommandLineArgs().Contains(options.CliFlag);
    }

    /// <summary>
    /// Runs all pending EF Core migrations, seeds data, and flushes logs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this method after <c>UseGranitAsync()</c> and before <c>RunAsync()</c>.
    /// If the migration flag is present, call this method and then <c>return;</c> from
    /// <c>Program.cs</c> to exit cleanly without starting the HTTP server.
    /// </para>
    /// <para>
    /// Uses <c>return;</c> from top-level statements (not <c>Environment.Exit()</c>)
    /// to ensure all <c>using</c>/<c>finally</c> blocks are respected.
    /// </para>
    /// </remarks>
    /// <param name="app">The web application.</param>
    /// <returns>The exit code: <c>0</c> on success, non-zero on failure.</returns>
    public static async Task<int> RunGranitMigrationsAsync(this WebApplication app)
    {
        IGranitMigrationRunner runner = app.Services.GetRequiredService<IGranitMigrationRunner>();
        ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.Persistence.Hosting");

        int exitCode = 1;

        try
        {
            exitCode = await runner.RunAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Migration failed with an unhandled exception.");
        }
        finally
        {
            // Flush all log sinks before exit
            await app.DisposeAsync().ConfigureAwait(false);

            // Defensive: flush Serilog static logger if present (survives DI disposal)
            var serilogType = Type.GetType("Serilog.Log, Serilog");
            serilogType?.GetMethod("CloseAndFlush")?.Invoke(null, null);
        }

        return exitCode;
    }
}
