using Granit.Users;

namespace Granit.Testing.Fakes;

/// <summary>
/// Configurable fake implementation of <see cref="ICurrentUserService"/> for tests.
/// </summary>
/// <remarks>
/// <para>
/// All state is stored in <see cref="AsyncLocal{T}"/> so that each async context
/// (i.e. each xUnit test) gets isolated values — safe for parallel execution
/// and <c>IClassFixture&lt;T&gt;</c> sharing.
/// </para>
/// <para>
/// Sensible defaults are provided: authenticated user with ID <c>"test-user-001"</c>,
/// name <c>"Test User"</c>, email <c>"test@example.com"</c>.
/// Override any property to match your test scenario.
/// </para>
/// </remarks>
public sealed class FakeCurrentUser : ICurrentUserService
{
    private readonly AsyncLocal<UserState?> _state = new();

    /// <inheritdoc/>
    public string? UserId
    {
        get => GetState().UserId;
        set => EnsureState().UserId = value;
    }

    /// <inheritdoc/>
    public string? UserName
    {
        get => GetState().UserName;
        set => EnsureState().UserName = value;
    }

    /// <inheritdoc/>
    public string? Email
    {
        get => GetState().Email;
        set => EnsureState().Email = value;
    }

    /// <inheritdoc/>
    public string? FirstName
    {
        get => GetState().FirstName;
        set => EnsureState().FirstName = value;
    }

    /// <inheritdoc/>
    public string? LastName
    {
        get => GetState().LastName;
        set => EnsureState().LastName = value;
    }

    /// <inheritdoc/>
    public bool IsAuthenticated
    {
        get => GetState().IsAuthenticated;
        set => EnsureState().IsAuthenticated = value;
    }

    /// <inheritdoc/>
    public ActorKind ActorKind
    {
        get => GetState().ActorKind;
        set => EnsureState().ActorKind = value;
    }

    /// <inheritdoc/>
    public bool IsMachine
    {
        get => ActorKind is not ActorKind.User;
        set => ActorKind = value ? ActorKind.ExternalSystem : ActorKind.User;
    }

    /// <inheritdoc/>
    public Guid? ApiKeyId
    {
        get => GetState().ApiKeyId;
        set => EnsureState().ApiKeyId = value;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetRoles() => GetState().Roles;

    /// <inheritdoc/>
    public bool IsInRole(string role) =>
        GetState().Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    /// <summary>Adds a role to the current user.</summary>
    public void AddRole(string role) => EnsureState().Roles.Add(role);

    /// <summary>Removes all roles from the current user.</summary>
    public void ClearRoles() => EnsureState().Roles.Clear();

    private UserState GetState() => _state.Value ?? UserState.Default;

    private UserState EnsureState() => _state.Value ??= new UserState();

    private sealed class UserState
    {
        internal static readonly UserState Default = new();

        public string? UserId { get; set; } = "test-user-001";
        public string? UserName { get; set; } = "Test User";
        public string? Email { get; set; } = "test@example.com";
        public string? FirstName { get; set; } = "Test";
        public string? LastName { get; set; } = "User";
        public bool IsAuthenticated { get; set; } = true;
        public ActorKind ActorKind { get; set; } = ActorKind.User;
        public Guid? ApiKeyId { get; set; }
        public List<string> Roles { get; set; } = [];
    }
}
