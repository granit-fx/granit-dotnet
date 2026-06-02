using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.Templating.Endpoints.Endpoints;
using Granit.Templating.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Templating.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping Granit template administration endpoints.
/// </summary>
public static class TemplatingEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps template administration endpoints under <c>/{prefix}</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers the following endpoints:
    /// <list type="bullet">
    /// <item><c>GET /templates</c> + <c>GET /templates/meta</c> — paginated list and query metadata, powered by the Query Engine</item>
    /// <item><c>GET /templates/{name}</c> — detail (draft + published)</item>
    /// <item><c>POST /templates/</c> — create a new draft</item>
    /// <item><c>PUT /templates/{name}</c> — update an existing draft</item>
    /// <item><c>DELETE /templates/{name}/draft</c> — delete draft only</item>
    /// <item><c>POST /templates/{name}/publish</c> — publish the current draft</item>
    /// <item><c>POST /templates/{name}/unpublish</c> — unpublish (archive the published revision)</item>
    /// <item><c>GET /templates/{name}/lifecycle</c> — lifecycle info (current status, available transitions)</item>
    /// <item><c>POST /templates/{name}/preview</c> — render the current draft with test data</item>
    /// <item><c>GET /templates/{name}/variables</c> — list available template variables for autocompletion</item>
    /// <item><c>GET /templates/{name}/history</c> — paginated revision history (summaries, no content)</item>
    /// <item><c>GET /templates/{name}/history/{revisionId}</c> — full detail of a specific revision</item>
    /// <item><c>GET /layouts</c> — list all available layout names</item>
    /// <item><c>GET /categories</c> — list all template categories</item>
    /// <item><c>POST /categories</c> — create a new category</item>
    /// <item><c>PUT /categories/{id}</c> — update a category</item>
    /// <item><c>DELETE /categories/{id}</c> — delete a category (409 if templates associated)</item>
    /// </list>
    /// </para>
    /// <para>
    /// All endpoints require at minimum the <c>Templates.Read</c> permission.
    /// Write operations require <c>Templates.Manage</c> or <c>Categories.Manage</c>.
    /// If <see cref="Granit.Templating.Store.IDocumentTemplateStoreReader"/>/<see cref="Granit.Templating.Store.IDocumentTemplateStoreWriter"/> is not registered
    /// (no EF Core persistence module loaded), all endpoints return <c>501 Not Implemented</c>.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="TemplatingEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitTemplating(
        this IEndpointRouteBuilder endpoints,
        Action<TemplatingEndpointsOptions>? configure = null)
    {
        TemplatingEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        // Template CRUD + lifecycle + preview + variables + history
        RouteGroupBuilder templateGroup = group.MapGranitGroup("templates");
        templateGroup.MapGranitQuery<Granit.Templating.Store.TemplateSummary>(configure: q =>
            q.AuthorizationPolicy = Granit.Templating.Endpoints.Permissions.TemplatingPermissions.Templates.Read);
        templateGroup.MapTemplatingCrudEndpoints();
        templateGroup.MapTemplatingLifecycleEndpoints();
        templateGroup.MapTemplatingPreviewEndpoints();
        templateGroup.MapTemplatingVariablesEndpoints();
        templateGroup.MapTemplatingHistoryEndpoints();

        // Layouts + categories (root group, not under /templates)
        group.MapTemplatingCategoryEndpoints();

        return group;
    }
}
