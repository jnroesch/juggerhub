namespace JuggerHub.Services.Recognition;

/// <summary>Result of an attempt to grant a badge/achievement; maps to an HTTP status in the controller.</summary>
public enum GrantOutcome
{
    Granted,
    DefinitionNotFound,
    DefinitionRetired,
    SubjectNotFound,
    SubjectTypeMismatch,
    Duplicate,
}

/// <summary>Result of a revoke attempt.</summary>
public enum RevokeOutcome
{
    Revoked,
    NotFound,
}

/// <summary>Result of an icon upload.</summary>
public enum IconOutcome
{
    Stored,
    Empty,
    TooLarge,
    InvalidType,
    DefinitionNotFound,
}
