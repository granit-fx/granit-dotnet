using System.Net;
using System.Net.Http.Json;
using Granit.AI.Prompts.Domain;
using Granit.AI.Prompts.Endpoints.Dtos;
using Granit.AI.Prompts.Endpoints.Extensions;
using Granit.AI.Prompts.Endpoints.Permissions;
using Granit.Guids;
using Granit.Testing.Endpoints;
using Granit.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Prompts.Endpoints.Tests;

/// <summary>
/// HTTP-level tests for <c>PromptCatalogueEndpoints</c> driven through
/// <see cref="GranitEndpointTestHost"/> with substituted stores. Exercises the handler
/// branches the validator/DTO unit tests cannot reach (auth gate, not-found, 422, 409).
/// </summary>
public sealed class PromptCatalogueEndpointsHttpTests
{
    private static readonly Guid Owner = Guid.Parse("0a0a0a0a-0a0a-0a0a-0a0a-0a0a0a0a0a0a");

    private readonly IPromptTemplateStore _store = Substitute.For<IPromptTemplateStore>();
    private readonly IPromptCategoryStore _categoryStore = Substitute.For<IPromptCategoryStore>();
    private readonly IGuidGenerator _guids = Substitute.For<IGuidGenerator>();

    public PromptCatalogueEndpointsHttpTests()
    {
        _guids.Create().Returns(_ => Guid.NewGuid());
        _categoryStore.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
    }

    private Task<GranitEndpointTestHost> StartAsync(string? userId) =>
        GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder()
                    .AddPolicy(AIPromptsPermissions.Templates.Read, p => p.RequireClaim(TestAuthHandler.PermissionClaimType, AIPromptsPermissions.Templates.Read))
                    .AddPolicy(AIPromptsPermissions.Templates.Manage, p => p.RequireClaim(TestAuthHandler.PermissionClaimType, AIPromptsPermissions.Templates.Manage))
                    .AddPolicy(AIPromptsPermissions.Templates.Delete, p => p.RequireClaim(TestAuthHandler.PermissionClaimType, AIPromptsPermissions.Templates.Delete));

