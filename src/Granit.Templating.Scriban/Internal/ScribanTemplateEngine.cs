using System.Collections.Concurrent;
using Granit.Templating.GlobalContext;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Templating.Scriban.Exceptions;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
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
/// <para>
/// <strong>Include support:</strong> when a <see cref="GranitTemplateLoader"/> is provided,
/// templates can use <c>{{ include 'template_name' }}</c> to include other templates resolved
/// through the standard <see cref="ITemplateResolver"/> chain.
/// </para>
/// <para>
/// <strong>Extra variables:</strong> <see cref="TemplateDescriptor.ExtraVariables"/> are injected
/// as top-level Scriban variables (e.g. <c>{{ body }}</c> for layout rendering).
/// </para>
/// </remarks>
internal sealed class ScribanTemplateEngine(
    IServiceProvider serviceProvider,
    GranitTemplateLoader? templateLoader = null) : ITemplateEngine
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

        TemplateContext context = BuildContext(descriptor, data, globalContexts, cancellationToken);
        string rendered = await template.RenderAsync(context).ConfigureAwait(false);

        return new TextRenderedContent(rendered, targetFormat)
        {
            RevisionId = descriptor.RevisionId,
        };
    }

    private TemplateContext BuildContext<TData>(
        TemplateDescriptor descriptor,
        TData data,
        IReadOnlyList<ITemplateGlobalContext> globalContexts,
        CancellationToken cancellationToken) where TData : notnull
    {
        ScriptObject globals = [];

        // Expose TData as "model" with snake_case property names (PascalCase → snake_case).
        // Scriban's Import skips the renamer for IDictionary (upstream limitation), so we handle it manually.
        ScriptObject model = [];
        if (data is IDictionary<string, object?> dict)
        {
            foreach ((string key, object? value) in dict)
            {
                model.SetValue(StandardMemberRenamer.Rename(key), value, readOnly: false);
            }
        }
        else
        {
            model.Import(data, renamer: StandardMemberRenamer.Default);
        }

        globals.SetValue("model", model, readOnly: true);

        // Inject each global context under its ContextName
        foreach (ITemplateGlobalContext globalContext in globalContexts)
        {
            ScriptObject contextObj = [];
            contextObj.Import(globalContext.Resolve(), renamer: StandardMemberRenamer.Default);
            globals.SetValue(globalContext.ContextName, contextObj, readOnly: true);
        }

        // Inject extra variables as top-level raw values (used by layout system for {{ body }})
        if (descriptor.ExtraVariables is not null)
        {
            foreach ((string key, object value) in descriptor.ExtraVariables)
            {
                globals.SetValue(key, value, readOnly: true);
            }
        }

        // Register {{ t "Resource:Key" arg1 arg2 }} localization function
        IStringLocalizerFactory? localizerFactory = serviceProvider.GetService<IStringLocalizerFactory>();
        if (localizerFactory is not null)
        {
            globals.SetValue("t", new TemplateLocalizationFunction(localizerFactory), readOnly: true);
        }

        // Register {{ value | to_user_time }} timezone conversion filter
        IClock? clock = serviceProvider.GetService<IClock>();
        if (clock is not null)
        {
            globals.SetValue("to_user_time", new UserTimeFunction(clock), readOnly: true);
        }

        // Register {{ bytes | format_bytes }} human-readable byte size filter
        // (B / KB / MB / GB / TB / PB, 1024-based).
        globals.SetValue("format_bytes", new FormatBytesFunction(), readOnly: true);

        TemplateContext templateContext = new()
        {
            // Sandboxing: no bypass of member visibility restrictions
            EnableRelaxedMemberAccess = false,

            // Explicit resource limits to prevent CPU/memory exhaustion.
            LoopLimit = MaxLoopIterations,
            RecursiveLimit = MaxRecursionDepth,

            // Block access to regex builtins to prevent ReDoS (CWE-1333).
            MemberFilter = MemberFilterDelegate,

            // Propagate cancellation to the Scriban render loop
            CancellationToken = cancellationToken,

            // Enable {{ include 'template_name' }} via the resolver chain
            TemplateLoader = templateLoader,
        };

        // Push globals on top of the default BuiltinObject (which provides html, string, math, etc.)
        templateContext.PushGlobal(globals);

        return templateContext;
    }

    /// <summary>
    /// Blocks access to <c>regex</c> builtins to prevent ReDoS attacks via user-controlled patterns.
    /// All other Scriban builtins (string, math, date, array, object) remain accessible.
    /// </summary>
    private static MemberFilterDelegate MemberFilterDelegate => (member) =>
    {
        // Block regex builtins — user-controlled patterns can cause catastrophic backtracking
        return member.DeclaringType?.Name is not "RegexFunctions";
    };
}
