using Granit.AI.Prompts.Domain;
using Granit.AI.Prompts.Endpoints.Dtos;
using Granit.AI.Prompts.Endpoints.Internal;
using Granit.AI.Prompts.Endpoints.Permissions;
using Granit.Domain.ValueObjects;
using Granit.Guids;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace Granit.AI.Prompts.Endpoints.Endpoints;

/// <summary>Catalogue endpoints: browse, picker, CRUD, and copy-on-customise.</summary>
internal static class PromptCatalogueEndpoints
{
    internal static RouteGroupBuilder MapPromptCatalogueEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .WithName("ListPrompts")
            .WithSummary("Lists the caller's prompt catalogue.")
            .WithDescription("Returns the framework-seeded system prompts plus the caller's own prompts, system prompts first then by name, without their instruction text.")
            .Produces<IReadOnlyList<PromptSummaryResponse>>()
            .RequireAuthorization(AIPromptsPermissions.Templates.Read);

        group.MapGet("/picker", PickerAsync)
            .WithName("GetPromptPicker")
            .WithSummary("Returns the catalogue grouped by category for the chat picker.")
            .WithDescription("Returns the caller's catalogue grouped by category, each prompt carrying its icon, colour, and short description. Uncategorised prompts fall under the \"General\" group.")
            .Produces<PromptPickerResponse>()
            .RequireAuthorization(AIPromptsPermissions.Templates.Read);

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetPrompt")
            .WithSummary("Returns one prompt from the caller's catalogue by ID.")
            .WithDescription("Returns the prompt with its instruction text. Scoped to the caller: another user's private prompt is reported as not found.")
            .Produces<PromptResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(AIPromptsPermissions.Templates.Read);

        group.MapPost("/", CreateAsync)
            .WithName("CreatePrompt")
            .WithSummary("Creates a prompt owned by the caller.")
            .WithDescription("Creates a private prompt with the given content, decoration, and categories, owned by the caller. Returns 422 when a referenced category does not exist.")
            .Produces<PromptResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization(AIPromptsPermissions.Templates.Manage);

        group.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdatePrompt")
            .WithSummary("Updates one of the caller's own prompts.")
            .WithDescription("Updates the prompt and bumps its version. System prompts are read-only and reported as not found; customise one to get an editable copy. Returns 422 when a referenced category does not exist.")
            .Produces<PromptResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization(AIPromptsPermissions.Templates.Manage);