                services.AddSingleton(_store);
                services.AddSingleton(_categoryStore);
                services.AddSingleton(_guids);
                services.AddSingleton<ICurrentUserService>(new StubCurrentUser(userId));
                services.AddSingleton<IStringLocalizer<AIPromptsLocalizationResource>>(new StubLocalizer());
            },
            configureEndpoints: app => app.MapGranitPrompts());

    private static HttpClient FullAccess(GranitEndpointTestHost host) => host.CreateClientWithPermissions(
        AIPromptsPermissions.Templates.Read,
        AIPromptsPermissions.Templates.Manage,
        AIPromptsPermissions.Templates.Delete);

    // ── Authorization gates ─────────────────────────────────────────────────────

    [Fact]
    public async Task List_without_the_read_permission_is_forbidden()
    {
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        // Authenticated, but holds an unrelated permission rather than Templates.Read → 403, not 401.
        HttpClient client = host.CreateClientWithPermissions("AIPrompts.Templates.Other");
        HttpResponseMessage response = await client.GetAsync("/prompts", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_without_a_user_context_is_unauthorized()
    {
        await using GranitEndpointTestHost host = await StartAsync(userId: null);

        HttpResponseMessage response = await FullAccess(host).GetAsync("/prompts", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── List / picker ───────────────────────────────────────────────────────────

    [Fact]
    public async Task List_returns_the_callers_catalogue()
    {
        _store.ListCatalogueAsync(Owner, Arg.Any<CancellationToken>())
            .Returns([UserPrompt("My prompt")]);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        List<PromptSummaryResponse>? body = await FullAccess(host)
            .GetFromJsonAsync<List<PromptSummaryResponse>>("/prompts", TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.ShouldHaveSingleItem().Name.ShouldBe("My prompt");
    }

    [Fact]
    public async Task Picker_groups_uncategorised_prompts_under_general()
    {
        _store.ListCatalogueAsync(Owner, Arg.Any<CancellationToken>())
            .Returns([UserPrompt("Loose prompt")]);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        PromptPickerResponse? body = await FullAccess(host)
            .GetFromJsonAsync<PromptPickerResponse>("/prompts/picker", TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Categories.ShouldHaveSingleItem().CategoryName.ShouldBe(PromptCategory.GeneralName);
    }

    // ── GetById ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_returns_404_when_the_prompt_is_absent()
    {
        var id = Guid.NewGuid();
        _store.GetAsync(id, Owner, Arg.Any<CancellationToken>()).Returns((PromptTemplate?)null);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).GetAsync($"/prompts/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_returns_the_prompt_when_found()
    {
        PromptTemplate prompt = UserPrompt("Found");
        _store.GetAsync(prompt.Id, Owner, Arg.Any<CancellationToken>()).Returns(prompt);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        PromptResponse? body = await FullAccess(host)
            .GetFromJsonAsync<PromptResponse>($"/prompts/{prompt.Id}", TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Name.ShouldBe("Found");
    }

    // ── Create ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_persists_and_returns_201()
    {
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/prompts", new CreatePromptRequest("New", "content"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await _store.Received(1).CreateAsync(Arg.Any<PromptTemplate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_returns_422_for_an_unknown_category()
    {
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsJsonAsync(
            "/prompts",
            new CreatePromptRequest("New", "content", CategoryIds: [Guid.NewGuid()]),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await _store.DidNotReceive().CreateAsync(Arg.Any<PromptTemplate>(), Arg.Any<CancellationToken>());
    }

    // ── Update ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_returns_404_when_the_prompt_is_absent()
    {
        var id = Guid.NewGuid();
        _store.UpdateAsync(id, Owner, Arg.Any<PromptTemplateEdit>(), Arg.Any<CancellationToken>())
            .Returns((PromptTemplate?)null);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PutAsJsonAsync(
            $"/prompts/{id}", new UpdatePromptRequest("Renamed", "content"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_returns_200_when_the_prompt_is_updated()
    {
        var id = Guid.NewGuid();
        _store.UpdateAsync(id, Owner, Arg.Any<PromptTemplateEdit>(), Arg.Any<CancellationToken>())
            .Returns(UserPrompt("Renamed"));
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PutAsJsonAsync(
            $"/prompts/{id}", new UpdatePromptRequest("Renamed", "content"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── Customise ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Customise_returns_404_when_the_source_is_absent()
    {
        var id = Guid.NewGuid();
        _store.GetAsync(id, Owner, Arg.Any<CancellationToken>()).Returns((PromptTemplate?)null);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsync($"/prompts/{id}/customise", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Customise_returns_409_when_the_source_is_not_a_system_prompt()
    {
        PromptTemplate userPrompt = UserPrompt("Mine");
        _store.GetAsync(userPrompt.Id, Owner, Arg.Any<CancellationToken>()).Returns(userPrompt);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsync($"/prompts/{userPrompt.Id}/customise", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Customise_copies_a_system_prompt_and_returns_201()
    {
        var system = PromptTemplate.CreateSystem(Guid.NewGuid(), "Prompt:Sys:Name", "Prompt:Sys:Desc", "content");
        _store.GetAsync(system.Id, Owner, Arg.Any<CancellationToken>()).Returns(system);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).PostAsync($"/prompts/{system.Id}/customise", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await _store.Received(1).CreateAsync(Arg.Any<PromptTemplate>(), Arg.Any<CancellationToken>());
    }

    // ── Delete ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_returns_204_when_the_prompt_is_removed()
    {
        var id = Guid.NewGuid();
        _store.DeleteAsync(id, Owner, Arg.Any<CancellationToken>()).Returns(true);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).DeleteAsync($"/prompts/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_returns_404_when_nothing_was_removed()
    {
        var id = Guid.NewGuid();
        _store.DeleteAsync(id, Owner, Arg.Any<CancellationToken>()).Returns(false);
        await using GranitEndpointTestHost host = await StartAsync(Owner.ToString());

        HttpResponseMessage response = await FullAccess(host).DeleteAsync($"/prompts/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static PromptTemplate UserPrompt(string name) =>
        PromptTemplate.Create(Guid.NewGuid(), Owner, name, "desc", "content");

    private sealed class StubCurrentUser(string? userId) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? UserName => null;
        public string? Email => null;
        public string? FirstName => null;
        public string? LastName => null;
        public bool IsAuthenticated => userId is not null;
        public IReadOnlyList<string> GetRoles() => [];
        public bool IsInRole(string role) => false;
    }

    private sealed class StubLocalizer : IStringLocalizer<AIPromptsLocalizationResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => this[name];
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
