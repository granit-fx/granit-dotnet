using System.Reflection;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Events;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates the pairing convention for privacy data providers: every non-abstract
/// <see cref="IPrivacyDataProvider"/> must ship with a matching Wolverine handler in the same
/// assembly that forwards <see cref="PersonalDataRequestedEto"/> to
/// <see cref="PrivacyFragmentUploader"/>.
/// </summary>
/// <remarks>
/// Without the handler, a provider is registered with the scatter-gather registry but no fragment
/// ever arrives — the saga silently times out at <c>GranitPrivacyOptions.ExportTimeoutMinutes</c>
/// and emits a partial archive. This test catches the omission at build time.
/// </remarks>
public sealed class PrivacyDataProviderConventionTests
{
    [Fact]
    public void Every_provider_has_a_matching_PersonalDataExportHandler_in_the_same_assembly()
    {
        string outputDir = Path.GetDirectoryName(typeof(PrivacyDataProviderConventionTests).Assembly.Location)!;

        Assembly[] granitAssemblies = Directory.GetFiles(outputDir, "Granit.*.dll")
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains("Tests"))
            .Select(TryLoadAssembly)
            .Where(a => a is not null)
            .ToArray()!;

        List<string> violations = [];

        foreach (Assembly assembly in granitAssemblies)
        {
            Type[] providers;
            try
            {
                providers = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .Where(t => typeof(IPrivacyDataProvider).IsAssignableFrom(t))
                    .ToArray();
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }

            foreach (Type provider in providers)
            {
                Type[] candidateHandlers = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface && t.IsPublic)
                    .Where(t => t.Name.EndsWith("PersonalDataExportHandler", StringComparison.Ordinal))
                    .ToArray();

                bool hasMatchingHandler = candidateHandlers.Any(h => HandlerReferencesProvider(h, provider));
                if (!hasMatchingHandler)
                {
                    violations.Add(
                        $"{assembly.GetName().Name}: {provider.Name} implements IPrivacyDataProvider " +
                        "but no *PersonalDataExportHandler in the same assembly forwards " +
                        "PersonalDataRequestedEto through PrivacyFragmentUploader for it. " +
                        "Add a handler like `public static Task HandleAsync(PersonalDataRequestedEto, " +
                        $"{provider.Name}, PrivacyFragmentUploader, CancellationToken)` or remove " +
                        "the provider registration.");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    private static bool HandlerReferencesProvider(Type handler, Type provider)
    {
        MethodInfo[] methods = handler.GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (MethodInfo method in methods)
        {
            if (method.Name is not ("HandleAsync" or "Handle" or "ConsumeAsync" or "Consume"))
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            bool hasEventParam = parameters.Any(p => p.ParameterType == typeof(PersonalDataRequestedEto));
            bool hasProviderParam = parameters.Any(p => p.ParameterType == provider);
            bool hasUploaderParam = parameters.Any(p => p.ParameterType == typeof(PrivacyFragmentUploader));

            if (hasEventParam && hasProviderParam && hasUploaderParam)
            {
                return true;
            }
        }

        return false;
    }

    private static Assembly? TryLoadAssembly(string path)
    {
        try
        {
            return Assembly.LoadFrom(path);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException)
        {
            return null;
        }
    }
}
