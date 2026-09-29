# Research: The Party Page Leads Into the Party Chat (063)

Every item here was settled by reading the code, not by assumption. File:line references are to
`main` at `8cde4c9`.

## R1 — One copy of the load-bearing order

**Finding.** 060's `OpenTeamChatAsync` (`ChatConversationService.cs:1040-1052`) is three steps whose
order is the security design (060 research R2): the inbox's own creation step, then the lookup, then
`ChatGuard`. The party version needs exactly the same three steps with `ConversationKind.Party`.

**Decision.** Extract the body into
`private async Task<Guid> OpenAutoChatAsync(Guid callerId, ConversationKind kind, Guid ownerId, CancellationToken ct)`,
returning `Guid.Empty` for "not found". `OpenTeamChatAsync` and the new `OpenPartyChatAsync` each wrap
it in their own DTO. The remark explaining the order moves onto the helper.

**Why.** The order is what stops a caller from creating a chat for a team or party they are not in,
and what keeps `ChatGuard` the single membership rule. With two copies, a later edit to one could
reintroduce `EnsureForXAsync(requestedId)` there without anything noticing. 060's test suite
(`ChatTeamChatLinkTests`, 12 facts) guards the extraction.

**Alternatives rejected.**
- A second copy of the eight lines: the drift risk above, for no saving.
- One public generic method (`OpenAutoChatAsync(kind, …)`) on the interface with a `kind` route
  parameter: it would let a caller pass `kind` values that make no sense here (Direct, Group,
  inquiries), and it changes 060's public contract for no reader's benefit.

## R2 — No defect to fix first (unlike 060)

060 had to fix a defect before its button could be correct: 027's `TeamInquiry` threads share
`Conversation.TeamId`, and the service lookups were not scoped by kind.

**Checked for parties.**
- Only `ConversationKind.Party` ever sets `PartyId`: `EnsureAutoAsync` writes it for that kind only
  (`ChatConversationService.cs:1068-1072`); inquiry threads carry `TeamId` or `EventId`, never
  `PartyId`.
- The unique index is `IX_Conversations_PartyId` filtered on `"PartyId" IS NOT NULL`
  (`AppDbContext.cs:1129`), so at most one conversation of any kind per party.
- 060 already scoped both party lookups by kind "for symmetry": the existence check in
  `EnsureAutoChatsForAsync` (`:594-598`) and `FindAutoAsync` (`:1105-1110`).
- Archiving (disband) nulls `PartyId` (`:1226`), so a disbanded party's id finds nothing: the 404
  for a disbanded party falls out of the lookup, with no extra branch.

**Decision.** No backend change beyond the endpoint. A test passes the party's **team id** as a
party id (after the team chat exists) and expects 404, so the kind scoping stays pinned.

## R3 — Who is the crew, and does the page agree with the chat?

**The chat's rule** (`ChatGuard.cs:90-93`, `:199-202`): a `PartyMember` row for the caller with
`Status == In`. Marketplace guests (`ViaMarket`) are `In`, so they are members (`ChatGuard.cs:252`).

**The page's rule** (`party-manage.component.ts`): `isCrew() = myState === 'In' || myState === 'Admin'`,
where the server's `ResolveState` (`PartyService.cs:559-564`) maps role Admin → `Admin`, else status
In → `In`.

**Do they coincide?** Yes, for every viewer the page can render:
- A party admin is always `In`. The creator is inserted `In` + `Admin` (`PartyService.cs:117-121`); an
  accepted co-admin is set `In` + `Admin` (`PartyInvitationService.cs:362-371`); declining demotes to
  `Member` (`PartyRosterService.cs:183-184`); leaving deletes the row. So `Admin` never occurs
  without `In`.
- A guest's `myState` is `In`, and the guest can see the page (`PartyService.cs:261`:
  `IsTeamMember || IsCrew`).
- `NoResponse` / `Declined` are team members with no `In` row: not chat members, no button (owner
  decision).

**Decision.** Gate the button on the two existing branches: the crew card's `p.myState === 'In'`
branch (non-admin crew, guests included) and the `isAdmin()` readiness card. The server remains the
authority (FR-004); a stale page is handled by the 404 path (R5).

## R4 — Response shape

**Decision.** A new `PartyChatRefDto(Guid ConversationId)` beside `TeamChatRefDto`: non-null and
nothing else (FR-006).

