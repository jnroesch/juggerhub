using JuggerHub.Data;
using JuggerHub.Dtos.Cities;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Geocoding;

/// <inheritdoc />
public sealed class CityService : ICityService
{
    // Mean Earth radius (km) — WGS84 authalic radius, good to well under the city-granularity
    // precision "near you" needs.
    private const double EarthRadiusKm = 6371.0088;

    private readonly AppDbContext _db;
    private readonly IGeocodingClient _geocoder;

    public CityService(AppDbContext db, IGeocodingClient geocoder)
    {
        _db = db;
        _geocoder = geocoder;
    }

    public async Task<IReadOnlyList<CityOptionDto>> SearchAsync(
        string query, int limit, CancellationToken ct = default)
    {
        var results = await _geocoder.SearchAsync(query, limit, ct);
        return results.Select(LocationLabels.ToOption).ToList();
    }

    public async Task<City> ResolveAndUpsertAsync(
        string externalId, string? nameHint, CancellationToken ct = default)
    {
        // 1) Reuse a city we already hold — no geocoder call, no re-import (FR-022).
        var existing = await _db.Cities.FirstOrDefaultAsync(c => c.ExternalId == externalId, ct);
        if (existing is not null)
        {
            return existing;
        }

        // 2) First use: re-resolve server-side so the stored record is authoritative (Principle I).
        var geocoded = await _geocoder.ResolveAsync(externalId, nameHint ?? string.Empty, ct)
            ?? throw new CityNotResolvableException(externalId);

        var city = new City
        {
            ExternalId = geocoded.ExternalId,
            Name = geocoded.Name,
            CountryName = geocoded.CountryName,
            CountryCode = geocoded.CountryCode,
            Region = geocoded.Region,
            Latitude = geocoded.Latitude,
            Longitude = geocoded.Longitude,
        };

        _db.Cities.Add(city);
        AddDistanceRows(city, await LoadOtherCityPointsAsync(ct));

        // A single SaveChangesAsync is atomic and already runs through the provider's execution
        // strategy (EnableRetryOnFailure) — no manual transaction is opened, so the multi-step
        // execution-strategy dance (constitution VII) is not required here. The client-generated
        // UUIDv7 key makes a commit-time replay collide on the known id rather than duplicate.
        try
        {
            await _db.SaveChangesAsync(ct);
            return city;
        }
        catch (DbUpdateException)
        {
            // Lost the create race: another request inserted this ExternalId first (unique index).
            // Discard our losing insert (city + its distance rows) and return the winner.
            _db.ChangeTracker.Clear();
            var winner = await _db.Cities.FirstOrDefaultAsync(c => c.ExternalId == externalId, ct);
            if (winner is not null)
            {
                return winner;
            }

            throw;
        }
    }

    private async Task<List<CityPoint>> LoadOtherCityPointsAsync(CancellationToken ct) =>
        await _db.Cities
            .AsNoTracking()
            .Select(c => new CityPoint(c.Id, c.Latitude, c.Longitude))
            .ToListAsync(ct);

    private void AddDistanceRows(City city, IReadOnlyList<CityPoint> others)
    {
        // Self-row: own-city entities rank nearest (distance 0). Required for the proximity join to
        // surface them (data-model.md).
        _db.CityDistances.Add(new CityDistance { FromCityId = city.Id, ToCityId = city.Id, DistanceKm = 0 });

        foreach (var other in others)
        {
            var km = HaversineKm(city.Latitude, city.Longitude, other.Latitude, other.Longitude);
            // Stored both ways so the proximity query is a single-sided join from any home city.
            _db.CityDistances.Add(new CityDistance { FromCityId = city.Id, ToCityId = other.Id, DistanceKm = km });
            _db.CityDistances.Add(new CityDistance { FromCityId = other.Id, ToCityId = city.Id, DistanceKm = km });
        }
    }

    internal static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private readonly record struct CityPoint(Guid Id, double Latitude, double Longitude);
}
