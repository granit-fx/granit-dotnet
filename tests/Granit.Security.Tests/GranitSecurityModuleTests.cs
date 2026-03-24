// =============================================================================
// Tests - GranitSecurityModule
// =============================================================================
// GranitSecurityModule is an abstractions marker module.
// No services registered — the JWT implementation is in
// Granit.Authentication.JwtBearer (GranitJwtBearerModule).
// =============================================================================

using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Security.Tests;

public sealed class GranitSecurityModuleTests
{
    [Fact]
    public void GranitSecurityModule_IsGranitModule() => typeof(GranitSecurityModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();
}
