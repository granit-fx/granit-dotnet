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
/// <c>EnableRelaxedMemberAccess = false</c>. No I/O, reflection, or .NET assembly access is
/// available from within a template.
/// <para>
/// Model data is exposed under the <c>model</c> variable with snake_case property names
/// (e.g. <c>{{ model.first_name }}</c>). Global contexts are injected under their
/// <see cref="ITemplateGlobalContext.ContextName"/> (e.g. <c>{{ now.date }}</c>).
/// </para>
/// </remarks>
internal sealed class ScribanTemplateEngine : ITemplateEngine
{
    // Parsed Template objects are immutable and thread-safe — cache to avoid re-parsing
    private readonly ConcurrentDictionary<string, Template> _templateCache = new();

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

            // Propagate cancellation to the Scriban render loop
            CancellationToken = cancellationToken,
        };

        return templateContext;
    }
}
