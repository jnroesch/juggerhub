using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using JuggerHub.Api.IntegrationTests.Parties;

namespace JuggerHub.Api.IntegrationTests.Email;

/// <summary>
/// The five emails GH #379 found in English for every recipient — the team invite, the event and
/// party co-admin invites, the role change and team news — now follow the recipient's saved language,
/// like every email built since 039. One test per sender, through the real API, asserting the subject:
/// the subject was the part composed inline in English, so it is what a missed call site would show.
/// </summary>
[Collection("Parties")]
public sealed class RecipientLanguageEmailTests : PartyTestSupport
{
    private const string TeamName = "Rheinfeuer";
    private const string EventName = "Tempelhof Summer Slam";

    public RecipientLanguageEmailTests(JuggerHubApiFactory factory) : base(factory) { }

    [Fact]
    public async Task The_team_invite_is_in_the_invited_players_language()
    {
        var (admin, _, adminHandle, _) = await NewUserAsync();
        var (target, targetId, _, targetEmail) = await NewUserAsync();
        await SetLanguageAsync(target, "de");
        var (_, slug) = await CreateTeamAsync(admin);

        Factory.EmailSender.Clear();
        var invite = await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = targetId });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);

        var mail = Assert.Single(Factory.EmailSender.Sent, e => IsTo(e, targetEmail));
        Assert.Equal($"Du bist zu {TeamName} eingeladen — JuggerHub", mail.Subject);
        Assert.Contains("Einladung annehmen", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains($"{adminHandle} findet", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains($"/join/{slug}/", mail.HtmlBody, StringComparison.Ordinal);
        AssertGermanExpiry(mail.HtmlBody);
    }

    [Fact]
    public async Task The_event_co_admin_invite_is_in_the_invited_players_language()
    {
        var (organiser, _, _, _) = await NewUserAsync();
        var (target, targetId, _, targetEmail) = await NewUserAsync();
        await SetLanguageAsync(target, "es");
        var eventId = await CreateTeamsEventAsync(organiser);

        Factory.EmailSender.Clear();
        var invite = await organiser.PostAsJsonAsync($"/api/v1/events/{eventId}/invitations", new { userId = targetId });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);

        var mail = Assert.Single(Factory.EmailSender.Sent, e => IsTo(e, targetEmail));
        Assert.Equal($"Te han invitado a coadministrar {EventName} — JuggerHub", mail.Subject);
        Assert.Contains("Aceptar invitación", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("/event-invite/", mail.HtmlBody, StringComparison.Ordinal);
        // The event invite used to share the team invite's body and pitch "training times".
        Assert.DoesNotContain("entrenamiento", mail.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_party_co_admin_invite_is_in_the_invited_members_language()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (member, memberId, _, memberEmail) = await NewUserAsync();
        await SetLanguageAsync(member, "de");
        var (teamId, _) = await CreateTeamAsync(admin);
        await AddTeamMemberAsync(teamId, memberId);
        var partyId = await FormPartyAsync(admin, await CreateTeamsEventAsync(admin), teamId);

        Factory.EmailSender.Clear();
        var invite = await admin.PostAsJsonAsync($"/api/v1/parties/{partyId}/invitations", new { userId = memberId });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);

        var mail = Assert.Single(Factory.EmailSender.Sent, e => IsTo(e, memberEmail));
        Assert.Equal($"Du bist eingeladen, die Party von {TeamName} bei {EventName} mitzuführen — JuggerHub", mail.Subject);
        Assert.Contains("Party-Co-Admin", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("/party-invite/", mail.HtmlBody, StringComparison.Ordinal);
        AssertGermanExpiry(mail.HtmlBody);
    }

    [Fact]
    public async Task The_role_change_email_is_in_the_members_language_with_the_role_in_words()
    {
        var (admin, _, adminHandle, _) = await NewUserAsync();
        var (member, memberId, _, memberEmail) = await NewUserAsync();
        await SetLanguageAsync(member, "de");
        var (teamId, slug) = await CreateTeamAsync(admin);
        await AddTeamMemberAsync(teamId, memberId);

        Factory.EmailSender.Clear();
        var promote = await admin.PatchAsJsonAsync($"/api/v1/teams/{slug}/members/{memberId}/role", new { role = "Admin" });
        promote.EnsureSuccessStatusCode();

        var mail = Assert.Single(Factory.EmailSender.Sent, e => IsTo(e, memberEmail));
        Assert.Equal($"Deine Rolle in {TeamName} hat sich geändert — JuggerHub", mail.Subject);
        Assert.Contains($"{adminHandle} hat deine Rolle geändert. Du bist jetzt Admin von {TeamName}.", mail.HtmlBody, StringComparison.Ordinal);
        // The server used to add an English phrase of its own ("an admin").
        Assert.DoesNotContain("an admin", mail.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Team_news_reaches_each_member_in_their_own_language()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (spanish, spanishId, _, spanishEmail) = await NewUserAsync();
        var (english, englishId, _, englishEmail) = await NewUserAsync();
        await SetLanguageAsync(spanish, "es");
        await SetLanguageAsync(english, "en");
        var (teamId, slug) = await CreateTeamAsync(admin);
        await AddTeamMemberAsync(teamId, spanishId);
        await AddTeamMemberAsync(teamId, englishId);

        Factory.EmailSender.Clear();
        var post = await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/news", new { body = "Training moves to Thursday." });
        post.EnsureSuccessStatusCode();

        var es = Assert.Single(Factory.EmailSender.Sent, e => IsTo(e, spanishEmail));
        Assert.Equal($"Noticias de {TeamName} — JuggerHub", es.Subject);
        Assert.Contains("Leer en JuggerHub", es.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Training moves to Thursday.", es.HtmlBody, StringComparison.Ordinal);

        // English is unchanged: the same subject the sender used to build inline.
        var en = Assert.Single(Factory.EmailSender.Sent, e => IsTo(e, englishEmail));
        Assert.Equal($"News from {TeamName} — JuggerHub", en.Subject);
        Assert.Contains("Read on JuggerHub", en.HtmlBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// The expiry reads as a German date. The app runs globalization-invariant, so the old
    /// <c>MMMM dd, yyyy</c> pattern printed an English month name inside the German sentence.
    /// </summary>
    private static void AssertGermanExpiry(string html)
    {
        Assert.Matches(new Regex(@"läuft am \d{2}\.\d{2}\.\d{4} um \d{2}:\d{2} UTC ab"), html);
    }

    private static bool IsTo(CapturedEmail email, string address) =>
        string.Equals(email.To, address, StringComparison.OrdinalIgnoreCase);

    private static async Task SetLanguageAsync(HttpClient client, string language) =>
        (await client.PutAsJsonAsync("/api/v1/account/language", new { language })).EnsureSuccessStatusCode();
}
