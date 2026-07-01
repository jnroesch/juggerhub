using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Data;

/// <summary>
/// Development-only convenience seeding for the profile feature: a few sample
/// Jugger events, attached as participations to any profile that has none, so the
/// "recent activity" section is demonstrable locally (quickstart Scenario D). Never
/// runs outside Development. This is NOT an event-management feature.
/// </summary>
public static class DevDataSeeder
{
    private static readonly (string Name, DateOnly Date, string Location, string Team)[] Samples =
    [
        ("Sommerturnier Berlin", new DateOnly(2025, 8, 16), "Berlin", "Team A"),
        ("Liga-Spieltag Hamburg", new DateOnly(2025, 6, 21), "Hamburg", "Team B"),
        ("Trainingscamp Köln", new DateOnly(2025, 5, 10), "Köln", "Team A"),
        ("Stadtmeisterschaft Leipzig", new DateOnly(2025, 4, 5), "Leipzig", "Team B"),
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        // Ensure the sample events exist (idempotent by name).
        var existingNames = await db.Events.Select(e => e.Name).ToListAsync(ct);
        var toAdd = Samples
            .Where(s => !existingNames.Contains(s.Name))
            .Select(s => new Event { Name = s.Name, Date = s.Date, Location = s.Location })
            .ToList();

        if (toAdd.Count > 0)
        {
            db.Events.AddRange(toAdd);
            await db.SaveChangesAsync(ct);
        }

        var events = await db.Events.AsNoTracking()
            .Where(e => Samples.Select(s => s.Name).Contains(e.Name))
            .ToListAsync(ct);
        var teamByName = Samples.ToDictionary(s => s.Name, s => s.Team);

        // Attach the samples to any profile that currently has no participations,
        // so freshly-registered dev profiles get demonstrable activity.
        var profilesWithout = await db.PlayerProfiles
            .Where(p => !p.Participations.Any())
            .Select(p => p.Id)
            .ToListAsync(ct);

        foreach (var profileId in profilesWithout)
        {
            foreach (var ev in events)
            {
                db.EventParticipations.Add(new EventParticipation
                {
                    ProfileId = profileId,
                    EventId = ev.Id,
                    TeamLabel = teamByName[ev.Name],
                });
            }
        }

        if (profilesWithout.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
