using System.Collections.Concurrent;
using Granit.Templating.GlobalContext;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Templating.Scriban.Exceptions;
using Scriban;
using Scriban.Runtime;

namespace Granit.Templating.Scriban.Internal;

/// <summary>
/// <see cref="ITemplateEngine"/> implementation backed by
/// <a href="https://github.com/scriban/scriban">Scriban</a>.
/// Supports <c>text/html</c> and <c>text/plain</c> MIME types.
/// </summary>
/// <remarks>
/// <strong>Security:</strong> templates run in a sandboxed <see cref="TemplateContext"/> with
/// <c>EnableRelaxedMemberAccess = false</c>, explicit loop/recursion limits, and a member filter
/// blocking <c>regex</c> builtins (ReDoS prevention). No I/O, reflection, or .NET assembly access
/// is available from within a template.
/// <para>
/// Model data is exposed under the <c>model</c> variable with snake_case property names
/// (e.g. <c>{{ model.first_name }}</c>). Global contexts are injected under their
/// <see cref="ITemplateGlobalContext.ContextName"/> (e.g. <c>{{ now.date }}</c>).
/// </para>
/// </remarks>
internal sealed class ScribanTemplateEngine : ITemplateEngine
{
    private const int MaxLoopIterations = 500;
    private const int MaxRecursionDepth = 50;
    private const int MaxCachedTemplates = 1_000;

    // Parsed Template objects are immutable and thread-safe — bounded cache to prevent memory exhaustion.
    private readonly ConcurrentDictionary<string, Template> _templateCache = new();
    private readonly Lock _evictionLock = new();

    /// <inheritdoc/>
    public bool CanRender(TemplateDescriptor descriptor) =>
        string.Equals(descriptor.MimeType, "text/html", StringComparison.OrdinalIgnoreCase)
        || string.Equals(descriptor.MimeType, "text/plain", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<RenderedContent> RenderAsync<TData>(
        TemplateDescriptor descriptor,
        TData data,
        DocumentFormat targetFormat,
        IReadOnlyList<ITemplateGlobalContext> globalContexts,
        CancellationToken cancellationToken = default) where TData : notnull
    {
        string cacheKey = descriptor.RevisionId?.ToString() ?? descriptor.Content;

        // Evict oldest entries when cache exceeds size limit to prevent memory exhaustion.
        // Lock to prevent TOCTOU race between Count check and TryRemove.
        if (_templateCache.Count >= MaxCachedTemplates && !_templateCache.ContainsKey(cacheKey))
        {
            lock (_evictionLock)
            {
                if (_templateCache.Count >= MaxCachedTemplates)
                {
                    string? firstKey = _templateCache.Keys.FirstOrDefault();
                    if (firstKey is not null)
                    {
                        _templateCache.TryRemove(firstKey, out _);
                    }
                }
            }
        }

        Template template = _templateCache.GetOrAdd(cacheKey, _ =>
        {
            var parsed = Template.Parse(descriptor.Content);
            if (parsed.HasErrors)
            {
                throw new TemplateParseException(parsed.Messages);
            }

            return parsed;
        });

        TemplateContext context = BuildContext(data, globalContexts, cancellationToken);
        string rendered = await template.RenderAsync(context).ConfigureAwait(false);

        return new TextRenderedContent(rendered, targetFormat)
        {
            RevisionId = descriptor.RevisionId,
        };
    }

    private static TemplateContext BuildContext<TData>(
        TData data,
        IReadOnlyList<ITemplateGlobalContext> globalContexts,
        CancellationToken cancellationToken) where TData : notnull
    {
        ScriptObject globals = [];

        // Expose TData as "model" with snake_case property names (PascalCase → snake_case)
        ScriptObject model = [];
        model.Import(data, renamer: StandardMemberRenamer.Default);
        globals.SetValue("model", model, readOnly: true);

        // Inject each global context under its ContextName
        foreach (ITemplateGlobalContext globalContext in globalContexts)
        {
            ScriptObject contextObj = [];
            contextObj.Import(globalContext.Resolve(), renamer: StandardMemberRenamer.Default);
            globals.SetValue(globalContext.ContextName, contextObj, readOnly: true);
        }

        TemplateContext templateContext = new(globals)
        {
            // Sandboxing: no bypass of member visibility restrictions
            EnableRelaxedMemberAccess = false,

            // Explicit resource limits to prevent CPU/memory exhaustion (VULN-200)
            LoopLimit = MaxLoopIterations,
            RecursiveLimit = MaxRecursionDepth,

            // Block access to regex builtins to prevent ReDoS (VULN-207 / CWE-1333)
            MemberFilter = MemberFilterDelegate,

            // Propagate cancellation to the Scriban render loop
            CancellationToken = cancellationToken,
        };

        return templateContext;
    }

    /// <summary>
    /// Blocks access to <c>regex</c> builtins to prevent ReDoS attacks via user-controlled patterns.
    /// All other Scriban builtins (string, math, date, array, object) remain accessible.
    /// </summary>
    private static MemberFilterDelegate MemberFilterDelegate => (member) =>
    {
        // Block regex builtins — user-controlled patterns can cause catastrophic backtracking
        if (member.DeclaringType?.Name is "RegexFunctions")
        {
            return false;
        }

        return true;
    };
}
