using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Granit.Persistence.EntityFrameworkCore.Interceptors;

/// <summary>
/// Marker interface for interceptors that should be automatically registered on all
/// Granit DbContexts via <see cref="Extensions.DbContextOptionsBuilderExtensions.UseGranitInterceptors"/>.
/// </summary>
/// <remarks>
/// <para>
/// Use this interface when a module provides an interceptor that must run on every
/// DbContext (e.g. audit change tracking, cross-cutting concerns). The interceptor
/// will be resolved from the service provider and added after the 6 standard
/// Granit.Persistence interceptors.
/// </para>
/// <para>
/// <b>Registration:</b> Register the interceptor as both its concrete type and
/// <see cref="IGranitAutoInterceptor"/> in the DI container:
/// <code>
/// services.AddScoped&lt;MyInterceptor&gt;();
/// services.AddScoped&lt;IGranitAutoInterceptor&gt;(sp =&gt; sp.GetRequiredService&lt;MyInterceptor&gt;());
/// </code>
/// </para>
/// <para>
/// <b>Ordering:</b> Auto-interceptors run after all standard Granit interceptors
/// (AuditedEntity, Versioning, ConcurrencyStamp, DomainEvents, EntityLifecycle, SoftDelete).
/// If ordering between auto-interceptors matters, implement <see cref="IGranitAutoInterceptor"/>
/// and register them in the desired order.
/// </para>
/// </remarks>
public interface IGranitAutoInterceptor : IInterceptor;
