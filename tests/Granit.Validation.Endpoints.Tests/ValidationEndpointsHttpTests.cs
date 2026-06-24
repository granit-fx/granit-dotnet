using System.Net;
using System.Net.Http.Json;
using FluentValidation;
using Granit.Testing.Endpoints;
using Granit.Validation.Endpoints.Diagnostics;
using Granit.Validation.Endpoints.Dtos;
using Granit.Validation.Endpoints.Extensions;
using Granit.Validation.Endpoints.Validators;
using Granit.Validation.ServerValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Endpoints.Tests;

/// <summary>
/// HTTP-level integration tests for <see cref="ValidationEndpointRouteBuilderExtensions"/>
/// using <see cref="GranitEndpointTestHost"/>. Complements the unit tests in
/// <see cref="ValidationEndpointTests"/> which call the handlers directly.
/// </summary>
public sealed class ValidationEndpointsHttpTests
{
    private const string Prefix = "/validation";

    private static Task<GranitEndpointTestHost> StartAsync(params IServerValidator[] validators) =>
        GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddSingleton<ValidationMetrics>();
                services.AddSingleton<IServerValidatorContributor>(
                    new TestContributor(validators));
                services.AddSingleton(sp =>
                    new ServerValidatorRegistry(
                        sp.GetRequiredService<IEnumerable<IServerValidatorContributor>>(),
                        NullLogger<ServerValidatorRegistry>.Instance));
                services.AddAuthorizationBuilder();
                services.AddScoped<IValidator<ValidationFieldValidateRequest>, ValidationFieldValidateRequestValidator>();
                services.AddScoped<IValidator<ValidationFieldValidateBatchRequest>, ValidationFieldValidateBatchRequestValidator>();
            },
            configureEndpoints: app => app.MapGranitValidation());

    // =========================================================================
    // POST /validation/validate
    // =========================================================================

    [Fact]
    public async Task PostValidate_ValidValue_Returns200WithValidStatus()
    {
        await using GranitEndpointTestHost host = await StartAsync(
            new DelegatingServerValidator("Validation:Format:Iban", v => v == "BE68539007547034"));
        using HttpClient client = host.CreateAnonymousClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{Prefix}/validate",
            new ValidationFieldValidateRequest("Validation:Format:Iban", "BE68539007547034"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ValidationFieldValidateResponse? body = await response.Content
            .ReadFromJsonAsync<ValidationFieldValidateResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Status.ShouldBe(ValidationFieldStatus.Valid);
        body.ErrorCode.ShouldBe("Validation:Format:Iban");
    }

    [Fact]
    public async Task PostValidate_InvalidValue_Returns200WithInvalidStatus()
    {
        await using GranitEndpointTestHost host = await StartAsync(
            new DelegatingServerValidator("Validation:Format:Iban", _ => false));
        using HttpClient client = host.CreateAnonymousClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{Prefix}/validate",
            new ValidationFieldValidateRequest("Validation:Format:Iban", "bad"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ValidationFieldValidateResponse? body = await response.Content
            .ReadFromJsonAsync<ValidationFieldValidateResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Status.ShouldBe(ValidationFieldStatus.Invalid);
    }

    [Fact]
    public async Task PostValidate_UnknownErrorCode_Returns404()
    {
        await using GranitEndpointTestHost host = await StartAsync();
        using HttpClient client = host.CreateAnonymousClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{Prefix}/validate",
            new ValidationFieldValidateRequest("Validation:Unknown", "x"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostValidate_SensitiveValidator_Anonymous_Returns404()
    {
        await using GranitEndpointTestHost host = await StartAsync(
            new DelegatingServerValidator("Validation:Format:UsSsn", _ => true, isSensitive: true));
        using HttpClient client = host.CreateAnonymousClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{Prefix}/validate",
            new ValidationFieldValidateRequest("Validation:Format:UsSsn", "123-45-6789"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostValidate_SensitiveValidator_Authenticated_Returns200()
    {
        await using GranitEndpointTestHost host = await StartAsync(
            new DelegatingServerValidator("Validation:Format:UsSsn", _ => true, isSensitive: true));
        using HttpClient client = host.CreateAuthenticatedClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{Prefix}/validate",
            new ValidationFieldValidateRequest("Validation:Format:UsSsn", "123-45-6789"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostValidate_EmptyErrorCode_Returns422()
    {
        await using GranitEndpointTestHost host = await StartAsync();
        using HttpClient client = host.CreateAnonymousClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{Prefix}/validate",
            new ValidationFieldValidateRequest("", "value"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // =========================================================================
    // POST /validation/validate-batch
    // =========================================================================

    [Fact]
    public async Task PostValidateBatch_MixedResults_Returns200WithEachStatus()
    {
        await using GranitEndpointTestHost host = await StartAsync(
            new DelegatingServerValidator("Validation:Format:Iban", v => v == "BE68539007547034"),
            new DelegatingServerValidator("Validation:Format:Email", _ => false));
        using HttpClient client = host.CreateAnonymousClient();

        ValidationFieldValidateBatchRequest request = new(
        [
            new("Validation:Format:Iban", "BE68539007547034"),
            new("Validation:Format:Email", "bad"),
            new("Validation:Unknown", "x"),
        ]);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{Prefix}/validate-batch", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ValidationFieldValidateBatchResponse? body = await response.Content
            .ReadFromJsonAsync<ValidationFieldValidateBatchResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Results.Count.ShouldBe(3);
        body.Results[0].Status.ShouldBe(ValidationFieldStatus.Valid);
        body.Results[1].Status.ShouldBe(ValidationFieldStatus.Invalid);
        body.Results[2].Status.ShouldBe(ValidationFieldStatus.ValidatorNotFound);
    }

    [Fact]
    public async Task PostValidateBatch_EmptyFields_Returns422()
    {
        await using GranitEndpointTestHost host = await StartAsync();
        using HttpClient client = host.CreateAnonymousClient();

        ValidationFieldValidateBatchRequest request = new([]);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{Prefix}/validate-batch", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PostValidateBatch_TooManyFields_Returns422()
    {
        await using GranitEndpointTestHost host = await StartAsync();
        using HttpClient client = host.CreateAnonymousClient();

        ValidationFieldValidateRequest[] fields =
            [.. Enumerable.Range(0, 25)
                .Select(i => new ValidationFieldValidateRequest($"Validation:X{i}", "v"))];

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{Prefix}/validate-batch",
            new ValidationFieldValidateBatchRequest(fields),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // =========================================================================
    // GET /validation/validators
    // =========================================================================

    [Fact]
    public async Task GetValidators_Anonymous_Returns200WithSortedCodes_HidesSensitive()
    {
        await using GranitEndpointTestHost host = await StartAsync(
            new DelegatingServerValidator("Validation:Format:Email", _ => true),
            new DelegatingServerValidator("Validation:Format:BicSwift", _ => true),
            new DelegatingServerValidator("Validation:Format:UsSsn", _ => true, isSensitive: true));
        using HttpClient client = host.CreateAnonymousClient();

        HttpResponseMessage response = await client.GetAsync(
            $"{Prefix}/validators", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<string>? codes = await response.Content
            .ReadFromJsonAsync<List<string>>(TestContext.Current.CancellationToken);
        codes.ShouldNotBeNull();
        codes.ShouldBe(["Validation:Format:BicSwift", "Validation:Format:Email"]);
    }

    [Fact]
    public async Task GetValidators_Authenticated_IncludesSensitive()
    {
        await using GranitEndpointTestHost host = await StartAsync(
            new DelegatingServerValidator("Validation:Format:Iban", _ => true),
            new DelegatingServerValidator("Validation:Format:UsSsn", _ => true, isSensitive: true));
        using HttpClient client = host.CreateAuthenticatedClient();

        HttpResponseMessage response = await client.GetAsync(
            $"{Prefix}/validators", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<string>? codes = await response.Content
            .ReadFromJsonAsync<List<string>>(TestContext.Current.CancellationToken);
        codes.ShouldNotBeNull();
        codes.Count.ShouldBe(2);
    }

    // =========================================================================
    // Custom prefix
    // =========================================================================

    [Fact]
    public async Task CustomPrefix_RoutesUnderProvidedPath()
    {
        await using GranitEndpointTestHost host = await GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddSingleton<IServerValidatorContributor>(new TestContributor([]));
                services.AddSingleton(sp =>
                    new ServerValidatorRegistry(
                        sp.GetRequiredService<IEnumerable<IServerValidatorContributor>>(),
                        NullLogger<ServerValidatorRegistry>.Instance));
                services.AddAuthorizationBuilder();
                services.AddScoped<IValidator<ValidationFieldValidateRequest>, ValidationFieldValidateRequestValidator>();
                services.AddScoped<IValidator<ValidationFieldValidateBatchRequest>, ValidationFieldValidateBatchRequestValidator>();
            },
            configureEndpoints: app => app.MapGranitValidation(opts => opts.RoutePrefix = "api/v1/validation"),
            cancellationToken: TestContext.Current.CancellationToken);

        using HttpClient client = host.CreateAnonymousClient();
        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/validation/validators", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private sealed class TestContributor(IServerValidator[] validators) : IServerValidatorContributor
    {
        public IEnumerable<IServerValidator> GetValidators() => validators;
    }
}
