using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentValidation;
using Granit.DataFiltering;
using Granit.Events;
using Granit.Identity.Local.Diagnostics;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Endpoints.Endpoints;
using Granit.Identity.Local.Endpoints.Extensions;
using Granit.Identity.Local.Endpoints.Options;
using Granit.Identity.Local.Endpoints.Permissions;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Granit.Settings.Services;
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
/// <see cref="AccountEndpointRouteBuilderExtensions.MapGranitAccount"/>.
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
    public IAuthenticatorTwoFactorService AuthenticatorTwoFactorService { get; }
    public IEmailTwoFactorService EmailTwoFactorService { get; }
    public IExternalLoginService ExternalLoginService { get; }
    public IExternalProviderRegistry ExternalProviderRegistry { get; }
    public IPasskeyService PasskeyService { get; }
    public IImpersonationService ImpersonationService { get; }
    public IAccountDeletionService DeletionService { get; }
    public IDistributedEventBus EventBus { get; }
    public IFusionCache FusionCache { get; }
    public ISettingProvider SettingProvider { get; }
    public TimeProvider TimeProvider { get; }
    public SignInManager<LocalIdentity> SignInManager { get; }
    public UserManager<LocalIdentity> UserManager { get; }
    public ICurrentTenant CurrentTenant { get; }
    public IDataFilter DataFilter { get; }

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
        IAuthenticatorTwoFactorService authenticatorTwoFactorService,
        IEmailTwoFactorService emailTwoFactorService,
        IExternalLoginService externalLoginService,
        IExternalProviderRegistry externalProviderRegistry,
        IPasskeyService passkeyService,
        IImpersonationService impersonationService,
        IAccountDeletionService deletionService,
        IDistributedEventBus eventBus,
        IFusionCache fusionCache,
        ISettingProvider settingProvider,
        TimeProvider timeProvider,
        SignInManager<LocalIdentity> signInManager,
        UserManager<LocalIdentity> userManager,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter)
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
        AuthenticatorTwoFactorService = authenticatorTwoFactorService;
        EmailTwoFactorService = emailTwoFactorService;
        ExternalLoginService = externalLoginService;
        ExternalProviderRegistry = externalProviderRegistry;
        PasskeyService = passkeyService;
        ImpersonationService = impersonationService;
        DeletionService = deletionService;
        EventBus = eventBus;
        FusionCache = fusionCache;
        SettingProvider = settingProvider;
        TimeProvider = timeProvider;
        SignInManager = signInManager;
        UserManager = userManager;
        CurrentTenant = currentTenant;
        DataFilter = dataFilter;
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
        IAuthenticatorTwoFactorService authenticatorTwoFactorService = Substitute.For<IAuthenticatorTwoFactorService>();
        IEmailTwoFactorService emailTwoFactorService = Substitute.For<IEmailTwoFactorService>();
        IExternalLoginService externalLoginService = Substitute.For<IExternalLoginService>();
        IExternalProviderRegistry externalProviderRegistry = Substitute.For<IExternalProviderRegistry>();
        IPasskeyService passkeyService = Substitute.For<IPasskeyService>();
        IImpersonationService impersonationService = Substitute.For<IImpersonationService>();
        IAccountDeletionService deletionService = Substitute.For<IAccountDeletionService>();
        IDistributedEventBus eventBus = Substitute.For<IDistributedEventBus>();
        IFusionCache fusionCache = Substitute.For<IFusionCache>();

        // Multi-tenancy stand-ins — default to "no tenant, no filter" so all existing
        // tests keep the pre-existing behaviour (filter untouched, no tenant switch).
        // Tests that exercise tenant-switching wire up their own return values via
        // the exposed CurrentTenant / DataFilter properties.
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDataFilter dataFilter = Substitute.For<IDataFilter>();

        ISettingProvider settingProvider = Substitute.For<ISettingProvider>();

        // Default: self-registration enabled (most tests expect registration to work)
        settingProvider.GetOrNullAsync(IdentityLocalSettingNames.AllowSelfRegistration, Arg.Any<CancellationToken>())
            .Returns("true");

        TimeProvider timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(FixedNow);

        // ASP.NET Identity mocks — SignInManager requires UserManager which requires IUserStore
        IUserStore<LocalIdentity> userStore = Substitute.For<IUserStore<LocalIdentity>>();
        UserManager<LocalIdentity> userManager = Substitute.For<UserManager<LocalIdentity>>(
            userStore, null, null, null, null, null, null, null, null);
        SignInManager<LocalIdentity> signInManager = Substitute.For<SignInManager<LocalIdentity>>(
            userManager,
            Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<LocalIdentity>>(),
            null, null, null, null);

        // Default: 2FA not enabled
        twoFactorService.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TwoFactorStatus(false, false, false, 0));
        twoFactorService.GetAvailableMethodsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<TwoFactorMethod>());

        // Default: no external logins
        externalLoginService.GetLoginsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ExternalLoginInfo>());

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        // Authentication — primary scheme for app sessions, plus a stub external
        // scheme so /external-logins/callback can verify provider resolution.
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { })
            .AddScheme<AuthenticationSchemeOptions, TestExternalAuthHandler>(
                IdentityConstants.ExternalScheme, _ => { });

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
        builder.Services.AddSingleton(authenticatorTwoFactorService);
        builder.Services.AddSingleton(emailTwoFactorService);
        builder.Services.AddSingleton(externalLoginService);
        builder.Services.AddSingleton(externalProviderRegistry);
        builder.Services.AddSingleton(passkeyService);
        builder.Services.AddSingleton(impersonationService);
        builder.Services.AddSingleton(deletionService);
        builder.Services.AddSingleton(eventBus);
        builder.Services.AddSingleton(fusionCache);
        builder.Services.AddSingleton(settingProvider);
        builder.Services.AddScoped<IdentityLocalConfigProvider>();
        builder.Services.AddSingleton(timeProvider);
        builder.Services.AddSingleton(signInManager);
        builder.Services.AddSingleton(userManager);
        builder.Services.AddSingleton(currentTenant);
        builder.Services.AddSingleton(dataFilter);
        builder.Services.AddSingleton<IPasswordHasher<LocalIdentity>, PasswordHasher<LocalIdentity>>();

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
        app.MapGranitAccount();
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
            authenticatorTwoFactorService, emailTwoFactorService,
            externalLoginService, externalProviderRegistry,
            passkeyService, impersonationService, deletionService,
            eventBus, fusionCache, settingProvider, timeProvider,
            signInManager, userManager, currentTenant, dataFilter);
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

/// <summary>
/// Fake authentication handler bound to <see cref="IdentityConstants.ExternalScheme"/>.
/// Authenticates when the <c>X-Test-External-Provider</c> header is present, and
/// stores its value as the <c>LoginProvider</c> property item — the well-known key
/// ASP.NET Core Identity reads via <c>SignInManager.GetExternalLoginInfoAsync</c>.
/// </summary>
internal sealed class TestExternalAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string ProviderHeader = "X-Test-External-Provider";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ProviderHeader, out Microsoft.Extensions.Primitives.StringValues providerHeader)
            || string.IsNullOrEmpty(providerHeader.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string provider = providerHeader.ToString();

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, "external-user-key"),
            new(ClaimTypes.Email, "external@example.com"),
        ];

        ClaimsIdentity identity = new(claims, IdentityConstants.ExternalScheme);
        ClaimsPrincipal principal = new(identity);

        AuthenticationProperties properties = new();
        properties.Items["LoginProvider"] = provider;

        AuthenticationTicket ticket = new(principal, properties, IdentityConstants.ExternalScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
