namespace Granit.Identity.Local.Options;

/// <summary>
/// Configuration options for account lockout with exponential backoff.
/// Bound to <c>Identity:Lockout</c> configuration section.
/// </summary>
/// <remarks>
/// <para>
/// When a user exceeds <see cref="MaxFailedAccessAttempts"/>, the account is locked
/// for <see cref="BaseLockoutDuration"/>. Each subsequent lockout doubles the duration
/// (controlled by <see cref="ExponentialBase"/>), capped at <see cref="MaxLockoutDuration"/>.
/// </para>
/// <para>
/// The exponential counter resets to zero on successful login, password reset, or
/// admin unlock — preventing permanent denial-of-service via brute-force lockout.
/// </para>
/// </remarks>
public sealed class GranitLockoutOptions
{
    /// <summary>Configuration section name for binding from <c>appsettings.json</c>.</summary>
    public const string SectionName = "Identity:Lockout";

    /// <summary>
    /// Gets or sets the maximum number of consecutive failed login attempts before lockout.
    /// </summary>
    public int MaxFailedAccessAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the base lockout duration applied on the first lockout.
    /// Subsequent lockouts multiply this by <see cref="ExponentialBase"/>^(n-1).
    /// </summary>
    public TimeSpan BaseLockoutDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum lockout duration (cap). Prevents indefinite lockout
    /// that would effectively become a denial-of-service vector.
    /// </summary>
    public TimeSpan MaxLockoutDuration { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Gets or sets the exponential base for backoff calculation.
    /// Duration = min(BaseLockoutDuration * ExponentialBase^(consecutiveLockouts - 1), MaxLockoutDuration).
    /// </summary>
    public double ExponentialBase { get; set; } = 2.0;
}
