using System.Diagnostics;
using Granit.Templating.Endpoints.Dtos;
using Granit.Templating.Endpoints.Internal;
using Granit.Templating.Endpoints.Permissions;
using Granit.Templating.GlobalContext;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Templating.Store;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Templating.Endpoints.Endpoints;

/// <summary>
/// Preview Minimal API endpoint for templates:
/// POST preview renders the current draft with optional test data.
/// </summary>
internal static class TemplatingPreviewEndpoints
{
    /// <summary>Maps the template preview endpoint to the given route group.</summary>
    public static RouteGroupBuilder MapTemplatingPreviewEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{name}/preview", HandlePreviewAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Manage)
             .WithName("PreviewTemplate")
             .WithSummary("Renders the current draft with optional test data and returns the HTML output.")
             .WithDescription("Renders the template's current draft content using the configured template engine (Liquid, Razor, etc.) with optional test data. Returns the rendered HTML. Useful for live preview in the template editor. Returns 404 if the template or draft does not exist.")
             .Produces<TemplatePreviewResponse>()
             .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        return group;
    }

    // -------------------------------------------------------------------------
    // POST /{name}/preview — Render the current draft with test data
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<TemplatePreviewResponse>, ProblemHttpResult>> HandlePreviewAsync(
        HttpContext context,
        string name,
        TemplatePreviewRequest body,
        CancellationToken cancellationToken)
    {
        IDocumentTemplateStoreReader? storeReader =
            context.RequestServices.GetService<IDocumentTemplateStoreReader>();

        if (storeReader is null)
        {
            return TemplatingResponseMapper.StoreNotRegistered();
        }

        ProblemHttpResult? nameError = TemplatingResponseMapper.ValidateTemplateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        if (body.Culture is not null)
        {
            ProblemHttpResult? cultureError = TemplatingResponseMapper.ValidateBcp47(body.Culture);
            if (cultureError is not null)
            {
                return cultureError;
            }
        }

        TemplateKey key = new(name, body.Culture);
        TemplateRevision? draft = await storeReader.TryGetDraftAsync(key, cancellationToken).ConfigureAwait(false);

        if (draft is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        var engines =
            context.RequestServices.GetServices<ITemplateEngine>().ToList();

        if (engines.Count == 0)
        {
            return TypedResults.Problem(
                detail: "No template engine is registered. Add Granit.Templating.Scriban to enable rendering.",
                statusCode: StatusCodes.Status501NotImplemented);
        }

        TemplateDescriptor descriptor = new()
        {
            Content = draft.Content,
            MimeType = draft.MimeType,
            RevisionId = draft.RevisionId,
        };

        ITemplateEngine? engine = engines.FirstOrDefault(e => e.CanRender(descriptor));
        if (engine is null)
        {
            return TypedResults.Problem(
                detail: $"No template engine can render MIME type '{draft.MimeType}'.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        IEnumerable<ITemplateGlobalContext> globalContexts =
            context.RequestServices.GetServices<ITemplateGlobalContext>();

        Dictionary<string, object?> data = body.Data.HasValue
            ? TemplatingResponseMapper.ConvertJsonObject(body.Data.Value)
            : [];

        var sw = Stopwatch.StartNew();

        RenderedContent rendered;
        try
        {
            rendered = await engine.RenderAsync(
                descriptor,
                data,
                DocumentFormat.Html,
                globalContexts.ToList(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Do not leak internal error details. Log the full exception
            // via structured logging in the engine; return a generic message to the client.
            return TypedResults.Problem(
                detail: "Template rendering failed. Check the template syntax and data model.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        sw.Stop();

        if (rendered is not TextRenderedContent textContent)
        {
            return TypedResults.Problem(
                detail: "Preview is only supported for text-based templates (HTML). Binary templates (Excel) cannot be previewed.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return TypedResults.Ok(new TemplatePreviewResponse(
            textContent.Html,
            rendered.RevisionId,
            sw.ElapsedMilliseconds));
    }
}
