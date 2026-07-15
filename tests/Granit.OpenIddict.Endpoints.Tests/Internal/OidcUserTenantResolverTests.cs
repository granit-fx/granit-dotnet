using Granit.DataFiltering;
using Granit.Domain;
using Granit.Identity.Local.Domain;
using Granit.OpenIddict.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Endpoints.Tests.Internal;

/// <summary>
/// <see cref="OidcUserTenantResolver"/> runs the user lookup with the multi-tenant filter disabled
/// — so a globally-unique subject/user name resolves regardless of the ambient tenant scope — and
/// restores the filter afterwards. Aligning the tenant scope to the user is the caller's job
/// (ICurrentTenant is AsyncLocal-backed), so it is not exercised here.
/// </summary>
public sealed class OidcUserTenantResolverTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task WithoutTenantFilterAsync_DisablesFilterDuringLookup_AndRestoresAfter()
    {
        RestorableDataFilter filter = new();
        bool? enabledDuringLookup = null;

        LocalIdentity? user = await OidcUserTenantResolver.WithoutTenantFilterAsync(
            filter, () =>
            {
                enabledDuringLookup = filter.IsEnabled<IMultiTenant>();
                return Task.FromResult<LocalIdentity?>(new LocalIdentity { TenantId = TenantA });
            });

        enabledDuringLookup.ShouldBe(false, "the multi-tenant filter must be off during the lookup");
        filter.IsEnabled<IMultiTenant>().ShouldBeTrue("the filter must be restored after the lookup");
        user.ShouldNotBeNull();
        user.TenantId.ShouldBe(TenantA);
    }

    [Fact]
    public async Task WithoutTenantFilterAsync_RestoresFilter_EvenWhenLookupThrows()
    {
        RestorableDataFilter filter = new();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            OidcUserTenantResolver.WithoutTenantFilterAsync(
                filter, () => throw new InvalidOperationException("boom")));

        filter.IsEnabled<IMultiTenant>().ShouldBeTrue("the filter must be restored after a failed lookup");
    }

    [Fact]
    public async Task WithoutTenantFilterAsync_UserNotFound_ReturnsNull()
    {
        RestorableDataFilter filter = new();

        LocalIdentity? user = await OidcUserTenantResolver.WithoutTenantFilterAsync(
            filter, () => Task.FromResult<LocalIdentity?>(null));

        user.ShouldBeNull();
    }

    [Fact]
    public async Task WithoutTenantFilterAsync_NullDataFilter_StillResolves()
    {
        LocalIdentity? user = await OidcUserTenantResolver.WithoutTenantFilterAsync(
            dataFilter: null, () => Task.FromResult<LocalIdentity?>(new LocalIdentity { TenantId = TenantA }));

        user.ShouldNotBeNull();
    }

    /// <summary>Minimal <see cref="IDataFilter"/> whose <see cref="Disable{T}"/> restores the prior state on dispose.</summary>
    private sealed class RestorableDataFilter : IDataFilter
    {
        private readonly Dictionary<Type, bool> _state = [];

        public bool IsEnabled<TFilter>() where TFilter : class =>
            !_state.TryGetValue(typeof(TFilter), out bool value) || value;

        public IDisposable Disable<TFilter>() where TFilter : class => SetScoped<TFilter>(false);

        public IDisposable Enable<TFilter>() where TFilter : class => SetScoped<TFilter>(true);

        private RestoreScope SetScoped<TFilter>(bool enabled) where TFilter : class
        {
            bool previous = IsEnabled<TFilter>();
            _state[typeof(TFilter)] = enabled;
            return new RestoreScope(() => _state[typeof(TFilter)] = previous);
        }

        private sealed class RestoreScope(Action restore) : IDisposable
        {
            public void Dispose() => restore();
        }
    }
}
