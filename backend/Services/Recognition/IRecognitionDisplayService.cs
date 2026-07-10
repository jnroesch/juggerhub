using JuggerHub.Dtos.Recognition;

namespace JuggerHub.Services.Recognition;

/// <summary>
/// Reads a subject's earned (active) badges and achievements for public display on profile and
/// team pages (feature 012 US2). Exposes only the public field set — no admin note or granter.
/// </summary>
public interface IRecognitionDisplayService
{
    Task<SubjectRecognitionsDto> ForPlayerAsync(Guid playerProfileId, CancellationToken ct = default);

    Task<SubjectRecognitionsDto> ForTeamAsync(Guid teamId, CancellationToken ct = default);
}