**Alternatives rejected.**
- Reusing `TeamChatRefDto`: identical shape, wrong name in the OpenAPI document and the client
  model. Renaming it to something generic would churn 060's shipped contract for a one-line record.
- `ConversationSummaryDto` via `SummariseAsync`: runs the whole inbox with `Take = 100`, **excludes
  hidden chats** (a player who hid the party chat would get 404 on their own party) and carries an
  unread count, which the owner declined (060 research R4, unchanged).

## R5 — Frontend: resolve on press, never on load

Mirrors 060 research R5.

- `PartyManageComponent.openPartyChat()` calls `ChatService.openPartyChat(this.id)` when pressed, then
  `router.navigate(['/chat', id])`. The page's load is untouched (FR-008, SC-004).
- **Busy**: `partyChatBusy` disables the button and swaps its label to
  `parties.manage.openingPartyChat`.
- **404** → `partyChatNotice = 'parties.manage.partyChatNotCrew'` + `reload()`. The notice renders
  **beside the page's `error()` alert**, outside both cards. After the reload the viewer typically sees
  the request card (left or removed from the crew), so a notice inside the crew card would vanish. If
  the party is gone or the viewer left the team, the reload lands on the existing not-found state
  (*"This party may have been disbanded."*), which is itself the explanation.
- **Any other failure** → `partyChatError = 'parties.manage.partyChatFailed'`, shown inside the card,
  under the buttons, and the button is enabled again. It branches on the status, never the server's
  English `detail` (GH #179).
- **A `<button>`, not a link**: the id is not known until the press.

**Not copied**: the page's `fail()` renders `err.error?.detail` for its other actions (a GH #179
case). Out of scope here, and the new code does not route through it.

## R6 — Placement and size (owner decision + DESIGN.md)

**Crew card** (non-admin, `p.myState === 'In'`): today a sentence plus one `size="sm"` *Leave the
party* button. It becomes the sentence plus a `flex flex-wrap gap-sm` row: **Party chat**, then
**Leave the party**. Party chat comes first because it is the constructive action and Leave the
exit.

**Readiness card** (admin): the existing `mt-md flex flex-wrap gap-sm` row holds *Apply to event*
(primary, default size) or *Withdraw from event* (secondary, `sm`). **Party chat** is appended after
it, `variant="secondary"`. Apply stays the page's one coral CTA (FR-016).

**Size.** DESIGN.md: "Touch targets ≥ 44px — the default control height for buttons and inputs".
`jhButton`'s `sm` is `min-h-9` (36px); the default is `min-h-11` (44px) (`button.directive.ts:80-83`).
062's walk recorded the same finding. The new button uses the **default** size. Its row neighbours
*Leave the party* and *Withdraw from event* move from `sm` to the default too, so each row has one
height and every control in it meets the rule. That is a small visible change to two existing
buttons, made deliberately and recorded in the UI review, not a side effect. *Apply to event* is
already default.

**Wrapping at 375px (German).** The card's inner width is about 311px (375 minus the page's and the
card's 16px gutters on each side). The admin row *Für Event bewerben* + *Party-Chat* needs about 330px
at the default size, so it wraps to two lines. `flex-wrap` is already on that row and wrapping is
accepted. The crew row *Party-Chat* + *Party verlassen* is about 300px and may or may not wrap. The
walk's screenshots decide whether either needs `w-full`. Truncation is not an option (SC-006).

## R7 — Copy

Four keys under `parties.manage`, in all three catalogues in one commit (`catalog-parity.spec.ts`):

| Key | en | de | es |
|-----|----|----|----|
| `partyChat` | Party chat | Party-Chat | Chat de la party |
| `openingPartyChat` | Opening… | Wird geöffnet… | Abriendo… |
| `partyChatFailed` | We couldn't open the party chat just now. | Wir konnten den Party-Chat gerade nicht öffnen. | No hemos podido abrir el chat de la party ahora mismo. |
| `partyChatNotCrew` | You're no longer in this crew. | Du bist nicht mehr in dieser Crew. | Ya no estás en este equipo. |

"Party-Chat" matches the chat catalogue's `kindParty` (de). The Spanish "Chat de la party" follows
`leaveParty` ("Salir de la party"), and the not-in-crew line mirrors `youreInCrew` ("Estás en este
equipo."), which is how the Spanish catalogue renders *crew*. The failure line copies 060's
`teamChatFailed` shape. The opening label is a separate key rather than a reuse of
`teams.detail.openingTeamChat`, so the two pages' copy can change independently.
