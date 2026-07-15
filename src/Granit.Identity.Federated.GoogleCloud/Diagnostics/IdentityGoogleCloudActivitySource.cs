using System.Diagnostics;

namespace Granit.Identity.Federated.GoogleCloud.Diagnostics;

/// <summary>OpenTelemetry activity source for the Google Cloud Identity Platform provider.</summary>
internal static class IdentityGoogleCloudActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Identity.Federated.GoogleCloud";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string ListUsers = "firebase.list-users";
        public const string GetUser = "firebase.get-user";
        public const string CreateUser = "firebase.create-user";
        public const string UpdateUser = "firebase.update-user";
        public const string EnableUser = "firebase.enable-user";
        public const string DisableUser = "firebase.disable-user";
        public const string SetCustomClaims = "firebase.set-custom-claims";

#pragma warning disable GRSEC003 // Operation name constants, not secrets
        public const string RevokeTokens = "firebase.revoke-tokens";
        public const string SetPassword = "firebase.set-password";
#pragma warning restore GRSEC003
    }

    /// <summary>Tag names.</summary>
    internal static class Tags
    {
        public const string UserId = "firebase.user_id";
        public const string ProjectId = "firebase.project_id";
    }
}
