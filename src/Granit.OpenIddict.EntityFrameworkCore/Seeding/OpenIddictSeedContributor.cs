using Granit.OpenIddict.Options;
using Granit.Persistence.DataSeeding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.EntityFrameworkCore.Seeding;

/// <summary>
/// Seeds OIDC applications and scopes at startup using OpenIddict managers.
/// Idempotent: upserts based on <c>ClientId</c> / <c>Name</c>.
/// </summary>
internal sealed partial class OpenIddictSeedContributor(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictScopeManager scopeManager,
    IOptions<GranitOpenIddictSeedingOptions> options,
    ILogger<OpenIddictSeedContributor> logger) : IDataSeedContributor
{
    /// <inheritdoc/>
    public async Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default)
    {
        GranitOpenIddictSeedingOptions seedOptions = options.Value;

        if (seedOptions.Applications.Length == 0 && seedOptions.Scopes.Length == 0)
        {
            Log.NoSeedData(logger);
            return;
        }

        Log.SeedingStarted(logger, seedOptions.Applications.Length, seedOptions.Scopes.Length);

        foreach (OidcApplicationSeedDescriptor app in seedOptions.Applications)
        {
            await SeedApplicationAsync(app, cancellationToken).ConfigureAwait(false);
        }

        foreach (OidcScopeSeedDescriptor scope in seedOptions.Scopes)
        {
            await SeedScopeAsync(scope, cancellationToken).ConfigureAwait(false);
        }

        Log.SeedingCompleted(logger);
    }

    private async Task SeedApplicationAsync(
        OidcApplicationSeedDescriptor descriptor, CancellationToken cancellationToken)
    {
        object? existing = await applicationManager.FindByClientIdAsync(descriptor.ClientId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var appDescriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = descriptor.ClientId,
                ClientSecret = descriptor.ClientSecret,
                DisplayName = descriptor.DisplayName,
            };

            foreach (string permission in descriptor.Permissions)
            {
                appDescriptor.Permissions.Add(permission);
            }

            foreach (string uri in descriptor.RedirectUris)
            {
                appDescriptor.RedirectUris.Add(new Uri(uri));
            }

            foreach (string uri in descriptor.PostLogoutRedirectUris)
            {
                appDescriptor.PostLogoutRedirectUris.Add(new Uri(uri));
            }

            await applicationManager.CreateAsync(appDescriptor, cancellationToken).ConfigureAwait(false);
            Log.ApplicationCreated(logger, descriptor.ClientId);
        }
        else
        {
            var appDescriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(appDescriptor, existing, cancellationToken)
                .ConfigureAwait(false);

            appDescriptor.DisplayName = descriptor.DisplayName;

            appDescriptor.Permissions.Clear();
            foreach (string permission in descriptor.Permissions)
            {
                appDescriptor.Permissions.Add(permission);
            }

            appDescriptor.RedirectUris.Clear();
            foreach (string uri in descriptor.RedirectUris)
            {
                appDescriptor.RedirectUris.Add(new Uri(uri));
            }

            appDescriptor.PostLogoutRedirectUris.Clear();
            foreach (string uri in descriptor.PostLogoutRedirectUris)
            {
                appDescriptor.PostLogoutRedirectUris.Add(new Uri(uri));
            }

            await applicationManager.UpdateAsync(existing, appDescriptor, cancellationToken)
                .ConfigureAwait(false);
            Log.ApplicationUpdated(logger, descriptor.ClientId);
        }
    }

    private async Task SeedScopeAsync(
        OidcScopeSeedDescriptor descriptor, CancellationToken cancellationToken)
    {
        object? existing = await scopeManager.FindByNameAsync(descriptor.Name, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var scopeDescriptor = new OpenIddictScopeDescriptor
            {
                Name = descriptor.Name,
                DisplayName = descriptor.DisplayName,
            };

            foreach (string resource in descriptor.Resources)
            {
                scopeDescriptor.Resources.Add(resource);
            }

            await scopeManager.CreateAsync(scopeDescriptor, cancellationToken).ConfigureAwait(false);
            Log.ScopeCreated(logger, descriptor.Name);
        }
        else
        {
            var scopeDescriptor = new OpenIddictScopeDescriptor();
            await scopeManager.PopulateAsync(scopeDescriptor, existing, cancellationToken)
                .ConfigureAwait(false);

            scopeDescriptor.DisplayName = descriptor.DisplayName;

            scopeDescriptor.Resources.Clear();
            foreach (string resource in descriptor.Resources)
            {
                scopeDescriptor.Resources.Add(resource);
            }

            await scopeManager.UpdateAsync(existing, scopeDescriptor, cancellationToken)
                .ConfigureAwait(false);
            Log.ScopeUpdated(logger, descriptor.Name);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "No OpenIddict seed data configured — skipping.")]
        public static partial void NoSeedData(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Seeding {ApplicationCount} OIDC application(s) and {ScopeCount} scope(s).")]
        public static partial void SeedingStarted(ILogger logger, int applicationCount, int scopeCount);

        [LoggerMessage(Level = LogLevel.Information, Message = "OpenIddict seeding completed.")]
        public static partial void SeedingCompleted(ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Created OIDC application '{ClientId}'.")]
        public static partial void ApplicationCreated(ILogger logger, string clientId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Updated OIDC application '{ClientId}'.")]
        public static partial void ApplicationUpdated(ILogger logger, string clientId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Created OIDC scope '{ScopeName}'.")]
        public static partial void ScopeCreated(ILogger logger, string scopeName);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Updated OIDC scope '{ScopeName}'.")]
        public static partial void ScopeUpdated(ILogger logger, string scopeName);
    }
}
