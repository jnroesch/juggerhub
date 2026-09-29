# Implementation Plan: Removing a Member Is Confirmed, and the People It Concerns Are Told

**Branch**: `064-member-removal-notices` | **Date**: 2026-09-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/064-member-removal-notices/spec.md`

## Summary

GH #385. Removing a teammate is one tap and silent; leaving is silent too, although the settings
promise "people joining or leaving". This feature (1) puts an in-page confirmation in front of
**Remove** on the team roster and the party crew, and moves the party page's **Disband** off the
browser's `confirm()` onto the same dialog; (2) tells the **removed player** (new
`TeamMemberRemoved`, names the team only, no actor); (3) tells **every other current admin** when a
member leaves or is removed (new `TeamMemberDeparted`, the departing player is the row's **actor**,
`removed: bool` in the payload); and (4) rate-limits **accepting team invitations** to 10 per player
per hour, so a shared-link join/leave loop cannot flood the admins. A party crew removal still tells
nobody (owner). No entity, no migration, no new endpoint.

The load-bearing points, all found by reading:

- **The notices go after the commit, outside the retried delegate**, in `MutateMembershipAsync`,
  which today returns before its own notification block on `remove`. The delegate's result carries
  the removed membership's `Id` out (research R1).
- **The departing player is the actor, never payload** (037 FR-023, the 058 rule). The removed
  player's notice has **no actor at all**, so nothing can identify the admin (R3).
- **Dedupe by the removed membership row's id**, never by player+team — the unique index is
  permanent, and a rejoin is a new row (R4).
- **Two of the five accept sites discard the invitation on any error**; a `429` must be branched
  before that, or the new limit costs players valid invitations (R9).
- **One `jh-confirm-dialog`** carries the focus/keyboard rules for all three new confirmations (R10).

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular (zoneless, signals) with Nx (frontend)

**Primary Dependencies**: EF Core + Npgsql, ASP.NET Core rate limiting (Redis fixed window), existing
`INotificationService` / `IPushFanOut` / `TeamEmailService` / `IEmailTemplateService`; Transloco

**Storage**: PostgreSQL 18 — no schema change (integer enum values appended)

**Testing**: xUnit integration tests (Testcontainers Postgres, fake email/push/realtime on the
factory); Jest (Angular TestBed); the catalogue parity guards; manual German walk (Playwright)

**Target Platform**: Linux containers on AKS; browsers incl. the installed PWA

**Project Type**: Web application (backend API + Angular SPA)

**Performance Goals**: One extra admin query and a bounded fan-out per departure (admins of one team)

**Constraints**: notices never fail or delay a departure; no person copied into a stored payload; our
own `429` never retried; every string ×3 languages

**Scale/Scope**: 2 notification types, 3 email templates ×3, ~8 push keys ×3, 1 rate-limit policy,
1 shared UI component, ~30 i18n keys ×3, 5 accept sites

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Status | Evidence |
|------|--------|----------|
| 1 Architecture | ✅ | Logic in `TeamService` (DI'd, interface'd); controller unchanged except one attribute; DTOs by projection |
| 2 Data access | ✅ | Admin recipients read by one `AsNoTracking` projection; no `ExecuteUpdate` added; no new entity |
| 3 Security | ✅ | No name in stored payloads (037 FR-023); removing admin never exposed; new rate limit on an open-reach loop; clients never render server `detail` |
| 4 Auth | ✅ | Unchanged; limit partitioned by authenticated user |
| 5 Conventions | ✅ | New component in separate `.html/.css/.ts`; no scripts |
| 6 Parity | ✅ | Redis-backed limiter everywhere but Development (existing rule); no config added |
| 7 UI/Design | ⚠ engaged | `checklists/ui-review.md` — binding case: the three dialogs in German at 375px; the Alerts rows |
| 8 Resilience | ✅ not engaged as an integration | No outbound call added; side effects after commit; `429` ours ⇒ never retried |

No violations; Complexity Tracking empty.

**Re-check after design**: unchanged. The shared dialog is justified by three uses carrying the same
accessibility rules (R10), not by speculation.

## Project Structure

### Documentation (this feature)

```text
specs/064-member-removal-notices/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── notifications.md
│   ├── team-members-api.md
│   └── ui.md
├── checklists/
│   ├── requirements.md
│   └── ui-review.md        # Gate 7, filled during implementation
└── tasks.md                # /speckit-tasks
```

### Source Code (repository root)

```text
backend/
├── Entities/NotificationEnums.cs                       # +TeamMemberRemoved=12, +TeamMemberDeparted=13, category arms
├── Dtos/Notifications/NotificationDtos.cs              # +2 payload records
├── Services/Teams/TeamService.cs                       # MutateMembershipAsync: carry membership id out; AnnounceDepartureAsync
├── Services/Email/TeamEmailService.cs                  # +3 send methods
├── Services/Email/EmailLocalizer.cs                    # subjects/titles/footers ×3
├── Services/EmailTemplateService/(I)EmailTemplateService.cs  # +3 generate methods
├── EmailTemplates/{en,de,es}/{removed-from-team,member-left,member-removed}.html
├── Services/Notifications/Push/PushContentComposer.cs  # +2 arms (title/body/url)
├── Services/Notifications/Push/PushLocalizer.cs        # keys ×3
├── Security/RateLimitPolicies.cs                       # +team-invite-accept (10/h)
├── Controllers/InvitationsController.cs                # [EnableRateLimiting] on Accept
└── tests/JuggerHub.Api.IntegrationTests/
    ├── Teams/TeamDepartureNoticeTests.cs               # new
    ├── Teams/InviteAcceptRateLimitTests.cs             # new
    ├── Teams/TeamRenameRewriteTests.cs                 # +2 kinds
    ├── Notifications/NotificationCategoryMappingTests.cs
    ├── Push/PushComposerTests.cs
    └── Email/TemplateRenderMatrixTests.cs

