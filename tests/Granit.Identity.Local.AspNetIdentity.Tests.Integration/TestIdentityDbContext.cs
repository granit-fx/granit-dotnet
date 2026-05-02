using Granit.Identity.Local.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// Minimal ASP.NET Core Identity DbContext used by the orchestrator integration tests.
/// Stores <c>LocalIdentity</c> / <c>GranitRole</c> via the stock Identity schema.
/// Mirrors production deployments that keep Identity tables in a dedicated DbContext
/// (in Granit's OIDC flow that role is played by the internal <c>OpenIddictDbContext</c>).
/// </summary>
internal sealed class TestIdentityDbContext(DbContextOptions<TestIdentityDbContext> options)
    : IdentityDbContext<LocalIdentity, GranitRole, Guid>(options);
