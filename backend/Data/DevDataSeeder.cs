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
            .Select(s => new Event
            {
                Name = s.Name,
                Description = s.Name,
                // Historical activity samples: a single-day in-person event (feature 006 replaced Date with StartsAt/EndsAt).
                StartsAt = s.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                EndsAt = s.Date.ToDateTime(new TimeOnly(18, 0), DateTimeKind.Utc),
                Type = EventType.Tournament,
                LocationKind = LocationKind.InPerson,
                Location = s.Location,
                City = s.Location,
                Country = "Deutschland",
                ParticipantMode = ParticipantMode.Teams,
                ParticipationLimit = 16,
            })
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
        await SeedEventsAsync(db, ct);
        await SeedRecognitionsAsync(db, ct);
    }

    /// <summary>
    /// Development-only fixed catalogue of badges &amp; achievements (feature 012) plus a couple of
    /// sample grants, so the profile/team display and the admin grant picker have content locally.
    /// Idempotent by definition name; grants only to subjects that don't already hold them.
    /// </summary>
    private static async Task SeedRecognitionsAsync(AppDbContext db, CancellationToken ct)
    {
        var badgeDefs = new (string Name, string Description, bool Players, bool Teams)[]
        {
            ("Beta tester", "Was here in the early days.", true, true),
            ("Fair play", "Recognized for great sportsmanship.", true, false),
            ("Founding club", "One of the first teams on JuggerHub.", false, true),
            ("Trainer", "Runs training for their team.", true, false),
        };
        var achievementDefs = new (string Name, string Description, bool Players, bool Teams)[]
        {
            ("Champion", "Won a championship.", true, true),
            ("50 trainings", "Turned up to 50 training sessions.", true, false),
        };

        foreach (var (name, desc, players, teams) in badgeDefs)
        {
            if (!await db.BadgeDefinitions.AnyAsync(d => d.Name == name, ct))
            {
                db.BadgeDefinitions.Add(new BadgeDefinition { Name = name, Description = desc, AppliesToPlayers = players, AppliesToTeams = teams });
            }
        }
        foreach (var (name, desc, players, teams) in achievementDefs)
        {
            if (!await db.AchievementDefinitions.AnyAsync(d => d.Name == name, ct))
            {
                db.AchievementDefinitions.Add(new AchievementDefinition { Name = name, Description = desc, AppliesToPlayers = players, AppliesToTeams = teams });
            }
        }
        await db.SaveChangesAsync(ct);

        var firstProfile = await db.PlayerProfiles.AsNoTracking()
            .OrderBy(p => p.CreatedDate)
            .Select(p => new { p.Id, p.UserId })
            .FirstOrDefaultAsync(ct);
        if (firstProfile is null)
        {
            return; // no players yet — grants seed on a later startup
        }

        var now = DateTime.UtcNow;

        // Grant the earliest player a badge + achievement (idempotent).
        await GrantBadgeIfMissing(db, "Fair play", firstProfile.Id, null, firstProfile.UserId, now, "Great sportsmanship all season.", ct);
        await GrantAchievementIfMissing(db, "50 trainings", firstProfile.Id, null, firstProfile.UserId, now, ct);

        // Grant Rheinfeuer a team badge.
        var rheinfeuerId = await db.Teams.AsNoTracking().Where(t => t.Slug == "rheinfeuer").Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        if (rheinfeuerId is Guid teamId)
        {
            await GrantBadgeIfMissing(db, "Founding club", null, teamId, firstProfile.UserId, now, null, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task GrantBadgeIfMissing(
        AppDbContext db, string defName, Guid? playerProfileId, Guid? teamId, Guid grantedBy, DateTime now, string? note, CancellationToken ct)
    {
        var defId = await db.BadgeDefinitions.Where(d => d.Name == defName).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
        if (defId is not Guid id)
        {
            return;
        }
        var exists = await db.BadgeAwards.AnyAsync(a =>
            a.BadgeDefinitionId == id && a.Status == AwardStatus.Active && a.PlayerProfileId == playerProfileId && a.TeamId == teamId, ct);
        if (!exists)
        {
            db.BadgeAwards.Add(new BadgeAward
            {
                BadgeDefinitionId = id, PlayerProfileId = playerProfileId, TeamId = teamId,
                Source = AwardSource.Manual, Status = AwardStatus.Active, EarnedAt = now, GrantedByUserId = grantedBy, Note = note,
            });
        }
    }

    private static async Task GrantAchievementIfMissing(
        AppDbContext db, string defName, Guid? playerProfileId, Guid? teamId, Guid grantedBy, DateTime now, CancellationToken ct)
    {
        var defId = await db.AchievementDefinitions.Where(d => d.Name == defName).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
        if (defId is not Guid id)
        {
            return;
        }
        var exists = await db.AchievementAwards.AnyAsync(a =>
            a.AchievementDefinitionId == id && a.Status == AwardStatus.Active && a.PlayerProfileId == playerProfileId && a.TeamId == teamId, ct);
        if (!exists)
        {
            db.AchievementAwards.Add(new AchievementAward
            {
                AchievementDefinitionId = id, PlayerProfileId = playerProfileId, TeamId = teamId,
                Source = AwardSource.Manual, Status = AwardStatus.Active, EarnedAt = now, GrantedByUserId = grantedBy,
            });
        }
    }

    /// <summary>
    /// Development-only demo <em>live</em> events (feature 006) so the events feature is
    /// demonstrable: an in-person paid teams-only tournament (with team sign-ups + a contact +
    /// news), a virtual free individuals-only workshop (with a few joined players), and a
    /// cancelled example. The earliest player admins them. Idempotent by a marker name.
    /// </summary>
    private static async Task SeedEventsAsync(AppDbContext db, CancellationToken ct)
    {
        const string marker = "Berlin Summer Cup 2026";
        if (await db.Events.AnyAsync(e => e.Name == marker, ct))
        {
            return; // already seeded
        }

        var firstUser = await db.PlayerProfiles.AsNoTracking()
            .OrderBy(p => p.CreatedDate)
            .Select(p => p.UserId)
            .FirstOrDefaultAsync(ct);
        if (firstUser == default)
        {
            return; // no players registered yet — try again next startup
        }

        var otherUsers = await db.PlayerProfiles.AsNoTracking()
            .OrderBy(p => p.CreatedDate)
            .Skip(1)
            .Take(3)
            .Select(p => p.UserId)
            .ToListAsync(ct);

        var rheinfeuer = await db.Teams.AsNoTracking().Where(t => t.Slug == "rheinfeuer").Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        var chaos = await db.Teams.AsNoTracking().Where(t => t.Slug == "chaos-crew").Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);

        var now = DateTime.UtcNow;

        // 1. In-person paid teams-only tournament (upcoming, small limit so the waitlist shows).
        var cup = new Event
        {
            Name = marker,
            Type = EventType.Tournament,
            Description = "Two days of open Jugger on the old airfield. All divisions welcome — bring your crew.",
            StartsAt = new DateTime(2026, 9, 5, 9, 0, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 9, 6, 18, 0, 0, DateTimeKind.Utc),
            LocationKind = LocationKind.InPerson,
            VenueName = "Altes Flugfeld",
            Street = "Hauptstrasse 1",
            PostalCode = "10115",
            City = "Berlin",
            Country = "Deutschland",
            Location = "Berlin, Deutschland",
            ParticipantMode = ParticipantMode.Teams,
            ParticipationLimit = 8,
            IsPaid = true,
            FeeAmount = 40m,
            FeeCurrency = "EUR",
            FeeRecipientName = "JSC Berlin e.V.",
            FeeIban = "DE89370400440532013000",
            FeePaymentDeadline = new DateOnly(2026, 8, 20),
        };
        db.Events.Add(cup);
        db.EventAdmins.Add(new EventAdmin { EventId = cup.Id, UserId = firstUser, AddedDate = now });
        if (chaos is Guid chaosId)
        {
            db.EventSignups.Add(new EventSignup { EventId = cup.Id, TeamId = chaosId, Status = SignupStatus.Joined });
        }
        if (rheinfeuer is Guid rid)
        {
            db.EventSignups.Add(new EventSignup { EventId = cup.Id, TeamId = rid, Status = SignupStatus.AwaitingApproval });
        }
        db.EventContacts.Add(new EventContact { EventId = cup.Id, Name = "Ada K.", Role = "Location host", Email = "ada@example.org" });
        db.EventContacts.Add(new EventContact { EventId = cup.Id, Name = "Ben M.", Role = "Caterer", Phone = "+49 30 123456" });
        db.EventNewsPosts.Add(new EventNewsPost { EventId = cup.Id, AuthorUserId = firstUser, Body = "First whistle 10:00 sharp — arrive early to warm up." });

        // 2. Virtual free individuals-only workshop (a few players already joined).
        var clinic = new Event
        {
            Name = "Pompfen Skills Clinic",
            Type = EventType.Workshop,
            Description = "Online technique session for runners and chains. Camera optional, questions encouraged.",
            StartsAt = new DateTime(2026, 7, 20, 18, 0, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 7, 20, 20, 0, 0, DateTimeKind.Utc),
            LocationKind = LocationKind.Virtual,
            VirtualLink = "https://zoom.us/j/1234567890",
            Location = "Online",
            ParticipantMode = ParticipantMode.Individuals,
            ParticipationLimit = 30,
            IsPaid = false,
        };
        db.Events.Add(clinic);
        db.EventAdmins.Add(new EventAdmin { EventId = clinic.Id, UserId = firstUser, AddedDate = now });
        // The earliest player joins personally so Home "Up next" (feature 008) has an individuals-mode
        // item with a live RSVP/withdraw toggle alongside their teams' read-only "team is going" entries.
        db.EventSignups.Add(new EventSignup { EventId = clinic.Id, UserId = firstUser, Status = SignupStatus.Joined });
        foreach (var uid in otherUsers)
        {
            db.EventSignups.Add(new EventSignup { EventId = clinic.Id, UserId = uid, Status = SignupStatus.Joined });
        }

        // 3. Cancelled example (stays viewable, marked cancelled).
        var cancelled = new Event
        {
            Name = "Winter Indoor Meetup",
            Type = EventType.Other,
            CustomTypeLabel = "Indoor meetup",
            Description = "Casual indoor session — cancelled because the hall fell through.",
            StartsAt = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 8, 1, 16, 0, 0, DateTimeKind.Utc),
            LocationKind = LocationKind.InPerson,
            VenueName = "Sporthalle Nord",
            Street = "Turnweg 3",
            PostalCode = "20095",
            City = "Hamburg",
            Country = "Deutschland",
            Location = "Hamburg, Deutschland",
            ParticipantMode = ParticipantMode.Individuals,
            ParticipationLimit = 20,
            IsPaid = false,
            Status = EventStatus.Cancelled,
            CancelledDate = now,
        };
        db.Events.Add(cancelled);
        db.EventAdmins.Add(new EventAdmin { EventId = cancelled.Id, UserId = firstUser, AddedDate = now });

        await db.SaveChangesAsync(ct);
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
            // Beginners-welcome + (below) event participations make Rheinfeuer an "active",
            // beginners-friendly team so browse filters (feature 007) have data to show.
            rheinfeuer = new Team { Slug = "rheinfeuer", Name = "Rheinfeuer", Type = TeamType.CityTeam, City = "Köln", BeginnersWelcome = true };
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

        // Opt these seeded players into player search (feature 007) so the players browse
        // page has data; players not in this set stay hidden (opt-in default off).
        var userIds = users.Select(u => u.UserId).ToList();
        await db.PlayerProfiles
            .Where(p => userIds.Contains(p.UserId) && !p.AppearInSearch)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.AppearInSearch, true)
                .SetProperty(p => p.ModifiedDate, now), ct);
    }
}
