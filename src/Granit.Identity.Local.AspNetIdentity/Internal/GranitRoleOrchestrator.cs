using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Guids;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// Coordinates the dual write between <see cref="GranitRole"/> (Identity DbContext) and
/// <see cref="RoleMetadata"/> (host DbContext that implements <c>IPermissionGrantDbContext</c>).
/// </summary>
/// <remarks>
/// <para>
/// Uses a compensating-write strategy rather than a cross-DbContext transaction: the role
/// is created in the Identity DbContext first, then the metadata is persisted via
/// <see cref="IRoleMetadataStore"/>. If the metadata write fails the GranitRole is
/// deleted so the invariant "every Granit-managed role has matching metadata" converges.
/// </para>
/// <para>
/// This avoids the MSDTC escalation hazard of <c>TransactionScope</c> on Linux with
/// Npgsql and works regardless of whether both DbContexts share a physical database.
/// Deployments that guarantee both contexts target the same database and can expose their
/// connection across assemblies may replace this with a shared-connection EF Core
/// transaction (<c>Database.OpenConnectionAsync</c> / <c>SetDbConnection</c> /
/// <c>BeginTransactionAsync</c> / <c>UseTransactionAsync</c>).
/// </para>
/// </remarks>
internal sealed partial class GranitRoleOrchestrator(
    RoleManager<GranitRole> roleManager,
    IRoleMetadataStore roleMetadataStore,
    IGuidGenerator guidGenerator,
    ILogger<GranitRoleOrchestrator> logger) : IGranitRoleOrchestrator
{
    /// <inheritdoc />
    public async Task<RoleMetadata> CreateAsync(
        CreateRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Guid roleId = guidGenerator.Create();
        GranitRole granitRole = new()
        {
            Id = roleId,
            Name = command.Name,
            Description = command.Description,
        };

        IdentityResult createResult = await roleManager.CreateAsync(granitRole).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create GranitRole '{command.Name}': " +
                string.Join("; ", createResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }

        try
        {
            var metadata = RoleMetadata.Create(
                roleId,
                command.Name,
                command.MultiTenancySide,
                command.TenantId,
                command.ClientId,
                command.Description,
                command.IsSystem);

            await roleMetadataStore.AddAsync(metadata, cancellationToken).ConfigureAwait(false);
            return metadata;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogMetadataFailedCompensating(logger, ex, roleId, command.Name);
            await CompensateDeleteAsync(granitRole).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<RoleMetadata> RenameAsync(
        Guid roleId,
        string newName,
        string? newDescription,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        RoleMetadata? existingMetadata = await roleMetadataStore.FindByIdAsync(roleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No RoleMetadata found for id {roleId:D}.");

        if (existingMetadata.IsSystem)
        {
            throw new InvalidOperationException(
                $"Role '{existingMetadata.Name}' is a system role and cannot be renamed.");
        }

        GranitRole? granitRole = await roleManager.FindByIdAsync(roleId.ToString("D"))
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No GranitRole found for id {roleId:D}.");

        string previousName = granitRole.Name!;
        string? previousDescription = granitRole.Description;

        granitRole.Name = newName;
        granitRole.Description = newDescription;
        IdentityResult updateResult = await roleManager.UpdateAsync(granitRole).ConfigureAwait(false);
        if (!updateResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to update GranitRole '{previousName}': " +
                string.Join("; ", updateResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }

        try
        {
            existingMetadata.Rename(newName, newDescription);
            await roleMetadataStore.UpdateAsync(existingMetadata, cancellationToken).ConfigureAwait(false);
            return existingMetadata;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRenameFailedCompensating(logger, ex, roleId, newName);
            granitRole.Name = previousName;
            granitRole.Description = previousDescription;
            try
            {
                await roleManager.UpdateAsync(granitRole).ConfigureAwait(false);
            }
            catch (Exception compEx) when (compEx is not OperationCanceledException)
            {
                LogCompensationFailed(logger, compEx, roleId);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        RoleMetadata? metadata = await roleMetadataStore.FindByIdAsync(roleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No RoleMetadata found for id {roleId:D}.");

        if (metadata.IsSystem)
        {
            throw new InvalidOperationException(
                $"Role '{metadata.Name}' is a system role and cannot be deleted.");
        }

        GranitRole? granitRole = await roleManager.FindByIdAsync(roleId.ToString("D")).ConfigureAwait(false);

        await roleMetadataStore.RemoveAsync(metadata, cancellationToken).ConfigureAwait(false);

        if (granitRole is not null)
        {
            IdentityResult deleteResult = await roleManager.DeleteAsync(granitRole).ConfigureAwait(false);
            if (!deleteResult.Succeeded)
            {
                // Metadata is already gone — surface the error so the operator knows
                // a GranitRole orphan is now in the Identity store. The FK-free nature
                // of the dual model means no referential integrity breakage, but the
                // role should be cleaned up.
                throw new InvalidOperationException(
                    $"RoleMetadata for '{metadata.Name}' deleted but GranitRole cleanup failed: " +
                    string.Join("; ", deleteResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
            }
        }
    }

    private async Task CompensateDeleteAsync(GranitRole granitRole)
    {
        try
        {
            IdentityResult deleteResult = await roleManager.DeleteAsync(granitRole).ConfigureAwait(false);
            if (!deleteResult.Succeeded)
            {
                LogCompensationFailed(logger, exception: null, granitRole.Id);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCompensationFailed(logger, ex, granitRole.Id);
        }
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "RoleMetadata creation failed for role {RoleId} ('{RoleName}') — compensating by deleting GranitRole.")]
    private static partial void LogMetadataFailedCompensating(
        ILogger logger, Exception exception, Guid roleId, string roleName);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "RoleMetadata rename failed for role {RoleId} → '{NewName}' — compensating by reverting GranitRole.")]
    private static partial void LogRenameFailedCompensating(
        ILogger logger, Exception exception, Guid roleId, string newName);

    [LoggerMessage(Level = LogLevel.Critical,
        Message = "Compensation action failed for role {RoleId} — manual cleanup required (orphan GranitRole or RoleMetadata row).")]
    private static partial void LogCompensationFailed(
        ILogger logger, Exception? exception, Guid roleId);
}
