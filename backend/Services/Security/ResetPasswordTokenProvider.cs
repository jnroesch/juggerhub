using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Security;

/// <summary>
/// Options giving the password-reset token its own (short) lifespan. The default
/// data-protection token lifespan is 1 day (fine for email confirmation); a reset
/// link should be shorter, so it gets a dedicated provider. See research.md §4.
/// </summary>
public sealed class ResetPasswordTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public ResetPasswordTokenProviderOptions()
    {
        Name = "ResetPasswordDataProtectorTokenProvider";
        TokenLifespan = TimeSpan.FromHours(1);
    }
}

/// <summary>
/// Data-protection token provider for password resets, bound to the shorter
/// <see cref="ResetPasswordTokenProviderOptions"/> lifespan and wired via
/// <c>IdentityOptions.Tokens.PasswordResetTokenProvider</c>.
/// </summary>
public sealed class ResetPasswordTokenProvider<TUser> : DataProtectorTokenProvider<TUser>
    where TUser : class
{
    public ResetPasswordTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<ResetPasswordTokenProviderOptions> options,
        ILogger<DataProtectorTokenProvider<TUser>> logger)
        : base(dataProtectionProvider, options, logger)
    {
    }
}
