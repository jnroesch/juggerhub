using Microsoft.AspNetCore.Identity;

namespace JuggerHub.Entities;

/// <summary>
/// The platform member / identity-foundation entity.
/// </summary>
/// <remarks>
/// Derives from <see cref="IdentityUser{TKey}"/> with a <see cref="Guid"/> key
/// (UUIDv7, aligned with <see cref="BaseEntity"/>). This scaffold adds no custom
/// profile fields — display name, roles, and membership semantics arrive with
/// later features. Sign-up / sign-in / password-reset flows are intentionally
/// deferred; only the identity foundation and its persistence exist here.
/// </remarks>
public class User : IdentityUser<Guid>
{
}