frontend/apps/web/
├── public/i18n/{en,de,es}.json
└── src/app/
    ├── shared/ui/confirm-dialog/confirm-dialog.component.{ts,html,css,spec.ts}   # new
    ├── shared/ui/index.ts, shared/ui/icon/icons.ts (+user-minus)
    ├── core/models/notification.models.ts              # +2 types, guards
    ├── features/alerts/notification-row/*              # +2 rows
    ├── features/alerts/alerts.component.ts             # 429 branch
    ├── features/teams/team-detail/*                    # Remove → dialog
    ├── features/parties/party-manage/*                 # Remove + Disband → dialog
    ├── features/my-team/my-team.component.*            # 429 branch, literal → key
    ├── features/onboarding/onboarding.component.ts     # 429 branch
    ├── features/teams/invite-accept/*                  # 429 branch
    └── features/dashboard/modules/needs-you-card.*     # 429 branch
```

**Structure Decision**: the existing web-application layout; no new project, library or folder
beyond `shared/ui/confirm-dialog/`.

## Phasing (small commits)

1. **Backend notices** — enum + payloads + category arms + composer arms + push keys; emails
   (templates, localizer, generators, senders); `MutateMembershipAsync` → `AnnounceDepartureAsync`;
   tests (`TeamDepartureNoticeTests`, mapping, composer guard, rename rewrite, template matrix).
2. **Backend limit** — policy + attribute + `InviteAcceptRateLimitTests`.
3. **Frontend dialog** — `jh-confirm-dialog` + spec + `user-minus` icon.
4. **Team page** — Remove → dialog, failure notes, keys ×3, spec.
5. **Party page** — Remove (In/Declined) + Disband → dialog, keys ×3, spec.
6. **Alerts rows** — models, row, keys ×3, spec.
7. **Accept sites** — five `429` branches, *My team* literal → key, specs.
8. **Verification** — full suites, lint, build, Gate 7 checklist, German walk at 375px + desktop.

## Complexity Tracking

None.
