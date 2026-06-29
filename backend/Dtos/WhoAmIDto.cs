namespace JuggerHub.Dtos;

/// <summary>
/// Transient read model for the protected sample endpoint. Unauthenticated
/// callers never see this body — they receive 401 with a <c>ProblemDetails</c>.
/// </summary>
/// <param name="UserId">The authenticated principal's id.</param>
/// <param name="Authenticated">Always true when reached (endpoint is [Authorize]).</param>
public sealed record WhoAmIDto(Guid UserId, bool Authenticated);
