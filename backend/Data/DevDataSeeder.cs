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

        await SeedTeamsAsync(db, ct);
    }

    /// <summary>
    /// Development-only demo teams so the team space (roster, activity, news) is demonstrable:
    /// a city team "Rheinfeuer" and a Mixteam "Chaos Crew". Enrolls the earliest profiles (first
    /// as admin), attributes their participations to Rheinfeuer so Activity renders, and adds a
    /// couple of news posts. Idempotent — only populates a team that has no members yet.
    /// </summary>
    private static async Task SeedTeamsAsync(AppDbContext db, CancellationToken ct)
    {
        var rheinfeuer = await db.Teams.FirstOrDefaultAsync(t => t.Slug == "rheinfeuer", ct);
        if (rheinfeuer is null)
        {
            rheinfeuer = new Team { Slug = "rheinfeuer", Name = "Rheinfeuer", Type = TeamType.CityTeam, City = "Berlin" };
            db.Teams.Add(rheinfeuer);
        }

        var chaos = await db.Teams.FirstOrDefaultAsync(t => t.Slug == "chaos-crew", ct);
        if (chaos is null)
        {
            chaos = new Team { Slug = "chaos-crew", Name = "Chaos Crew", Type = TeamType.Mixteam, City = null };
            db.Teams.Add(chaos);
        }

        await db.SaveChangesAsync(ct);

        if (await db.TeamMemberships.AnyAsync(m => m.TeamId == rheinfeuer.Id, ct))
        {
            return; // already populated
        }

        var users = await db.PlayerProfiles.AsNoTracking()
            .OrderBy(p => p.CreatedDate)
            .Select(p => new { p.Id, p.UserId })
            .Take(4)
            .ToListAsync(ct);
        if (users.Count == 0)
        {
            return; // no players registered yet — try again next startup
        }

        var now = DateTime.UtcNow;
        for (var i = 0; i < users.Count; i++)
        {
            db.TeamMemberships.Add(new TeamMembership
            {
                TeamId = rheinfeuer.Id,
                UserId = users[i].UserId,
                Role = i == 0 ? TeamRole.Admin : TeamRole.Member,
                JoinedDate = now,
            });
        }

        // The first player also admins the Mixteam (demonstrates multiple membership).
        db.TeamMemberships.Add(new TeamMembership
        {
            TeamId = chaos.Id,
            UserId = users[0].UserId,
            Role = TeamRole.Admin,
            JoinedDate = now,
        });

        db.TeamNewsPosts.Add(new TeamNewsPost
        {
            TeamId = rheinfeuer.Id,
            AuthorUserId = users[0].UserId,
            Body = "New jerseys are in — grab yours at Saturday training.",
        });
        db.TeamNewsPosts.Add(new TeamNewsPost
        {
            TeamId = rheinfeuer.Id,
            AuthorUserId = users[0].UserId,
            Body = "Carpool to Leipzig for the tournament — add your name to the sheet.",
        });

        await db.SaveChangesAsync(ct);

        // Attribute the members' existing participations to Rheinfeuer so team Activity renders.
        var profileIds = users.Select(u => u.Id).ToList();
        await db.EventParticipations
            .Where(ep => profileIds.Contains(ep.ProfileId) && ep.TeamId == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(ep => ep.TeamId, rheinfeuer.Id)
                .SetProperty(ep => ep.TeamLabel, "Rheinfeuer")
                .SetProperty(ep => ep.ModifiedDate, now), ct);
    }
}
