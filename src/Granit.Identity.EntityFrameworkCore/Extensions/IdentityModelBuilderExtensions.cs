using Granit.Identity.EntityFrameworkCore.EntityConfigurations;
using Granit.Identity.EntityFrameworkCore.Internal.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including the Granit Identity
/// entity configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class IdentityModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Identity module: the
    /// <see cref="Granit.Identity.Domain.User"/> aggregate (<see cref="UserConfiguration"/>)
    /// and the durable session-risk verdict store (<see cref="UserSessionRiskEntityConfiguration"/>).
    /// Apps that fold the identity model into a host-owned <see cref="DbContext"/>
    /// get the full table set — including <c>user_sessions_risks</c> — from this one call.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureGranitIdentityModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new UserSessionRiskEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DeviceTrustEntityConfiguration());
        modelBuilder.ApplyConfiguration(new UserBehavioralProfileEntityConfiguration());
        return modelBuilder;
    }
}
