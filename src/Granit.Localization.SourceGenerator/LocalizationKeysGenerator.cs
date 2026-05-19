// ---------------------------------------------------------------------------
// LocalizationKeysGenerator.cs
// Incremental source generator that reads Granit JSON localization files
// declared as AdditionalFiles and produces type-safe C# constant classes.
// ---------------------------------------------------------------------------

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Granit.Localization.SourceGenerator;

/// <summary>
/// Roslyn incremental source generator that reads JSON localization files and
/// produces a <c>LocalizationKeys</c> class with nested constant string fields.
/// </summary>
/// <remarks>
/// JSON files must follow the Granit format: <c>{ "culture": "...", "texts": { ... } }</c>.
/// They must be declared as <c>&lt;AdditionalFiles&gt;</c> in the consuming project.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class LocalizationKeysGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Filter AdditionalFiles to .json files
        IncrementalValuesProvider<AdditionalText> jsonFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        // 2. Extract keys from each JSON file
        IncrementalValuesProvider<ImmutableArray<string>> keyCollections = jsonFiles
            .Select(static (file, cancellationToken) => ExtractKeys(file, cancellationToken));

        // 3. Collect all key collections
        IncrementalValueProvider<ImmutableArray<ImmutableArray<string>>> allKeys = keyCollections.Collect();

        // 4. Get the root namespace from build properties
        IncrementalValueProvider<string?> rootNamespace = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out string? ns);
                return ns;
            });

        // 5. Combine keys with root namespace
        IncrementalValueProvider<(ImmutableArray<ImmutableArray<string>> Keys, string? RootNamespace)> combined =
            allKeys.Combine(rootNamespace);

        // 6. Generate source output
        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            GenerateSource(spc, source.Keys, source.RootNamespace);
        });
    }

    /// <summary>
    /// Extracts all localization keys from a JSON file following the Granit format.
    /// </summary>
    private static ImmutableArray<string> ExtractKeys(AdditionalText file, System.Threading.CancellationToken cancellationToken)
    {
        SourceText? sourceText = file.GetText(cancellationToken);
        if (sourceText is null)
        {
            return ImmutableArray<string>.Empty;
        }

        try
        {
            string json = sourceText.ToString();
            using var document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return ImmutableArray<string>.Empty;
            }

            if (!root.TryGetProperty("texts", out JsonElement textsElement) ||
                textsElement.ValueKind != JsonValueKind.Object)
            {
                return ImmutableArray<string>.Empty;
            }

            List<string> keys = [];
            FlattenKeys(textsElement, "", keys);
            return keys.ToImmutableArray();
        }
        catch
        {
            return ImmutableArray<string>.Empty;
        }
    }

    /// <summary>
    /// Recursively flattens JSON keys using "." as separator (matching
    /// the same convention as JsonLocalizationDictionaryBuilder).
    /// </summary>
    private static void FlattenKeys(JsonElement element, string prefix, List<string> result)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            string key = string.IsNullOrEmpty(prefix)
                ? property.Name
                : prefix + "." + property.Name;

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                FlattenKeys(property.Value, key, result);
            }
            else
            {
                result.Add(key);
            }
        }
    }

    /// <summary>
    /// Generates the <c>LocalizationKeys</c> source file from collected keys.
    /// </summary>
    private static void GenerateSource(
        SourceProductionContext context,
        ImmutableArray<ImmutableArray<string>> allKeyCollections,
        string? rootNamespace)
    {
        // Merge all keys (dedup, preserve order)
        var seen = new HashSet<string>(StringComparer.Ordinal);
        List<string> allKeys = [];

        foreach (ImmutableArray<string> keys in allKeyCollections)
        {
            foreach (string key in keys.Where(seen.Add))
            {
                allKeys.Add(key);
            }
        }

        if (allKeys.Count == 0)
        {
            return;
        }

        // Build the tree structure from keys
        var rootNode = new KeyNode("");
        foreach (string key in allKeys)
        {
            InsertKey(rootNode, key);
        }

        // Generate source
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        string ns = rootNamespace ?? "Granit.Localization";
        sb.AppendLine("namespace " + ns + ";");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Type-safe localization key constants generated from JSON localization files.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class LocalizationKeys");
        sb.AppendLine("{");

        WriteChildren(sb, rootNode, indent: 1);

        sb.AppendLine("}");

        context.AddSource("LocalizationKeys.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    /// <summary>
    /// Inserts a key into the tree structure. Keys are split by ":" first
    /// (resource:key convention), then by "." for nested sub-keys.
    /// </summary>
    private static void InsertKey(KeyNode root, string key)
    {
        // Split on ":" first (Granit convention: "ResourceName:KeyPath")
        string[] colonParts = key.Split(new[] { ':' }, 2);

        List<string> segments = [];

        if (colonParts.Length == 2)
        {
            // "Granit:EntityNotFound" → ["Granit", "EntityNotFound"]
            // "Granit:Validation.Required" → ["Granit", "Validation", "Required"]
            segments.Add(colonParts[0]);
            segments.AddRange(colonParts[1].Split('.'));
        }
        else
        {
            // No colon — split only by "."
            // "Validation.Required" → ["Validation", "Required"]
            segments.AddRange(key.Split('.'));
        }

        KeyNode current = root;
        for (int i = 0; i < segments.Count; i++)
        {
            string segment = segments[i];
            bool isLeaf = i == segments.Count - 1;

            if (isLeaf)
            {
                // This is the final segment — it becomes a constant
                current.Constants.Add(new KeyConstant(SanitizeIdentifier(segment), key));
            }
            else
            {
                // Intermediate segment — it becomes a nested class
                string identifier = SanitizeIdentifier(segment);
                KeyNode? child = current.Children.FirstOrDefault(c => c.Name == identifier);

                if (child is null)
                {
                    child = new KeyNode(identifier);
                    current.Children.Add(child);
                }

                current = child;
            }
        }
    }

    /// <summary>
    /// Recursively writes nested classes and constants to the string builder.
    /// </summary>
    private static void WriteChildren(StringBuilder sb, KeyNode node, int indent)
    {
        string indentStr = new string(' ', indent * 4);

        // Write nested classes first
        foreach (KeyNode child in node.Children)
        {
            sb.AppendLine(indentStr + "public static class " + child.Name);
            sb.AppendLine(indentStr + "{");

            // Write constants of this child
            foreach (KeyConstant constant in child.Constants)
            {
                sb.AppendLine(indentStr + "    public const string " + constant.Identifier + " = \"" + EscapeString(constant.OriginalKey) + "\";");
            }

            // Write nested children
            if (child.Children.Count > 0)
            {
                if (child.Constants.Count > 0)
                {
                    sb.AppendLine();
                }

                WriteChildren(sb, child, indent + 1);
            }

            sb.AppendLine(indentStr + "}");
            sb.AppendLine();
        }

        // Write top-level constants (keys without a resource prefix)
        foreach (KeyConstant constant in node.Constants)
        {
            sb.AppendLine(indentStr + "public const string " + constant.Identifier + " = \"" + EscapeString(constant.OriginalKey) + "\";");
        }
    }

    /// <summary>
    /// Sanitizes a string to be a valid C# identifier.
    /// </summary>
    private static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "_";
        }

        var sb = new StringBuilder(value.Length);

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (i == 0 && char.IsDigit(c))
            {
                sb.Append('_');
            }

            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('_');
            }
        }

        string result = sb.ToString();
        return string.IsNullOrEmpty(result) ? "_" : result;
    }

    /// <summary>
    /// Escapes a string for use in a C# string literal.
    /// </summary>
    private static string EscapeString(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("\0", "\\0");

    /// <summary>
    /// Represents a node in the key hierarchy tree.
    /// </summary>
    private sealed class KeyNode
    {
        public KeyNode(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public List<KeyNode> Children { get; } = [];
        public List<KeyConstant> Constants { get; } = [];
    }

    /// <summary>
    /// Represents a leaf constant with its sanitized identifier and original key value.
    /// </summary>
    private sealed class KeyConstant
    {
        public KeyConstant(string identifier, string originalKey)
        {
            Identifier = identifier;
            OriginalKey = originalKey;
        }

        public string Identifier { get; }
        public string OriginalKey { get; }
    }
}
