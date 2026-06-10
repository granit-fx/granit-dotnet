using Granit.Guids;
using Granit.Identity.Domain;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Options;
using Granit.Timing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Local.AspNetIdentity;

/// <summary>
/// Custom <see cref="UserManager{TUser}"/> that implements exponential backoff lockout
/// and keeps the canonical <see cref="User"/> aggregate in sync on creation
/// (ADR-051 B-step 2.5).
/// </summary>
/// <remarks>
/// <para>
/// Overrides <see cref="AccessFailedAsync"/> to compute an exponentially increasing
/// lockout duration based on <see cref="LocalIdentity.ConsecutiveLockouts"/>. The stock
/// <see cref="SignInManager{TUser}"/> calls <c>AccessFailedAsync</c> on each failed
/// login — this is the single, stable extension point that avoids overriding
/// <c>SignInManager</c> (fragile across .NET upgrades).
/// </para>
/// <para>
/// The formula is: <c>min(BaseDuration * ExponentialBase^(n-1), MaxDuration)</c>
/// where <c>n</c> is the consecutive lockout count. The counter resets on successful
/// login (<see cref="ResetAccessFailedCountAsync"/>), password reset, or admin unlock.
/// </para>
/// <para>
/// Overrides <see cref="CreateAsync(LocalIdentity)"/> to auto-create the
/// matching <see cref="User"/> row (per ADR-051) before persisting the
/// <see cref="LocalIdentity"/>. The two records share the same Guid so
/// historical references continue to resolve. If the
/// <see cref="LocalIdentity"/> insert fails, the freshly-created
/// <see cref="User"/> row is compensated via
/// <see cref="IUserDirectoryWriter.DeleteAsync"/>.
/// </para>
/// </remarks>
public class LocalIdentityManager(
    IUserStore<LocalIdentity> store,
    IOptions<IdentityOptions> optionsAccessor,
    IPasswordHasher<LocalIdentity> passwordHasher,
    IEnumerable<IUserValidator<LocalIdentity>> userValidators,
    IEnumerable<IPasswordValidator<LocalIdentity>> passwordValidators,
    ILookupNormalizer keyNormalizer,
    IdentityErrorDescriber errors,
    IServiceProvider services,
    ILogger<LocalIdentityManager> logger,
    IOptions<GranitLockoutOptions> lockoutOptions,
    IUserDirectoryWriter userDirectoryWriter,
    IGuidGenerator guidGenerator,
    IClock clock)
    : UserManager<LocalIdentity>(store, optionsAccessor, passwordHasher,
        userValidators, passwordValidators, keyNormalizer, errors, services, logger)
{
    private readonly GranitLockoutOptions _lockoutOptions = lockoutOptions.Value;
    private readonly IUserDirectoryWriter _userDirectoryWriter = userDirectoryWriter;
    private readonly IGuidGenerator _guidGenerator = guidGenerator;
    private readonly IClock _clock = clock;

    /// <inheritdoc/>
    public override async Task<IdentityResult> CreateAsync(LocalIdentity user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.Id == Guid.Empty)
        {
            user.Id = _guidGenerator.Create();
        }

        user.UserId = user.Id;

        var canonical = User.Create(
            id: user.Id,
            email: user.Email ?? string.Empty,
            displayName: ResolveDisplayName(user),
            firstName: user.FirstName,
            lastName: user.LastName,
            phoneNumber: user.PhoneNumber,
            tenantId: user.TenantId);

        await _userDirectoryWriter.CreateAsync(canonical).ConfigureAwait(false);

        IdentityResult result;
        try
        {
            result = await base.CreateAsync(user).ConfigureAwait(false);
        }
        catch
        {
            await _userDirectoryWriter.DeleteAsync(user.Id).ConfigureAwait(false);
            throw;
        }

        if (!result.Succeeded)
        {
            await _userDirectoryWriter.DeleteAsync(user.Id).ConfigureAwait(false);
        }

        return result;
    }

    private static string ResolveDisplayName(LocalIdentity user)
    {
        string composed = $"{user.FirstName} {user.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(composed))
        {
            return composed;
        }

        return !string.IsNullOrWhiteSpace(user.Email)
            ? user.Email
            : user.UserName ?? user.Id.ToString();
    }

    /// <inheritdoc/>
    public override async Task<IdentityResult> AccessFailedAsync(LocalIdentity user)
    {
        ArgumentNullException.ThrowIfNull(user);

        IdentityResult result = await base.AccessFailedAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return result;
        }

        // base.AccessFailedAsync sets LockoutEnd when AccessFailedCount reaches max.
        // Detect whether a lockout was just triggered by checking LockoutEnd.
        DateTimeOffset now = _clock.Now;
        DateTimeOffset? lockoutEnd = await GetLockoutEndDateAsync(user).ConfigureAwait(false);

        if (lockoutEnd is null || lockoutEnd <= now)
        {
            return result;
        }

        // A lockout was triggered — apply exponential backoff
        user.ConsecutiveLockouts++;

        TimeSpan duration = ComputeLockoutDuration(user.ConsecutiveLockouts);
        await SetLockoutEndDateAsync(user, now + duration).ConfigureAwait(false);
        await UpdateAsync(user).ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc/>
    public override async Task<IdentityResult> ResetAccessFailedCountAsync(LocalIdentity user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.ConsecutiveLockouts > 0)
        {
            user.ConsecutiveLockouts = 0;
            await UpdateAsync(user).ConfigureAwait(false);
        }

        return await base.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
    }

    internal TimeSpan ComputeLockoutDuration(int consecutiveLockouts)
    {
        if (consecutiveLockouts <= 0)
        {
            return _lockoutOptions.BaseLockoutDuration;
        }

        double multiplier = Math.Pow(_lockoutOptions.ExponentialBase, consecutiveLockouts - 1);
        double totalSeconds = _lockoutOptions.BaseLockoutDuration.TotalSeconds * multiplier;

        // Guard against overflow
        if (totalSeconds > _lockoutOptions.MaxLockoutDuration.TotalSeconds)
        {
            return _lockoutOptions.MaxLockoutDuration;
        }

        return TimeSpan.FromSeconds(totalSeconds);
    }
}