        group.MapPost("/{id:guid}/customise", CustomiseAsync)
            .WithName("CustomisePrompt")
            .WithSummary("Creates a private editable copy of a system prompt.")
            .WithDescription("Copies a system prompt into a new prompt owned by the caller (resolving its display text and categories); the original is untouched.")
            .Produces<PromptResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(AIPromptsPermissions.Templates.Manage);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeletePrompt")
            .WithSummary("Deletes one of the caller's own prompts.")
            .WithDescription("Deletes the prompt. System prompts cannot be deleted; another user's prompt is reported as not found.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(AIPromptsPermissions.Templates.Delete);

        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<PromptSummaryResponse>>, ProblemHttpResult>> ListAsync(
        [FromServices] IPromptTemplateStore store,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IStringLocalizer<AIPromptsLocalizationResource> localizer,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            return Unauthorized();
        }

        IReadOnlyList<PromptTemplate> prompts = await store.ListCatalogueAsync(ownerId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<PromptSummaryResponse> response =
        [
            .. prompts.Select(p =>
            {
                (string name, string shortDescription) = PromptDisplay.Resolve(localizer, p);
                return PromptSummaryResponse.FromAggregate(p, name, shortDescription);
            }),
        ];
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<PromptPickerResponse>, ProblemHttpResult>> PickerAsync(
        [FromServices] IPromptTemplateStore store,
        [FromServices] IPromptCategoryStore categoryStore,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IStringLocalizer<AIPromptsLocalizationResource> localizer,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            return Unauthorized();
        }

        IReadOnlyList<PromptTemplate> prompts = await store.ListCatalogueAsync(ownerId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<PromptCategory> categories = await categoryStore.ListAsync(cancellationToken).ConfigureAwait(false);

        var categoryNames = categories.ToDictionary(c => c.Id, c => c.Name);
        PromptCategory? general = categories.FirstOrDefault(c => c.Name == PromptCategory.GeneralName);

        // Key Guid.Empty represents the implicit "General" group when no seeded General category exists;
        // when it does, uncategorised prompts merge under its real id.
        Guid uncategorisedKey = general?.Id ?? Guid.Empty;
        string uncategorisedName = general?.Name ?? PromptCategory.GeneralName;
        Dictionary<Guid, (string Name, List<PromptPickerItemResponse> Items)> groups = [];

        foreach (PromptTemplate prompt in prompts)
        {
            (string name, string shortDescription) = PromptDisplay.Resolve(localizer, prompt);
            PromptPickerItemResponse item = new(prompt.Id, name, shortDescription, prompt.Icon, prompt.IconColor?.Value, prompt.IsSystem);

            List<Guid> knownCategoryIds = [.. prompt.CategoryLinks.Select(l => l.CategoryId).Where(categoryNames.ContainsKey)];

            if (knownCategoryIds.Count == 0)
            {
                AddToGroup(groups, uncategorisedKey, uncategorisedName, item);
            }
            else
            {
                foreach (Guid categoryId in knownCategoryIds)
                {
                    AddToGroup(groups, categoryId, categoryNames[categoryId], item);
                }
            }
        }

        IReadOnlyList<PromptPickerCategoryResponse> result =
        [
            .. groups
                .Select(g => new PromptPickerCategoryResponse(
                    g.Key == Guid.Empty ? null : g.Key,
                    g.Value.Name,
                    [.. g.Value.Items.OrderByDescending(i => i.IsSystem).ThenBy(i => i.Name)]))
                .OrderBy(g => g.CategoryName),
        ];

        return TypedResults.Ok(new PromptPickerResponse(result));
    }

    private static async Task<Results<Ok<PromptResponse>, ProblemHttpResult>> GetByIdAsync(
        Guid id,
        [FromServices] IPromptTemplateStore store,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IStringLocalizer<AIPromptsLocalizationResource> localizer,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            return Unauthorized();
        }

        PromptTemplate? prompt = await store.GetAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        if (prompt is null)
        {
            return NotFound();
        }

        (string name, string shortDescription) = PromptDisplay.Resolve(localizer, prompt);
        return TypedResults.Ok(PromptResponse.FromAggregate(prompt, name, shortDescription));
    }

    private static async Task<Results<Created<PromptResponse>, ProblemHttpResult>> CreateAsync(
        CreatePromptRequest request,
        [FromServices] IPromptTemplateStore store,
        [FromServices] IPromptCategoryStore categoryStore,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IStringLocalizer<AIPromptsLocalizationResource> localizer,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            return Unauthorized();
        }

        IReadOnlyList<Guid> categoryIds = request.CategoryIds ?? [];
        if (await HasUnknownCategoryAsync(categoryStore, categoryIds, cancellationToken).ConfigureAwait(false))
        {
            return UnknownCategory();
        }

        var prompt = PromptTemplate.Create(
            guidGenerator.Create(), ownerId, request.Name, request.ShortDescription ?? string.Empty,
            request.Content, request.Icon, ParseColor(request.IconColor));

        foreach (Guid categoryId in categoryIds.Distinct())
        {
            prompt.AssignCategory(guidGenerator.Create(), categoryId);
        }

        await store.CreateAsync(prompt, cancellationToken).ConfigureAwait(false);

        (string name, string shortDescription) = PromptDisplay.Resolve(localizer, prompt);
        return TypedResults.Created($"/prompts/{prompt.Id}", PromptResponse.FromAggregate(prompt, name, shortDescription));
    }

