using Granit.Authentication.JwtBearer.BackChannelLogout;
using Granit.Authentication.JwtBearer.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.Tests.BackChannelLogout;

public sealed class BackChannelLogoutEndpointAdditionalTests
{
    private readonly BackChannelLogoutTokenValidator _validator;
    private readonly IRevokedSessionStore _store = Substitute.For<IRevokedSessionStore>();
    private readonly IOptions<JwtBearerAuthOptions> _options;
    private readonly ILogger<BackChannelLogoutTokenValidator> _logger;

    public BackChannelLogoutEndpointAdditionalTests()
    {
        JwtBearerAuthOptions authOptions = new()
        {
            Authority = "https://keycloak.test/realms/test",
            Audience = "test-client",
            RequireHttpsMetadata = false,
            BackChannelLogout = new BackChannelLogoutOptions
            {
                Enabled = true,
                SessionRevocationTtl = TimeSpan.FromMinutes(45),
            },
        };
        _options = Microsoft.Extensions.Options.Options.Create(authOptions);
        _logger = Substitute.For<ILogger<BackChannelLogoutTokenValidator>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _validator = Substitute.ForPartsOf<BackChannelLogoutTokenValidator>(_options, _logger);
    }

    [Fact]
    public async Task HandleAsync_CustomTtl_PassesConfiguredTtlToStore()
    {
        // Arrange
        HttpRequest request = CreateFormRequest("logout_token", "valid-jwt-token");
        _validator.ValidateAsync("valid-jwt-token", Arg.Any<CancellationToken>())
            .Returns(new BackChannelLogoutResult(true, "session-abc", null, null));

        // Act
        await BackChannelLogoutEndpoint.HandleAsync(
            request, _validator, _store, _options, _logger, TestContext.Current.CancellationToken);

        // Assert — uses configured TTL of 45 minutes
        await _store.Received(1).RevokeSessionAsync(
            "session-abc",
            TimeSpan.FromMinutes(45),
            Arg.Any<CancellationToken>());
    }

    private static HttpRequest CreateFormRequest(string key, string value)
    {
        DefaultHttpContext context = new();
        FormCollection form = new(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            [key] = value,
        });
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = form;
        return context.Request;
    }
}

public sealed class FormContentTypeEndpointFilterTests
{
    [Fact]
    public async Task InvokeAsync_NonFormContentType_Returns400()
    {
        // Arrange
        FormContentTypeEndpointFilter filter = new();
        DefaultHttpContext httpContext = new();
        httpContext.Request.ContentType = "application/json";

        EndpointFilterInvocationContext context = new DefaultEndpointFilterInvocationContext(httpContext);
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(TypedResults.Ok());

        // Act
        object? result = await filter.InvokeAsync(context, next);

        // Assert
        result.ShouldBeOfType<ProblemHttpResult>()
            .StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task InvokeAsync_FormContentType_CallsNext()
    {
        // Arrange
        FormContentTypeEndpointFilter filter = new();
        DefaultHttpContext httpContext = new();
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";

        EndpointFilterInvocationContext context = new DefaultEndpointFilterInvocationContext(httpContext);
        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(TypedResults.Ok());
        };

        // Act
        await filter.InvokeAsync(context, next);

        // Assert
        nextCalled.ShouldBeTrue();
    }
}
