using System.Security.Cryptography;
using System.Text;
using JuggerHub.Entities;
using Konscious.Security.Cryptography;
using Microsoft.AspNetCore.Identity;

namespace JuggerHub.Services.Security;

/// <summary>
/// Argon2id implementation of <see cref="IPasswordHasher{TUser}"/> (constitution
/// Principle IV — passwords hashed with argon2 + a per-password salt). Registered
/// in DI to override Identity's default PBKDF2 hasher.
/// </summary>
/// <remarks>
/// Argon2id is the OWASP-recommended variant (resists GPU and side-channel
/// attacks). Each hash embeds its parameters and a random 16-byte salt, so the
/// cost can be raised later without breaking existing hashes. No password is
/// actually hashed in the walking skeleton (no register/login), but the hasher is
/// wired and unit-testable so the later auth feature inherits it.
/// Encoded format: <c>argon2id$v=1$m=&lt;kib&gt;,t=&lt;iters&gt;,p=&lt;par&gt;$base64(salt)$base64(hash)</c>.
/// </remarks>
public sealed class Argon2PasswordHasher : IPasswordHasher<User>
{
    private const int SaltSize = 16;
    private const int HashSize = 32;

    // OWASP-aligned defaults; embedded per-hash so they can evolve.
    private const int MemoryKib = 19_456; // 19 MiB
    private const int Iterations = 2;
    private const int DegreeOfParallelism = 1;

    public string HashPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(password, salt, MemoryKib, Iterations, DegreeOfParallelism);

        return string.Join(
            '$',
            "argon2id",
            "v=1",
            $"m={MemoryKib},t={Iterations},p={DegreeOfParallelism}",
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
    {
        ArgumentNullException.ThrowIfNull(hashedPassword);
        ArgumentNullException.ThrowIfNull(providedPassword);

        if (!TryParse(hashedPassword, out var parameters))
        {
            return PasswordVerificationResult.Failed;
        }

        var actual = ComputeHash(
            providedPassword,
            parameters.Salt,
            parameters.MemoryKib,
            parameters.Iterations,
            parameters.Parallelism);

        if (!CryptographicOperations.FixedTimeEquals(actual, parameters.Hash))
        {
            return PasswordVerificationResult.Failed;
        }

        // Signal a rehash if the stored parameters are weaker than current defaults.
        var needsRehash = parameters.MemoryKib < MemoryKib
            || parameters.Iterations < Iterations
            || parameters.Parallelism < DegreeOfParallelism;

        return needsRehash
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }

    private static byte[] ComputeHash(string password, byte[] salt, int memoryKib, int iterations, int parallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKib,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };
        return argon2.GetBytes(HashSize);
    }

    private static bool TryParse(string encoded, out Argon2Parameters parameters)
    {
        parameters = default;

        var parts = encoded.Split('$');
        if (parts.Length != 5 || parts[0] != "argon2id")
        {
            return false;
        }

        var costs = parts[2].Split(',');
        if (costs.Length != 3)
        {
            return false;
        }

        try
        {
            var memoryKib = int.Parse(costs[0].AsSpan(costs[0].IndexOf('=') + 1));
            var iterations = int.Parse(costs[1].AsSpan(costs[1].IndexOf('=') + 1));
            var parallelism = int.Parse(costs[2].AsSpan(costs[2].IndexOf('=') + 1));
            var salt = Convert.FromBase64String(parts[3]);
            var hash = Convert.FromBase64String(parts[4]);

            parameters = new Argon2Parameters(salt, hash, memoryKib, iterations, parallelism);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            return false;
        }
    }

    private readonly record struct Argon2Parameters(
        byte[] Salt,
        byte[] Hash,
        int MemoryKib,
        int Iterations,
        int Parallelism);
}
