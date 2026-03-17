using Bogus;
using Granit.Security;
using Granit.Testing.Fakes;

namespace Granit.Testing.Generators;

/// <summary>
/// Static factory for Bogus <see cref="Faker{T}"/> instances pre-configured
/// for Granit fake types.
/// </summary>
public static class GranitFaker
{
    /// <summary>
    /// Creates a <see cref="Faker{T}"/> for <see cref="FakeCurrentUser"/>
    /// with realistic user profile data.
    /// </summary>
    public static Faker<FakeCurrentUser> CurrentUser() => new Faker<FakeCurrentUser>()
        .RuleFor(u => u.UserId, f => f.Random.Guid().ToString())
        .RuleFor(u => u.UserName, f => f.Internet.UserName())
        .RuleFor(u => u.Email, f => f.Internet.Email())
        .RuleFor(u => u.FirstName, f => f.Name.FirstName())
        .RuleFor(u => u.LastName, f => f.Name.LastName())
        .RuleFor(u => u.IsAuthenticated, _ => true)
        .RuleFor(u => u.ActorKind, _ => ActorKind.User)
        .RuleFor(u => u.IsMachine, _ => false)
        .RuleFor(u => u.ApiKeyId, _ => null);

    /// <summary>
    /// Creates a <see cref="Faker{T}"/> for <see cref="FakeCurrentTenant"/>
    /// with realistic tenant data.
    /// </summary>
    public static Faker<FakeCurrentTenant> Tenant() => new Faker<FakeCurrentTenant>()
        .RuleFor(t => t.Id, f => f.Random.Guid())
        .RuleFor(t => t.Name, f => f.Company.CompanyName())
        .RuleFor(t => t.IsAvailable, _ => true);
}
