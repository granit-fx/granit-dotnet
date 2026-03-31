using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentValidation;
using Granit.Events;
using Granit.Identity;
using Granit.Identity.Local.Diagnostics;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Endpoints.Extensions;
using Granit.Identity.Local.Endpoints.Options;
using Granit.Identity.Local.Endpoints.Permissions;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ZiggyCreatures.Caching.Fusion;
using ExternalLoginInfo = Granit.Identity.Local.Services.ExternalLoginInfo;

namespace Granit.Identity.Local.Endpoints.Tests.Integration;

/// <summary>
/// Test server that boots a minimal ASP.NET Core application with mocked services
/// and the account endpoints registered via
/// <see cref="AccountEndpointRouteBuilderExtensions.MapAccountEndpoints"/>.
/// </summary>
internal sealed class AccountEndpointsTestServer : IAsyncDisposable
{
    public static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    public static readonly string TestUserIdString = TestUserId.ToString();
    public static readonly Guid ImpersonatorUserId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    public static readonly string ImpersonatorUserIdString = ImpersonatorUserId.ToString();
    public static readonly DateTimeOffset FixedNow = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private const string AuthenticatedRole = "authenticated";
    private const string ImpersonateRole = "impersonate";

    private readonly WebApplication _app;

    public HttpClient AuthenticatedClient { get; }
    public HttpClient AnonymousClient { get; }

    /// <summary>
    /// Client that simulates an impersonation session (has <c>impersonator_id</c> claim).
    /// </summary>
    public HttpClient ImpersonatedClient { get; }

    public IIdentityProvider IdentityProvider { get; }
    public IIdentityCredentialVerifier CredentialVerifier { get; }
    public IIdentityUserReader UserReader { get; }
    public IIdentityUserWriter UserWriter { get; }
    public IIdentityPasswordManager PasswordManager { get; }
    public IEmailConfirmationService EmailConfirmation { get; }
    public IPasswordResetService PasswordResetService { get; }
    public ITwoFactorService TwoFactorService { get; }
    public IExternalLoginService ExternalLoginService { get; }
    public IExternalProviderRegistry ExternalProviderRegistry { get; }
    public IPasskeyService PasskeyService { get; }
    public IImpersonationService ImpersonationService { get; }
    public IAccountDeletionService DeletionService { get; }
    public IDistributedEventBus EventBus { get; }
    public IFusionCache FusionCache { get; }
    public TimeProvider TimeProvider { get; }
    public SignInManager<GranitUser> SignInManager { get; }
    public UserManager<GranitUser> UserManager { get; }

    private AccountEndpointsTestServer(
        WebApplication app,
        HttpClient authenticatedClient,
        HttpClient anonymousClient,
        HttpClient impersonatedClient,
        IIdentityProvider identityProvider,
        IIdentityCredentialVerifier credentialVerifier,
        IIdentityUserReader userReader,
        IIdentityUserWriter userWriter,
        IIdentityPasswordManager passwordManager,
        IEmailConfirmationService emailConfirmation,
        IPasswordResetService passwordResetService,
        ITwoFactorService twoFactorService,
        IExternalLoginService externalLoginService,
        IExternalProviderRegistry externalProviderRegistry,
        IPasskeyService passkeyService,
        IImpersonationService impersonationService,
        IAccountDeletionService deletionService,
        IDistributedEventBus eventBus,
        IFusionCache fusionCache,
        TimeProvider timeProvider,
        SignInManager<GranitUser> signInManager,
        UserManager<GranitUser> userManager)
    {
        _app = app;
        AuthenticatedClient = authenticatedClient;
        AnonymousClient = anonymousClient;
        ImpersonatedClient = impersonatedClient;
        IdentityProvider = identityProvider;
        CredentialVerifier = credentialVerifier;
        UserReader = userReader;
        UserWriter = userWriter;
        PasswordManager = passwordManager;
        EmailConfirmation = emailConfirmation;
        PasswordResetService = passwordResetService;
        TwoFactorService = twoFactorService;
        ExternalLoginService = externalLoginService;
        ExternalProviderRegistry = externalProviderRegistry;
        PasskeyService = passkeyService;
        ImpersonationService = impersonationService;
        DeletionService = deletionService;
        EventBus = eventBus;
        FusionCache = fusionCache;
        TimeProvider = timeProvider;
        SignInManager = signInManager;
        UserManager = userManager;
    }

    public static async Task<AccountEndpointsTestServer> CreateAsync()
    {
        // Service mocks
        IIdentityProvider identityProvider = Substitute.For<IIdentityProvider>();
        IIdentityCredentialVerifier credentialVerifier = Substitute.For<IIdentityCredentialVerifier>();
        IIdentityUserReader userReader = Substitute.For<IIdentityUserReader>();
        IIdentityUserWriter userWriter = Substitute.For<IIdentityUserWriter>();
        IIdentityPasswordManager passwordManager = Substitute.For<IIdentityPasswordManager>();
        IEmailConfirmationService emailConfirmation = Substitute.For<IEmailConfirmationService>();
        IPasswordResetService passwordResetService = Substitute.For<IPasswordResetService>();
        ITwoFactorService twoFactorService = Substitute.For<ITwoFactorService>();
        IExternalLoginService externalLoginService = Substitute.For<IExternalLoginService>();
        IExternalProviderRegistry externalProviderRegistry = Substitute.For<IExternalProviderRegistry>();
        IPasskeyService passkeyService = Substitute.For<IPasskeyService>();
        IImpersonationService impersonationService = Substitute.For<IImpersonationService>();
        IAccountDeletionService deletionService = Substitute.For<IAccountDeletionService>();
        IDistributedEventBus eventBus = Substitute.For<IDistributedEventBus>();
        IFusionCache fusionCache = Substitute.For<IFusionCache>();

        TimeProvider timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(FixedNow);

        // ASP.NET Identity mocks — SignInManager requires UserManager which requires IUserStore
        IUserStore<GranitUser> userStore = Substitute.For<IUserStore<GranitUser>>();
        UserManager<GranitUser> userManager = Substitute.For<UserManager<GranitUser>>(
            userStore, null, null, null, null, null, null, null, null);
        SignInManager<GranitUser> signInManager = Substitute.For<SignInManager<GranitUser>>(
            userManager,
            Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<GranitUser>>(),
            null, null, null, null);

        // Default: 2FA not enabled
        twoFactorService.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TwoFactorStatus(false, false, 0));

