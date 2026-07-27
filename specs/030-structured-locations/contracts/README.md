# API Contracts: Structured Locations

**Feature**: 030-structured-locations

All endpoints are behind existing authentication (feature 026 — no anonymous access). Responses are DTOs built with explicit EF `.Select` projections (constitution Principle II, no object mapper); errors follow the global exception middleware (generic body, no stack traces). Lists use the shared `PaginationRequest` / `PagedResult<T>` envelope.

- **[cities.md](./cities.md)** — city type-ahead search (backend-proxied to Photon) and the city-selection payload used by profile/team/event updates.
- **[browse-and-profile.md](./browse-and-profile.md)** — the proximity/country additions to team & event browse queries, and the structured location shape on profile/team/event read + write DTOs.

The geocoder (Photon) is **internal only** — no browser calls it directly. There is no public contract for Photon; `IGeocodingClient` wraps it and is covered by the resilience pipeline (`Resilience:Outbound:Geocoding`).
