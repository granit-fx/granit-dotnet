using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;

namespace Granit.Templating.Resolvers;

/// <summary>
/// Resolves templates from embedded assembly resources.
/// </summary>
/// <remarks>
/// Lowest-priority resolver (priority <c>-100</c>) — serves as a code-level fallback
/// when no store-backed resolver finds a published template.
/// <para>
/// Resource naming convention (case-sensitive):
/// <list type="table">
///   <listheader><term>Culture</term><description>Resource name</description></listheader>
///   <item>
///     <term>Regional (<c>"pt-BR"</c>)</term>
///     <description><c>{AssemblyName}.Templates.{TemplateName}.pt-BR.html</c></description>
///   </item>
///   <item>
///     <term>Parent culture (<c>"pt"</c>)</term>
///     <description><c>{AssemblyName}.Templates.{TemplateName}.pt.html</c></description>
///   </item>
///   <item>
///     <term>Neutral (any / fallback)</term>
///     <description><c>{AssemblyName}.Templates.{TemplateName}.html</c></description>
///   </item>
/// </list>
/// Lookup walks the requested culture, then each parent culture, then the neutral
/// resource — mirroring the <c>{culture} → {parent} → neutral</c> strategy used by the
/// JSON localization files. This lets regional template files ship only when they
/// differ from their parent culture.
/// </para>
/// <para>
/// Register via:
/// <code>
/// services.AddEmbeddedTemplates(typeof(AcmeTemplates).Assembly);
/// </code>
/// Multiple assemblies can be registered by calling <c>AddEmbeddedTemplates</c> multiple times.
/// </para>
/// </remarks>
internal sealed class EmbeddedTemplateResolver(IReadOnlyList<Assembly> assemblies) : ITemplateResolver
{
    private readonly IReadOnlyList<Assembly> _assemblies = assemblies;

    // Cache resource names per assembly to avoid repeated GetManifestResourceNames() allocations
    private readonly ConcurrentDictionary<Assembly, HashSet<string>> _resourceNameCache = new();

    /// <inheritdoc/>
    public int Priority => -100;

    /// <inheritdoc/>
    public Task<TemplateDescriptor?> TryResolveAsync(
        TemplateKey key, CancellationToken cancellationToken = default)
    {
        foreach (Assembly assembly in _assemblies)
        {
            TemplateDescriptor? descriptor = TryLoadFromAssembly(assembly, key);
            if (descriptor is not null)
            {
                return Task.FromResult<TemplateDescriptor?>(descriptor);
            }
        }

        return Task.FromResult<TemplateDescriptor?>(null);
    }

    private TemplateDescriptor? TryLoadFromAssembly(Assembly assembly, TemplateKey key)
    {
        string assemblyName = assembly.GetName().Name ?? string.Empty;
        (string extension, string mimeType) = ExtensionFor(key.MimeType);
        string neutralResource = $"{assemblyName}.Templates.{key.Name}.{extension}";

        // Walk requested culture → parent → neutral. Mirrors the JSON locale fallback so
        // regional template files (pt-BR, en-GB...) can omit keys identical to their parent.
        string? resourceName = null;
        if (key.Culture is not null)
        {
            foreach (string culture in EnumerateCultureChain(key.Culture))
            {
                resourceName = FindResource(assembly, $"{assemblyName}.Templates.{key.Name}.{culture}.{extension}");
                if (resourceName is not null)
                {
                    break;
                }
            }
        }

        resourceName ??= FindResource(assembly, neutralResource);

        if (resourceName is null)
        {
            return null;
        }

        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        using StreamReader reader = new(stream, System.Text.Encoding.UTF8);
        string content = reader.ReadToEnd();

        return new TemplateDescriptor
        {
            Content = content,
            MimeType = mimeType,
            RevisionId = null,
        };
    }

    /// <summary>Maps the requested MIME type to the embedded-resource file extension.</summary>
    private static (string Extension, string MimeType) ExtensionFor(string? mimeType) => mimeType switch
    {
        "text/plain" => ("txt", "text/plain"),
        "text/markdown" => ("md", "text/markdown"),
        _ => ("html", "text/html"),
    };

    private static IEnumerable<string> EnumerateCultureChain(string culture)
    {
        CultureInfo? current = null;
        try
        {
            current = CultureInfo.GetCultureInfo(culture);
        }
        catch (CultureNotFoundException)
        {
            // ignored — fall through to literal-tag yield below
        }

        if (current is null)
        {
            // Unknown / malformed culture: yield the raw tag once (caller may still have
            // shipped a file under that literal name) then fall through to neutral.
            yield return culture;
            yield break;
        }

        while (!string.IsNullOrEmpty(current.Name))
        {
            yield return current.Name;
            CultureInfo parent = current.Parent;
            if (parent.Equals(current))
            {
                yield break;
            }
            current = parent;
        }
    }

    private string? FindResource(Assembly assembly, string resourceName)
    {
        HashSet<string> names = _resourceNameCache.GetOrAdd(
            assembly,
            static a => new HashSet<string>(a.GetManifestResourceNames(), StringComparer.Ordinal));

        return names.Contains(resourceName) ? resourceName : null;
    }
}
