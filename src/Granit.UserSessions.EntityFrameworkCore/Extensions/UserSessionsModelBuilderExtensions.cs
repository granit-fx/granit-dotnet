using Granit.UserSessions.EntityFrameworkCore.Internal.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.UserSessions.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including the Granit user-sessions entity configurations in a
/// host-owned <see cref="DbContext"/> (so host migrations include the risk table).
/// </summary>
public static class UserSessionsModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the user-sessions module.</summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureUserSessionsModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserSessionRiskEntityConfiguration());
        return modelBuilder;
    }
}
