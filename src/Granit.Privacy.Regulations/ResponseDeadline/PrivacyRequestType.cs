namespace Granit.Privacy.Regulations.ResponseDeadline;

/// <summary>
/// Types of privacy requests subject to regulatory response deadlines.
/// </summary>
public enum PrivacyRequestType
{
    /// <summary>Data subject access request (GDPR Art. 15, LGPD Art. 18, CCPA).</summary>
    SubjectAccessRequest,

    /// <summary>Data deletion request (GDPR Art. 17, LGPD Art. 18, CCPA).</summary>
    DeletionRequest,

    /// <summary>Data rectification request (GDPR Art. 16).</summary>
    RectificationRequest,

    /// <summary>Data portability request (GDPR Art. 20).</summary>
    DataPortability,

    /// <summary>Processing restriction request (GDPR Art. 18).</summary>
    ProcessingRestriction,

    /// <summary>Opt-out of sale/sharing (CCPA).</summary>
    OptOut,
}
