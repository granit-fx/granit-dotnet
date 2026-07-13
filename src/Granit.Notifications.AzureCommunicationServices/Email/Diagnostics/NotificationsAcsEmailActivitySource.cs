using System.Diagnostics;

namespace Granit.Notifications.AzureCommunicationServices.Email.Diagnostics;

/// <summary>OpenTelemetry activity source for the Azure Communication Services email provider.</summary>
internal static class NotificationsAcsEmailActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Notifications.AzureCommunicationServices.Email";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string SendEmail = "acs-email.send";
    }

    /// <summary>Tag names.</summary>
    internal static class Tags
    {
        public const string To = "acs.email.to";
        public const string SubjectLength = "acs.email.subject_length";
    }
}
