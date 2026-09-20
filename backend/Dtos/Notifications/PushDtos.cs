using System.ComponentModel.DataAnnotations;

namespace JuggerHub.Dtos.Notifications;

/// <summary>
/// The application server key a browser needs before it can subscribe (feature 055). Not a secret:
/// every subscribing browser receives it. It is served rather than built into the frontend bundle
/// because it differs per environment, and one build per environment is what feature 033
/// established as the thing to avoid.
/// </summary>
public sealed record PushPublicKeyDto(string PublicKey);

/// <summary>
/// Registers the caller's current browser for push (feature 055). The owner is always taken from
/// the caller's token — there is deliberately no user field here, so one account cannot subscribe
/// on another's behalf.
/// </summary>
public sealed record RegisterPushSubscriptionRequest(
    [Required][Url][StringLength(512, MinimumLength = 1)] string Endpoint,
    [Required][StringLength(128, MinimumLength = 1)] string P256dh,
    [Required][StringLength(64, MinimumLength = 1)] string Auth,
    [StringLength(64)] string? DeviceLabel);

/// <summary>
/// Removes the caller's registration for one browser (feature 055). The endpoint is in the body
/// because the server cannot tell which of a member's devices is asking.
/// </summary>
public sealed record RemovePushSubscriptionRequest(
    [Required][Url][StringLength(512, MinimumLength = 1)] string Endpoint);
