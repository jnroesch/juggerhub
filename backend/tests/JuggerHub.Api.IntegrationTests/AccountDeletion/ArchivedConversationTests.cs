using System.Net;
using System.Net.Http.Json;
using JuggerHub.Common;
using JuggerHub.Entities;
using JuggerHub.Services.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.AccountDeletion;

/// <summary>
/// Feature 037 T019 — an archived conversation snapshot must not become a second home for an erased
/// member's identity (spec FR-028).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a regression guard, not a fix.</b> The plan originally flagged the frozen
/// <c>Conversation.Name</c> as the single most likely place a name would survive erasure, because no
/// cascade or query filter can reach a string column. Reading the archival code showed that is not
/// what it stores: the freeze resolves to a <em>team</em> or <em>event</em> name, or a literal like
/// "Party chat" — never a person. There are exactly two writers of that column in the codebase, and
/// the other is group-chat creation, where the name is typed by a member.
/// </para>
/// <para>
/// So these tests pin the behaviour rather than repair it. If someone later makes the freeze fall
/// back to a participant's display name — a very natural-looking change for a direct conversation —
/// this is what catches it.
/// </para>
/// </remarks>
[Collection("AccountDeletion")]
public sealed class ArchivedConversationTests : AccountDeletionTestSupport
{
    public ArchivedConversationTests(JuggerHubApiFactory factory) : base(factory) { }

    private async Task ArchiveForTeamAsync(Guid teamId)
    {
        using var scope = Factory.Services.CreateScope();
        var conversations = scope.ServiceProvider.GetRequiredService<IChatConversationService>();
        await conversations.ArchiveForTeamAsync(teamId);
    }

    [Fact]
    public async Task An_archived_team_chat_freezes_the_team_name_not_a_member_name()
    {
        var (leaver, leaverId, _, _) = await NewMemberAsync();
        var (_, keeperId, _, _) = await NewMemberAsync();

        const string displayName = "Ada Kowalczyk";
        const string teamName = "Rheinfeuer";

        (await leaver.PutAsJsonAsync("/api/v1/profiles/me", new { displayName })).EnsureSuccessStatusCode();

        var teamId = await CreateTeamWithSoleAdminAsync(leaverId, teamName);
        await AddTeamAdminAsync(teamId, keeperId);

        var conversationId = await WithDbAsync(async db =>
        {
            var conversation = new Conversation { Kind = ConversationKind.Team, TeamId = teamId };
            db.Conversations.Add(conversation);
            db.ChatMessages.Add(new ChatMessage
            {
                Conversation = conversation,
                SenderId = leaverId,
                Body = "Pitch is booked for Saturday.",
            });
            await db.SaveChangesAsync();
            return conversation.Id;
        });

        // Archiving snapshots the roster and freezes the name — this is the moment the plan worried about.
        await ArchiveForTeamAsync(teamId);
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(leaver)).StatusCode);

        var frozen = await WithDbAsync(db => db.Conversations.AsNoTracking()
            .Where(c => c.Id == conversationId)
            .Select(c => new { c.Name, c.State })
            .SingleAsync());

        Assert.Equal(ConversationState.Archived, frozen.State);
        Assert.Equal(teamName, frozen.Name);

        // The assertion that matters: no trace of the person anywhere in the frozen label.
        Assert.DoesNotContain("Ada", frozen.Name!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Kowalczyk", frozen.Name!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_snapshot_keeps_the_history_readable_under_the_placeholder()
    {
        var (leaver, leaverId, _, _) = await NewMemberAsync();
        var (keeper, keeperId, _, _) = await NewMemberAsync();

        (await leaver.PutAsJsonAsync("/api/v1/profiles/me", new { displayName = "Ada Kowalczyk" }))
            .EnsureSuccessStatusCode();

        var teamId = await CreateTeamWithSoleAdminAsync(leaverId);
        await AddTeamAdminAsync(teamId, keeperId);

        var conversationId = await WithDbAsync(async db =>
        {
            var conversation = new Conversation { Kind = ConversationKind.Team, TeamId = teamId };
            db.Conversations.Add(conversation);
            db.ChatMessages.Add(new ChatMessage
            {
                Conversation = conversation,
                SenderId = leaverId,
                Body = "Pitch is booked for Saturday.",
            });
            await db.SaveChangesAsync();
            return conversation.Id;
        });

        await ArchiveForTeamAsync(teamId);
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(leaver)).StatusCode);

        // The other member can still read it — that is the whole reason archival snapshots rather
        // than flags — and the departed sender reads as the neutral placeholder.
        var page = await keeper.GetAsync($"/api/v1/chat/conversations/{conversationId}/messages");
        page.EnsureSuccessStatusCode();

        var body = await page.Content.ReadAsStringAsync();
        Assert.Contains("Pitch is booked for Saturday.", body);
        Assert.Contains(MemberPlaceholder.English, body);
        Assert.DoesNotContain("Ada Kowalczyk", body);
    }

    [Fact]
    public async Task Frozen_participant_rows_do_not_carry_a_name()
    {
        var (leaver, leaverId, _, _) = await NewMemberAsync();
        var (_, keeperId, _, _) = await NewMemberAsync();

        (await leaver.PutAsJsonAsync("/api/v1/profiles/me", new { displayName = "Ada Kowalczyk" }))
            .EnsureSuccessStatusCode();

        var teamId = await CreateTeamWithSoleAdminAsync(leaverId);
        await AddTeamAdminAsync(teamId, keeperId);

        await WithDbAsync(async db =>
        {
            db.Conversations.Add(new Conversation { Kind = ConversationKind.Team, TeamId = teamId });
            await db.SaveChangesAsync();
        });

        await ArchiveForTeamAsync(teamId);
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(leaver)).StatusCode);

        // The snapshot materialised real participant rows. Erasure removes the departed member's row
        // outright, and the rows that remain hold only ids — a name would have to come from the
        // profile, which is gone.
        Assert.False(await WithDbAsync(db => db.ConversationParticipants
            .AnyAsync(p => p.UserId == leaverId)));
    }
}