        // Default: no external logins
        externalLoginService.GetLoginsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ExternalLoginInfo>());

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        // Authentication
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        // Authorization policies matching the permission names used by the endpoints
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(IdentityLocalPermissions.Users.Impersonate,
                policy => policy.RequireRole(ImpersonateRole));

        // Service mocks
        builder.Services.AddSingleton(identityProvider);
        builder.Services.AddSingleton(credentialVerifier);
        builder.Services.AddSingleton(userReader);
        builder.Services.AddSingleton(userWriter);
        builder.Services.AddSingleton(passwordManager);
        builder.Services.AddSingleton(emailConfirmation);
        builder.Services.AddSingleton(passwordResetService);
        builder.Services.AddSingleton(twoFactorService);
        builder.Services.AddSingleton(externalLoginService);
        builder.Services.AddSingleton(externalProviderRegistry);
        builder.Services.AddSingleton(passkeyService);
        builder.Services.AddSingleton(impersonationService);
        builder.Services.AddSingleton(deletionService);
        builder.Services.AddSingleton(eventBus);
        builder.Services.AddSingleton(fusionCache);
        builder.Services.AddSingleton(timeProvider);
        builder.Services.AddSingleton(signInManager);
        builder.Services.AddSingleton(userManager);

        // Options
        builder.Services.AddSingleton(
            Microsoft.Extensions.Options.Options.Create(new AccountEndpointsOptions()));

        // Real metrics (needs a real IMeterFactory)
        builder.Services.AddMetrics();
        builder.Services.AddSingleton<IdentityLocalMetrics>();

        // Validators from the Identity.Local.Endpoints assembly
        builder.Services.AddValidatorsFromAssemblyContaining<AccountEndpointsOptions>(
            ServiceLifetime.Singleton, includeInternalTypes: true);

        WebApplication app = builder.Build();
        app.MapAccountEndpoints();
        await app.StartAsync().ConfigureAwait(false);

        string allRoles = string.Join(",", AuthenticatedRole, ImpersonateRole);

        HttpClient authenticatedClient = app.GetTestClient();
        authenticatedClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, allRoles);

        HttpClient anonymousClient = app.GetTestClient();

        HttpClient impersonatedClient = app.GetTestClient();
        impersonatedClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, allRoles);
        impersonatedClient.DefaultRequestHeaders.Add(
            TestAuthHandler.ImpersonatorIdHeader, ImpersonatorUserIdString);

        return new AccountEndpointsTestServer(
            app, authenticatedClient, anonymousClient, impersonatedClient,
            identityProvider, credentialVerifier, userReader, userWriter, passwordManager,
            emailConfirmation, passwordResetService, twoFactorService,
            externalLoginService, externalProviderRegistry,
            passkeyService, impersonationService, deletionService,
            eventBus, fusionCache, timeProvider,
            signInManager, userManager);
    }

    public async ValueTask DisposeAsync()
    {
        AuthenticatedClient.Dispose();
        AnonymousClient.Dispose();
        ImpersonatedClient.Dispose();
        await _app.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Fake authentication handler that authenticates when the <c>X-Test-Roles</c> header
/// is present. The authenticated user has a fixed <c>sub</c> claim set to
/// <see cref="AccountEndpointsTestServer.TestUserId"/>.
/// </summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";
    public const string ImpersonatorIdHeader = "X-Test-Impersonator-Id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues rolesHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string[] roles = rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        List<Claim> claims =
        [
            new("sub", AccountEndpointsTestServer.TestUserIdString),
            new(ClaimTypes.NameIdentifier, AccountEndpointsTestServer.TestUserIdString),
            new(ClaimTypes.Name, "test-user"),
            new("preferred_username", "test-user"),
            new("name", "test-user"),
            new("email", "test@example.com"),
            new("email_verified", "true"),
            new("jti", Guid.NewGuid().ToString()),
            .. roles.Select(r => new Claim(ClaimTypes.Role, r.Trim())),
        ];

        // Support impersonation testing via X-Test-Impersonator-Id header
        if (Request.Headers.TryGetValue(ImpersonatorIdHeader, out Microsoft.Extensions.Primitives.StringValues impersonatorId)
            && !string.IsNullOrEmpty(impersonatorId.ToString()))
        {
            claims.Add(new Claim("impersonator_id", impersonatorId.ToString()));
            claims.Add(new Claim("impersonator_name", "admin@example.com"));
        }

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
