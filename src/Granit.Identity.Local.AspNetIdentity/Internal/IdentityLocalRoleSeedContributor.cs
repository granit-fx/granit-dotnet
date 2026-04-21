using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// Seeds the three platform-provided system roles and their <see cref="RoleMetadata"/> rows:
/// <list type="bullet">
///   <item><c>SuperAdmin</c> — <see cref="MultiTenancySide.Host"/>, platform administrator.</item>
///   <item><c>TenantAdministrator</c> — <see cref="MultiTenancySide.Both"/>, defined globally, assignable per tenant.</item>
///   <item><c>User</c> — <see cref="MultiTenancySide.Both"/>, default role for new users.</item>
/// </list>
/// </summary>
/// <remarks>
/// Idempotent: each role is created only if no matching <see cref="RoleMetadata"/> row
/// already exists for the <c>(Name, TenantId, ClientId)</c> triplet. System roles flagged
/// <see cref="RoleMetadata.IsSystem"/> are protected from CRUD-endpoint deletion / rename.
/// </remarks>
internal sealed partial class IdentityLocalRoleSeedContributor(
    IGranitRoleOrchestrator orchestrator,
    IRoleMetadataStore roleMetadataStore,
    RoleManager<GranitRole> roleManager,
    ILogger<IdentityLocalRoleSeedContributor> logger) : IHostDataSeedContributor
{
    private static readonly SystemRoleDescriptor[] SystemRoles =
    [
        new("SuperAdmin", MultiTenancySide.Host, "Platform administrator with cross-tenant access."),
        new("TenantAdministrator", MultiTenancySide.Both, "Administrator within a tenant — can manage tenant users, roles, and settings."),
        new("User", MultiTenancySide.Both, "Default role assigned to every authenticated user."),
    ];

    /// <inheritdoc />
    public async Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default)
    {
        foreach (SystemRoleDescriptor descriptor in SystemRoles)
        {
            await SeedSystemRoleAsync(descriptor, cancellationToken).ConfigureAwait(false);
        }

        LogSeedingCompleted(logger, SystemRoles.Length);
    }

    private async Task SeedSystemRoleAsync(SystemRoleDescriptor descriptor, CancellationToken cancellationToken)
    {
        RoleMetadata? existingMetadata = await roleMetadataStore
            .FindByNameAsync(descriptor.Name, tenantId: null, clientId: null, cancellationToken)
            .ConfigureAwait(false);

        if (existingMetadata is not null)
        {
            LogRoleUnchanged(logger, descriptor.Name);
            return;
        }

        // Repair path: a GranitRole may exist without RoleMetadata (partial failure of a
        // previous seed). Reuse it so we don't hit the unique-name index.
        GranitRole? existingRole = await roleManager.FindByNameAsync(descriptor.Name).ConfigureAwait(false);
        if (existingRole is not null)
        {
            await roleManager.DeleteAsync(existingRole).ConfigureAwait(false);
            LogOrphanGranitRoleRemoved(logger, descriptor.Name);
        }

        await orchestrator.CreateAsync(
            new CreateRoleCommand(
                Name: descriptor.Name,
                MultiTenancySide: descriptor.Side,
                TenantId: null,
                ClientId: null,
                Description: descriptor.Description,
                IsSystem: true),
            cancellationToken).ConfigureAwait(false);

        LogRoleCreated(logger, descriptor.Name, descriptor.Side);
    }

    private sealed record SystemRoleDescriptor(string Name, MultiTenancySide Side, string Description);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Identity.Local system role seeding completed — {RoleCount} role(s) verified.")]
    private static partial void LogSeedingCompleted(ILogger logger, int roleCount);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "System role '{RoleName}' already exists — skipping.")]
    private static partial void LogRoleUnchanged(ILogger logger, string roleName);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Created system role '{RoleName}' with side {Side}.")]
    private static partial void LogRoleCreated(ILogger logger, string roleName, MultiTenancySide side);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Orphan GranitRole '{RoleName}' without RoleMetadata removed before re-seed.")]
    private static partial void LogOrphanGranitRoleRemoved(ILogger logger, string roleName);
}
