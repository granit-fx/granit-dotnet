using Granit.Authentication.JwtBearer.GoogleCloud.Authentication;
using Granit.Authentication.JwtBearer.GoogleCloud.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.JwtBearer.GoogleCloud.Extensions;

/// <summary>
/// Extensions to configure Google Cloud Identity Platform (Firebase Auth) on top of
/// <c>Granit.Authentication.JwtBearer</c>.
/// </summary>
public static class GoogleCloudAuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Overrides JWT Bearer configuration with Firebase Auth values,
    /// registers <see cref="GoogleCloudClaimsTransformation"/> and the <c>"Admin"</c> policy.
    /// </summary>
    public static IServiceCollection AddGranitGoogleCloudAuthentication(
        this IServiceCollection services)
    {
        services
            .AddOptions<GoogleCloudAuthenticationOptions>()
            .BindConfiguration(GoogleCloudAuthenticationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // PostConfigure runs after AddGranitJwtBearer (GranitJwtBearerModule),
        // overriding Authority, Audience, and NameClaimType for Firebase Auth.
        // Deferred configuration: reads GoogleCloudAuthenticationOptions at resolution time.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .PostConfigure<IOptions<GoogleCloudAuthenticationOptions>>((jwt, gcOpts) =>
            {
                GoogleCloudAuthenticationOptions options = gcOpts.Value;
                jwt.Authority = options.Authority;
                jwt.Audience = options.ProjectId;
                jwt.RequireHttpsMetadata = options.RequireHttpsMetadata;
                // Firebase Auth uses "user_id" for the subject claim in custom tokens,
                // but standard Firebase ID tokens use "sub". Use "email" as name claim.
                jwt.TokenValidationParameters.NameClaimType = "email";
                jwt.TokenValidationParameters.ValidIssuer = options.Authority;
                jwt.TokenValidationParameters.ValidAudience = options.ProjectId;
            });

        services.AddTransient<IClaimsTransformation, GoogleCloudClaimsTransformation>();

        // Deferred Admin policy: reads AdminRole from GoogleCloudAuthenticationOptions at resolution time.
        services
            .AddOptions<AuthorizationOptions>()
            .Configure<IOptions<GoogleCloudAuthenticationOptions>>((authOpts, gcOpts) =>
            {
                authOpts.AddPolicy("Admin",
                    policy => policy.RequireRole(gcOpts.Value.AdminRole));
            });

        return services;
    }
}
