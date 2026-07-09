using System.ComponentModel.DataAnnotations;

namespace JuggerHub.Dtos.Recognition;

/// <summary>Optional reason recorded when an admin revokes a badge or achievement award.</summary>
public sealed record RevokeAwardRequest([MaxLength(280)] string? Reason);