    private static async Task<Results<Ok<PromptResponse>, ProblemHttpResult>> UpdateAsync(
        Guid id,
        UpdatePromptRequest request,
        [FromServices] IPromptTemplateStore store,
        [FromServices] IPromptCategoryStore categoryStore,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IStringLocalizer<AIPromptsLocalizationResource> localizer,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            return Unauthorized();
        }

        IReadOnlyList<Guid> categoryIds = request.CategoryIds ?? [];
        if (await HasUnknownCategoryAsync(categoryStore, categoryIds, cancellationToken).ConfigureAwait(false))
        {
            return UnknownCategory();
        }

        PromptTemplateEdit edit = new(
            request.Name, request.ShortDescription ?? string.Empty, request.Content,
            request.Icon, ParseColor(request.IconColor), categoryIds);

        PromptTemplate? updated = await store.UpdateAsync(id, ownerId, edit, cancellationToken).ConfigureAwait(false);
        if (updated is null)
        {
            return NotFound();
        }

        (string name, string shortDescription) = PromptDisplay.Resolve(localizer, updated);
        return TypedResults.Ok(PromptResponse.FromAggregate(updated, name, shortDescription));
    }

    private static async Task<Results<Created<PromptResponse>, ProblemHttpResult>> CustomiseAsync(
        Guid id,
        [FromServices] IPromptTemplateStore store,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IStringLocalizer<AIPromptsLocalizationResource> localizer,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            return Unauthorized();
        }

        PromptTemplate? source = await store.GetAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return NotFound();
        }

        if (!source.IsSystem)
        {
            return TypedResults.Problem(
                detail: "Only system prompts can be customised; this prompt is already an editable copy.",
                statusCode: StatusCodes.Status409Conflict);
        }

        (string name, string shortDescription) = PromptDisplay.Resolve(localizer, source);

        var copy = PromptTemplate.Create(
            guidGenerator.Create(), ownerId, name, shortDescription, source.Content, source.Icon, source.IconColor);

        foreach (Guid categoryId in source.CategoryLinks.Select(l => l.CategoryId).Distinct())
        {
            copy.AssignCategory(guidGenerator.Create(), categoryId);
        }

        await store.CreateAsync(copy, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/prompts/{copy.Id}", PromptResponse.FromAggregate(copy, name, shortDescription));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid id,
        [FromServices] IPromptTemplateStore store,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            return Unauthorized();
        }

        bool deleted = await store.DeleteAsync(id, ownerId, cancellationToken).ConfigureAwait(false);
        return deleted ? TypedResults.NoContent() : NotFound();
    }

    private static void AddToGroup(
        Dictionary<Guid, (string Name, List<PromptPickerItemResponse> Items)> groups,
        Guid key,
        string name,
        PromptPickerItemResponse item)
    {
        if (!groups.TryGetValue(key, out (string Name, List<PromptPickerItemResponse> Items) group))
        {
            group = (name, []);
            groups[key] = group;
        }

        group.Items.Add(item);
    }

    private static async Task<bool> HasUnknownCategoryAsync(
        IPromptCategoryStore categoryStore, IReadOnlyList<Guid> categoryIds, CancellationToken cancellationToken)
    {
        if (categoryIds.Count == 0)
        {
            return false;
        }

        IReadOnlyList<PromptCategory> categories = await categoryStore.ListAsync(cancellationToken).ConfigureAwait(false);
        HashSet<Guid> known = [.. categories.Select(c => c.Id)];
        return !categoryIds.All(known.Contains);
    }

    private static ProblemHttpResult UnknownCategory() =>
        TypedResults.Problem(detail: "One or more categories do not exist.", statusCode: StatusCodes.Status422UnprocessableEntity);

    private static HexColor? ParseColor(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : HexColor.Create(value);

    private static ProblemHttpResult Unauthorized() =>
        TypedResults.Problem(detail: "The current identity has no user context.", statusCode: StatusCodes.Status401Unauthorized);

    private static ProblemHttpResult NotFound() =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
}
