# Contract: existing endpoints the wizard now consumes (053)

None of these change. Listed so the wizard's use of them is reviewable against what each already
promises.

| Endpoint | Auth | Used for | Outcomes the wizard maps |
|---|---|---|---|
| `GET /api/v1/invitations/{token}` | anonymous | preview the carried invite | `200 { teamName, teamSlug, type, location, memberCount, inviterDisplayName, state: Usable\|Expired\|Invalid }` → `usable`/`expired`/`invalid`; `404` → `invalid` |
| `GET /api/v1/profiles/me/invitations` | JWT | addressed (targeted) invitations | `200 PagedResult<MyInvitationDto>` (token included; caller-scoped); any error → empty list |
| `GET /api/v1/profiles/me/teams` | JWT | already-a-member check (via `MembershipService.load()`) | `200 PagedResult<MyTeamDto>`; error → "no teams" (service behaviour) |
| `POST /api/v1/invitations/{token}/accept` | JWT | Accept | `200 { teamSlug }` for Joined **and** AlreadyMember; `409` not usable; `404` not found → plain failure line, retry by press, **never auto-retried** |
| `POST /api/v1/invitations/{token}/decline` | JWT | Decline an addressed invite | `204`; `404` → row removed either way (023 precedent) |
| `GET /api/v1/teams` (browse) | JWT | the unchanged 029 search | unchanged |
| `POST /api/v1/teams/{slug}/join-requests` | JWT | the unchanged 029 ask-to-join | unchanged |

Frontend service methods: `TeamService.getInvitePreview / acceptInvite / declineInvite`,
`InvitationService.listMine`, `MembershipService.load / teams`. No new service method is needed.
