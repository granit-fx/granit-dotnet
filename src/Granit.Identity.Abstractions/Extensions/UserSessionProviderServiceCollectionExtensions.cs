using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Identity.Extensions;

/// <summary>
/// Registers a backend's <see cref="IUserSessionProvider"/> (and, when it implements it,
/// <see cref="IUserDeviceProvider"/>) as the active provider for the canonical session API, with
/// <strong>explicit, order-independent precedence</strong>.
/// </summary>
/// <remarks>
/// Backends call these helpers instead of <c>IServiceCollection.Replace</c> directly so that, when several
/// are present on one host (e.g. OpenIddict + BFF), the winner of each facet is decided by
/// <see cref="UserSessionProviderPrecedence"/> — not by the accident of module topological order or the
/// order of imperative <c>AddGranit*</c> calls. A registration wins a facet only when its precedence is
/// greater than or equal to the precedence already recorded for that facet, so the highest-precedence
/// backend always wins regardless of the call order. The no-op defaults from
/// <c>GranitIdentityAbstractionsModule</c> sit at the implicit <see cref="UserSessionProviderPrecedence.None"/>
/// floor and are replaced by any real backend.
/// </remarks>
public static class UserSessionProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TProvider"/> (resolved as itself) as the session provider, and as the
    /// device provider too when it implements <see cref="IUserDeviceProvider"/>, at <paramref name="precedence"/>.
    /// </summary>
    public static IServiceCollection SetUserSessionProvider<TProvider>(
        this IServiceCollection services,
        UserSessionProviderPrecedence precedence)
        where TProvider : class, IUserSessionProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<TProvider>();

        Func<IServiceProvider, IUserDeviceProvider>? deviceFactory =
            typeof(IUserDeviceProvider).IsAssignableFrom(typeof(TProvider))
                ? sp => (IUserDeviceProvider)sp.GetRequiredService<TProvider>()
                : null;

        return services.SetUserSessionProvider(
            precedence,
            sessionFactory: sp => sp.GetRequiredService<TProvider>(),
            deviceFactory: deviceFactory);
    }

    /// <summary>
    /// Registers the session provider (and optionally the device provider) via factories, at
    /// <paramref name="precedence"/>. Use this overload when both facets forward to a shared instance
    /// (e.g. the federated <c>IIdentityProvider</c>) rather than a dedicated provider type.
    /// </summary>
    public static IServiceCollection SetUserSessionProvider(
        this IServiceCollection services,
        UserSessionProviderPrecedence precedence,
        Func<IServiceProvider, IUserSessionProvider> sessionFactory,
        Func<IServiceProvider, IUserDeviceProvider>? deviceFactory = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(sessionFactory);

        PrecedenceState state = GetOrAddState(services);

        if (precedence >= state.Session)
        {
            state.Session = precedence;
            services.Replace(ServiceDescriptor.Scoped(sessionFactory));
        }

        if (deviceFactory is not null && precedence >= state.Device)
        {
            state.Device = precedence;
            services.Replace(ServiceDescriptor.Scoped(deviceFactory));
        }

        return services;
    }

    /// <summary>
    /// Returns the precedence of the backend currently winning each facet (<see cref="UserSessionProviderPrecedence.None"/>
    /// when only the no-op defaults are registered). Useful for startup diagnostics and tests.
    /// </summary>
    public static (UserSessionProviderPrecedence Session, UserSessionProviderPrecedence Device)
        GetUserSessionProviderPrecedence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.FirstOrDefault(d => d.ServiceType == typeof(PrecedenceState))?.ImplementationInstance
            is PrecedenceState state
            ? (state.Session, state.Device)
            : (UserSessionProviderPrecedence.None, UserSessionProviderPrecedence.None);
    }

    private static PrecedenceState GetOrAddState(IServiceCollection services)
    {
        if (services.FirstOrDefault(d => d.ServiceType == typeof(PrecedenceState))?.ImplementationInstance
            is PrecedenceState existing)
        {
            return existing;
        }

        PrecedenceState state = new();
        services.AddSingleton(state);
        return state;
    }

    // Tracks the winning precedence per facet across registration calls. Stored in the collection so the
    // guard is the single source of truth and order-independent; harmless in the built container (a private
    // type nothing resolves).
    private sealed class PrecedenceState
    {
        public UserSessionProviderPrecedence Session { get; set; } = UserSessionProviderPrecedence.None;

        public UserSessionProviderPrecedence Device { get; set; } = UserSessionProviderPrecedence.None;
    }
}
