using System.Reflection;
using Granit.Localization;
using Granit.Localization.Options;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Convention check: every <c>Granit.*.Endpoints</c> assembly that ships a
/// <c>[LocalizationResourceName]</c>-decorated marker type must explicitly register
/// that resource from its <c>*EndpointsModule.ConfigureServices</c>.
/// </summary>
/// <remarks>
/// <para>
/// Prevents regressions of the dev.2691 sidebar bug where workspace labels rendered
/// as the literal key suffix because endpoint resource registration was implicit
/// through auto-discovery and races with assembly load order.
/// </para>
/// <para>
/// Runtime-boot fallback: instead of brittle static analysis on
/// <c>AddLocalizationResource&lt;T&gt;</c> call sites, this test loads each endpoint
/// module, runs <c>ConfigureServices</c> against a real <see cref="ServiceCollection"/>,
/// and asserts the resource type ends up in <see cref="GranitLocalizationOptions.Resources"/>.
/// </para>
/// </remarks>
public sealed class EndpointModuleLocalizationRegistrationTests
{
    [Fact]
    public void Every_endpoints_module_with_localization_resource_must_register_it_explicitly()
    {
        List<(Assembly Assembly, Type ResourceType, Type ModuleType)> candidates =
            DiscoverEndpointModulesWithLocalizationResource();

        candidates.ShouldNotBeEmpty(
            "No *.Endpoints assembly with a [LocalizationResourceName] resource was found. "
            + "Either the project layout changed, or the test cannot see endpoint assemblies "
            + "via Granit.ArchitectureTests' references.");

        List<string> failures = [];

        foreach ((Assembly assembly, Type resourceType, Type moduleType) in candidates)
        {
            (bool registered, string? failureReason) =
                TryRunModuleAndCheckRegistration(moduleType, resourceType);
            if (!registered)
            {
                if (failureReason is not null)
                {
                    failures.Add(
                        $"{moduleType.FullName} (assembly {assembly.GetName().Name}) threw while "
                        + $"running ConfigureServices in isolation: {failureReason}");
                }
                else
                {
                    failures.Add(
                        $"{moduleType.FullName} (assembly {assembly.GetName().Name}) does not register "
                        + $"{resourceType.FullName}. Add "
                        + $"`context.Services.AddLocalizationResource<{resourceType.Name}>();` at the top "
                        + $"of ConfigureServices in {moduleType.Name}.");
                }
            }
        }

        failures.ShouldBeEmpty(
            "Every Granit.*.Endpoints module that ships a [LocalizationResourceName] resource "
            + "must register it explicitly from its *EndpointsModule.ConfigureServices. "
            + $"{failures.Count} module(s) violate this rule:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, failures.Order(StringComparer.Ordinal)));
    }

    private static List<(Assembly, Type, Type)> DiscoverEndpointModulesWithLocalizationResource()
    {
        List<(Assembly, Type, Type)> result = [];

        // Force-load every Granit.*.Endpoints.dll from the test output directory: project
        // references are lazy and an endpoint assembly that hasn't been touched yet by any
        // other code path won't appear in AppDomain.GetAssemblies().
        string outputDir = Path.GetDirectoryName(typeof(EndpointModuleLocalizationRegistrationTests)
            .Assembly.Location)!;

        foreach (string dllPath in Directory.GetFiles(outputDir, "Granit.*.Endpoints.dll"))
        {
            try
            {
                Assembly.LoadFrom(dllPath);
            }
            catch
            {
                // Skip unreadable/unloadable assemblies — they don't carry endpoint modules.
            }
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (Assembly assembly in assemblies)
        {
            string? name = assembly.GetName().Name;
            if (name is null
                || !name.StartsWith("Granit.", StringComparison.Ordinal)
                || !name.EndsWith(".Endpoints", StringComparison.Ordinal))
            {
                continue;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = [.. ex.Types.OfType<Type>()];
            }

            Type? resourceType = types.FirstOrDefault(t =>
                t.GetCustomAttribute<LocalizationResourceNameAttribute>() is not null);

            if (resourceType is null)
            {
                continue;
            }

            Type? moduleType = types.FirstOrDefault(t =>
                t is { IsAbstract: false, IsClass: true }
                && t.Name.EndsWith("EndpointsModule", StringComparison.Ordinal)
                && typeof(GranitModule).IsAssignableFrom(t));

            if (moduleType is null)
            {
                // Assembly has a resource but no *EndpointsModule — count as a failure
                // by recording a synthetic entry; the test loop will surface it.
                result.Add((assembly, resourceType, typeof(MissingEndpointsModuleMarker)));
                continue;
            }

            result.Add((assembly, resourceType, moduleType));
        }

        return result;
    }

    private static (bool Registered, string? FailureReason) TryRunModuleAndCheckRegistration(
        Type moduleType, Type resourceType)
    {
        if (moduleType == typeof(MissingEndpointsModuleMarker))
        {
            return (false, null);
        }

        try
        {
            HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
            builder.Services.AddOptions();
            ServiceConfigurationContext context = new(
                builder.Services, builder.Configuration, builder);

            object moduleInstance = Activator.CreateInstance(moduleType)
                ?? throw new InvalidOperationException(
                    $"Could not instantiate {moduleType.FullName}.");
            var module = (GranitModule)moduleInstance;
            module.ConfigureServices(context);

            using ServiceProvider sp = builder.Services.BuildServiceProvider();
            GranitLocalizationOptions options =
                sp.GetRequiredService<IOptions<GranitLocalizationOptions>>().Value;

            return (options.Resources.TryGetValue(resourceType, out _), null);
        }
        catch (Exception ex)
        {
            // If the module fails to ConfigureServices in isolation (e.g. it transitively
            // requires services not registered here), surface it as a violation rather than
            // a green-by-default — and include the original exception type + message so CI
            // logs expose the actual cause instead of a misleading "does not register" line.
            Exception root = ex.GetBaseException();
            return (false, $"{root.GetType().Name}: {root.Message}");
        }
    }

    private sealed class MissingEndpointsModuleMarker;
}
