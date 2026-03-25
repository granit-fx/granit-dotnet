using Granit.Authentication.JwtBearer.Cognito.Authentication;
using Granit.Authentication.JwtBearer.Cognito.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.JwtBearer.Cognito.Extensions;

/// <summary>
/// Extensions to configure AWS Cognito on top of <c>Granit.Authentication.JwtBearer</c>.
/// </summary>
public static class CognitoServiceCollectionExtensions
{
    /// <summary>
    /// Overrides JWT Bearer configuration with Cognito values
    /// and registers <see cref="CognitoClaimsTransformation"/>.
    /// </summary>
    public static IServiceCollection AddGranitCognito(
        this IServiceCollection services)
    {
        services
            .AddOptions<CognitoOptions>()
            .BindConfiguration(CognitoOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // PostConfigure runs after AddGranitJwtBearer (GranitAuthenticationJwtBearerModule),
        // overriding Authority, Audience, and NameClaimType for Cognito.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .PostConfigure<IOptions<CognitoOptions>>((jwt, cognitoOpts) =>
            {
                CognitoOptions options = cognitoOpts.Value;
                string audience = options.Audience ?? options.ClientId;
                jwt.Authority = options.Authority;
                jwt.Audience = audience;
                jwt.RequireHttpsMetadata = options.RequireHttpsMetadata;
                // Cognito uses "cognito:username" or "username" — default to "sub" for user ID
                jwt.TokenValidationParameters.NameClaimType = "cognito:username";
                jwt.TokenValidationParameters.ValidIssuer = options.Authority;
                jwt.TokenValidationParameters.ValidAudience = audience;
            });

        services.AddTransient<IClaimsTransformation, CognitoClaimsTransformation>();

        return services;
    }
}
