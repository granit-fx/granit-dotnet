using System.Reflection;
using Granit.Authentication.ApiKeys.Cache;
using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests.Cache;

/// <summary>
/// Behavioural tests proving the fix for VULN-100-IAM: dispatching an
/// <see cref="ApiKeyRevokedEto"/> (or <see cref="ApiKeyScopesUpdatedEto"/>) evicts the
/// cached entry keyed by the exact <c>HashedKey</c> carried on the event, so a revoked or
/// re-scoped key can no longer authenticate from the stale cache.
/// </summary>
public sealed class ApiKeyCacheInvalidationHandlerTests
{
    private const string HashedKey = "sha256:9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    [Fact]
    public async Task HandleAsync_Revoked_RemovesCacheEntryWithExactHashedKey()
    {
        var cache = new CapturingApiKeyCacheService();
        var evt = new ApiKeyRevokedEto(Guid.NewGuid(), HashedKey);

        await ApiKeyCacheInvalidationHandler.HandleAsync(
            evt, cache, NullLogger<ApiKeyCacheInvalidationHandler>.Instance, TestContext.Current.CancellationToken);

        cache.RemoveCalls.ShouldHaveSingleItem().ShouldBe(HashedKey);
    }

    [Fact]
    public async Task HandleAsync_ScopesUpdated_RemovesCacheEntryWithExactHashedKey()
    {
        var cache = new CapturingApiKeyCacheService();
        var evt = new ApiKeyScopesUpdatedEto(Guid.NewGuid(), HashedKey);

        await ApiKeyCacheInvalidationHandler.HandleAsync(
            evt, cache, NullLogger<ApiKeyCacheInvalidationHandler>.Instance, TestContext.Current.CancellationToken);

        cache.RemoveCalls.ShouldHaveSingleItem().ShouldBe(HashedKey);
    }

    [Fact]
    public async Task HandleAsync_Revoked_PropagatesCancellationToken()
    {
        var cache = new CapturingApiKeyCacheService();
        using var cts = new CancellationTokenSource();
        var evt = new ApiKeyRevokedEto(Guid.NewGuid(), HashedKey);

        await ApiKeyCacheInvalidationHandler.HandleAsync(
            evt, cache, NullLogger<ApiKeyCacheInvalidationHandler>.Instance, cts.Token);

        cache.LastToken.ShouldBe(cts.Token);
    }

    // =========================================================================
    // Wolverine discoverability guard — teeth beyond declaration.
    //
    // Wolverine discovers handlers by convention (class ends in "Handler", public,
    // non-static, with a public static "Handle"/"HandleAsync" whose FIRST parameter is
    // the message). If a future refactor renames the method, makes it internal, or turns
    // the class static, Wolverine SILENTLY stops dispatching and the vulnerability
    // reopens with no failing test. These reflection checks fail loudly instead.
    // =========================================================================

    [Theory]
    [InlineData(typeof(ApiKeyRevokedEto))]
    [InlineData(typeof(ApiKeyScopesUpdatedEto))]
    public void Handler_ExposesWolverineDiscoverableHandleMethodFor(Type messageType)
    {
        MethodInfo? handle = typeof(ApiKeyCacheInvalidationHandler)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(m =>
                m.Name == "HandleAsync" &&
                m.GetParameters() is { Length: > 0 } p &&
                p[0].ParameterType == messageType);

        handle.ShouldNotBeNull($"Wolverine cannot dispatch {messageType.Name} without a public static HandleAsync whose first parameter is that message.");
        handle.ReturnType.ShouldBe(typeof(Task));
    }

    [Fact]
    public void Handler_IsPublicNonStaticClass_SoWolverineCanScanIt()
    {
        Type t = typeof(ApiKeyCacheInvalidationHandler);
        t.IsPublic.ShouldBeTrue("Wolverine scans Assembly.ExportedTypes.");
        t.IsAbstract.ShouldBeFalse("A static class (abstract+sealed) is skipped by Wolverine.");
    }

    private sealed class CapturingApiKeyCacheService : IApiKeyCacheService
    {
        public List<string> RemoveCalls { get; } = [];
        public CancellationToken LastToken { get; private set; }

        public Task<ApiKeyEntry?> GetOrLoadAsync(
            string hashedKey,
            Func<CancellationToken, Task<ApiKeyEntry?>> factory,
            TimeSpan duration,
            CancellationToken cancellationToken = default) =>
            factory(cancellationToken);

        public Task RemoveAsync(string hashedKey, CancellationToken cancellationToken = default)
        {
            RemoveCalls.Add(hashedKey);
            LastToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
