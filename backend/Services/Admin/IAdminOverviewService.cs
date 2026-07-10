using JuggerHub.Dtos.Admin;

namespace JuggerHub.Services.Admin;

/// <summary>The admin landing aggregate (feature 013 US2).</summary>
public interface IAdminOverviewService
{
    Task<AdminOverviewDto> GetOverviewAsync(CancellationToken ct = default);
}
