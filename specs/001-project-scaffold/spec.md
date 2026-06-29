# Feature Specification: Project Scaffold (Walking Skeleton)

**Feature Branch**: `001-project-scaffold`

**Created**: 2026-06-29

**Status**: Draft

**Input**: User description: "Project scaffold — walking skeleton for the JuggerHub community platform (a community-driven web app to manage Jugger teams, tournaments, and general community features such as a forum; individual features are defined later). This feature scaffolds the general architecture only."

## Overview

JuggerHub is a community-driven platform for managing Jugger teams, tournaments, and general community features (e.g. a forum). Before any of those product features can be built, the project needs a proven architectural foundation: a thin but complete vertical slice connecting the browser, the API, and the database, with the security, configuration, and quality scaffolding that every later feature will rely on.

This feature delivers that foundation as a **walking skeleton** — the smallest end-to-end implementation that exercises every layer of the stack — plus the supporting structure (project layout, identity/auth plumbing, shared API primitives, test harnesses, containerized local environment) into which future features will be added. It deliberately builds **no end-user product feature** (no teams, tournaments, forum, or auth screens); those follow as their own specs.

## User Scenarios & Testing *(mandatory)*

The primary beneficiaries of this feature are the **development team** and **platform operators**. "Users" below refers to these roles; end-user product journeys arrive in later features.

### User Story 1 - Prove the stack end-to-end (Priority: P1)

A developer clones the repository, provides local configuration, and starts the whole system with a single command. The browser application loads, calls the backend, and the backend confirms it can reach the database — demonstrating that every layer (frontend → API → database) is correctly wired together.

**Why this priority**: This is the core purpose of a walking skeleton. Until a single request can travel from the browser through the API to the database and back, no feature can be built or trusted. It is the minimum viable proof that the architecture works.

**Independent Test**: Start the full stack locally, open the application, and confirm the dashboard displays a healthy status that reflects live database connectivity. Stopping the database causes the status to report unhealthy.

**Acceptance Scenarios**:

1. **Given** the full stack is started locally with one command, **When** a developer opens the application in a browser, **Then** the dashboard loads and displays an overall "healthy" status sourced from the backend.
2. **Given** the application is open, **When** the dashboard requests system health, **Then** the response indicates the database is reachable.
3. **Given** the database is unavailable, **When** the dashboard requests system health, **Then** the status is reported as unhealthy rather than the application crashing or showing a raw error.

---

### User Story 2 - Prove the security boundary (Priority: P1)

A developer confirms that protected parts of the API reject unauthenticated requests, demonstrating that the authentication and authorization pipeline is in place and enforced server-side — even though no login screen or sign-up flow exists yet.

**Why this priority**: The platform is security-first and "never trust the client" is non-negotiable. The auth boundary must exist and be enforced from the very first slice so that every later feature inherits a working, trusted security pipeline rather than retrofitting one.

**Independent Test**: Call the protected sample endpoint without credentials and confirm it is rejected; confirm the public health endpoint remains accessible. In the browser, navigating to a guarded area while unauthenticated redirects toward sign-in rather than exposing protected content.

**Acceptance Scenarios**:

1. **Given** a protected sample endpoint exists, **When** it is requested without valid credentials, **Then** the request is rejected as unauthorized.
2. **Given** a public health endpoint exists, **When** it is requested without credentials, **Then** it succeeds.
3. **Given** the browser application has a guarded area, **When** an unauthenticated user attempts to access it, **Then** they are routed toward sign-in instead of seeing protected content.
4. **Given** any backend error occurs, **When** the response reaches the client, **Then** it contains a generic, well-formed message with no stack trace, internal details, or secrets.

---

### User Story 3 - A foundation features can be added to (Priority: P2)

A developer beginning a new feature finds a consistent, documented structure already in place — a defined project layout, a shared base for data records, reusable list-paging and error-response conventions, the visual design system wired into the frontend, and an application shell with navigation — so they add their feature by following established patterns rather than inventing them.

**Why this priority**: The scaffold's lasting value is consistency and velocity for everything that follows. Without shared primitives and an agreed layout, each later feature would diverge, undermining the constitution's conventions. This is essential but secondary to first proving the stack and security boundary actually work.

**Independent Test**: Review the running application and codebase to confirm the app shell (navigation + sidebar) renders in the project's visual identity, the API exposes browsable documentation in the development environment, versioned routes are in place, and shared paging/error conventions and a common record base are present and used by the sample slice.

**Acceptance Scenarios**:

1. **Given** the application is running, **When** a developer opens the app, **Then** it presents a navigation + sidebar shell styled with the project's design tokens.
2. **Given** the API is running in development, **When** a developer opens its documentation view, **Then** all available endpoints are browsable and callable.
3. **Given** the backend, **When** a developer inspects its conventions, **Then** versioned routes, a shared paged-list result shape, a shared paging request shape, a common record base (with identity and audit timestamps), and a global error format are all present — with versioning, the record base, and the error format exercised by the sample slice, and the paging shapes provided for future list endpoints.
4. **Given** the application is running, **When** it is viewed on a mobile-width viewport, **Then** the shell and dashboard remain usable — no clipped content, no unintended horizontal scrolling, and navigation stays reachable.

---

### User Story 4 - Reproducible, low-friction environment (Priority: P2)

A developer or operator brings the system up in any environment with configuration supplied externally (never hard-coded), and the database schema is brought up to date automatically on startup, so local, Dev, and Prod behave identically apart from configuration and secrets.

**Why this priority**: Environment parity and reproducibility prevent "works on my machine" failures and make later features deployable. It depends on the stack existing (Story 1) but is foundational for everyone working after this feature.

**Independent Test**: Using only externally-supplied configuration, start the stack against an empty database and confirm the schema is created automatically and the system reports healthy, with no manual migration step.

**Acceptance Scenarios**:

1. **Given** an empty database, **When** the backend starts, **Then** the schema is brought up to date automatically before the system reports healthy.
2. **Given** a fresh clone, **When** a developer supplies local configuration from the provided sample and runs the single start command, **Then** all services start and the application is reachable.
3. **Given** configuration and secrets, **When** the same build is run in different environments, **Then** behavior differs only by configuration — not by architecture or code paths.

### Edge Cases

- **Database unreachable at request time**: Health reporting must degrade gracefully to "unhealthy" rather than surfacing a raw error or crashing.
- **Database unreachable at startup**: Automatic schema-update on startup must fail safely and visibly (clear, non-sensitive startup failure) rather than starting in a broken state.
- **Unauthenticated access to protected resources**: Rejected at the server regardless of any client-side state.
- **Backend error of any kind**: Returns a generic, well-formed error with no internal detail leaked to the client.
- **Missing/invalid local configuration**: Startup fails with a clear message pointing to the configuration sample, not with leaked secrets.
- **Small/mobile viewport**: The app shell's navigation must remain reachable (e.g. collapse to a mobile pattern) rather than overflowing or hiding content off-screen.
- **Automatic schema-update in a deployed environment**: Applied automatically on startup in every environment, including production — a deliberate, documented trade-off (see Assumptions) that must not block startup indefinitely or leave the schema half-applied.

## Requirements *(mandatory)*

### Functional Requirements

#### End-to-end vertical slice

- **FR-001**: The system MUST start the full stack (database, backend API, frontend, and local email capture) locally with a single command.
- **FR-002**: The backend MUST expose a public health endpoint that reports overall status and whether the database is reachable.
- **FR-003**: The frontend MUST present a dashboard that retrieves and displays the backend health status, demonstrating a complete frontend → API → database round trip.
- **FR-004**: The health reporting MUST degrade gracefully to "unhealthy" when the database is unreachable, without crashing or exposing internal details.

#### Security boundary (auth pipeline, no auth features)

- **FR-005**: The backend MUST enforce an authentication/authorization pipeline such that designated protected endpoints reject requests lacking valid credentials.
- **FR-006**: The backend MUST include at least one protected sample endpoint that returns "unauthorized" when called without valid credentials, proving the pipeline is active.
- **FR-007**: The system MUST store session credentials only in a manner inaccessible to client-side scripts (not in browser-script-accessible storage).
- **FR-008**: The frontend MUST include a route guard that redirects unauthenticated access of guarded areas toward sign-in, and a request layer that attaches credentials and handles an unauthorized response by attempting session renewal and otherwise routing to sign-in.
- **FR-009**: The system MUST NOT expose stack traces, internal exception detail, or secrets to the client; all errors MUST be returned in a generic, well-formed format.
- **FR-010**: The identity foundation (a user record and its persistence) MUST be established, WITHOUT implementing sign-up, sign-in, or password-reset endpoints or screens (explicitly deferred).

#### Shared conventions for future features

- **FR-011**: The backend MUST expose versioned API routes so future versions remain cleanly separable.
- **FR-012**: The backend MUST provide a shared paged-list result shape and a shared list-request (paging) shape, and MUST require list-returning endpoints to page rather than return unbounded collections.
- **FR-013**: All persisted records MUST derive from a common base providing a unique identifier and automatically-maintained created/modified audit timestamps; the audit timestamps MUST be set automatically rather than by hand.
- **FR-014**: The backend MUST expose browsable, interactive API documentation in the development environment.
- **FR-015**: The frontend MUST render an application shell (top navigation + sidebar) styled from the shared design tokens, with client-side routing in place.
- **FR-016**: The frontend's visual styling MUST be driven by the shared design system tokens rather than ad-hoc values.

#### Cross-device usability

- **FR-025**: The application MUST be usable on both desktop and mobile devices. The app shell (navigation + sidebar) and the dashboard MUST adapt responsively so that on small/mobile viewports content is not clipped, there is no unintended horizontal scrolling, and primary navigation remains reachable (e.g. collapses to a mobile-appropriate pattern).
- **FR-026**: All frontend work delivered by this feature MUST be validated for both desktop and mobile usability before it is considered done, including an automated check exercising at least one representative desktop and one representative mobile viewport.

#### Environment, configuration, and reproducibility

- **FR-017**: The system MUST source all configuration and secrets from external configuration (committed sample for local use; the real values never committed), never hard-coded.
- **FR-018**: The backend MUST bring the database schema up to date automatically on startup in every environment, including production, and MUST fail safely (clear, non-sensitive error) if it cannot.
- **FR-019**: Each deployable service MUST ship its own container definition, and the full stack MUST be orchestrated for local development from a single composition.
- **FR-020**: The same build MUST behave identically across local, Dev, and Prod, with differences limited to configuration and secrets.

#### Quality scaffolding

- **FR-021**: The project MUST include a backend automated-test harness with at least one integration test that exercises the vertical slice against a real database, runnable on demand within the containerized environment.
- **FR-022**: The project MUST include a frontend automated-test harness for both unit and end-to-end tests, each with at least one sample test, runnable on demand within the containerized environment.
- **FR-023**: Project automation/scripts added by this feature MUST follow the repository's standardized scripting convention.
- **FR-027**: Local development and all automated test execution MUST work entirely through the container-based environment (a single orchestrated stack). The feature MUST NOT depend on host-level dev servers or host-installed language runtimes to run the app or its tests.

#### Out of scope (explicitly deferred)

- **FR-024**: The following MUST NOT be implemented in this feature and are deferred to dedicated future work: continuous integration/deployment pipelines, cloud infrastructure provisioning, real-time/live-update transport, development seed data, the Teams/Tournaments/Forum domains, and all authentication screens/endpoints (sign-up, sign-in, password reset).

### Key Entities *(include if feature involves data)*

- **User (identity foundation)**: Represents a platform member for authentication purposes. Established as the identity foundation only; profile, roles, and membership semantics arrive with later features. Derives from the common record base.
- **Common Record Base**: The shared foundation every persisted record builds on — a unique identifier plus automatically-maintained created and modified timestamps. Not an end-user concept; it standardizes data across all future features.
- **System Health (read model)**: A transient, non-persisted view describing overall status and database reachability, surfaced by the health endpoint and shown on the dashboard.
- **Tenancy/scoping principle (decision, not an entity yet)**: A documented convention for the hybrid community model — global community/forum/tournaments, with teams optionally associated to a club/organization. Captured now as guidance for future features; no club/organization or team records are created in this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can go from a fresh clone to the running application using only the provided configuration sample and a single start command in under 15 minutes, with no manual database setup step.
- **SC-002**: With the stack running, the dashboard displays a health status reflecting live database connectivity 100% of the time the database is up, and reports "unhealthy" (never a crash or raw error) when the database is down.
- **SC-003**: 100% of requests to the protected sample endpoint without valid credentials are rejected as unauthorized, while the public health endpoint succeeds without credentials.
- **SC-004**: No client-facing response in any tested error scenario contains a stack trace, internal exception text, or secret value.
- **SC-005**: Starting the backend against an empty database results in a fully up-to-date schema with zero manual migration commands.
- **SC-006**: The backend and frontend test harnesses each run on demand and their sample tests pass, including the backend integration test that exercises the slice against a real database.
- **SC-007**: The shared conventions are present and ready for reuse without inventing new patterns — the common record base and the global error format are exercised end-to-end by the sample slice, and the paging primitives (list-request + paged-result shapes) are provided for future list-returning endpoints (no list endpoint ships in this slice, so paging is available but not yet exercised).
- **SC-008**: The running application presents the navigation + sidebar shell in the project's visual identity, and the API's interactive documentation lists and can invoke the available endpoints in the development environment.
- **SC-009**: The app shell and dashboard render and remain fully usable across a representative desktop viewport and a representative mobile viewport (e.g. ~1280px and ~375px wide) with no clipped content or unintended horizontal scrolling, verified by an automated multi-viewport check.
- **SC-010**: The application and its full automated test suite can be started and run using only the container-based environment, with no host-level dev server or host-installed language runtime required.

## Assumptions

- **Stack is pre-decided by the constitution**: The technology stack, architectural style (thin controllers, services behind interfaces, no repository layer, DTO mapping), data-access rules, security model, containerization, email approach, scripting convention, and secret-management approach are fixed by `.specify/memory/constitution.md` and are treated as given rather than re-decided here.
- **Walking-skeleton depth**: "Done" means one health slice end-to-end plus one protected sample endpoint proving the auth pipeline — not a complete product feature.
- **Identity foundation only**: A user record and its persistence and the full auth *pipeline* are in scope, but no sign-up/sign-in/password-reset endpoints or screens; those are a separate future feature.
- **Hybrid tenancy is recorded, not built**: The hybrid global/club-scoped model is documented as a convention for future features; no club/organization or team records exist after this feature.
- **Automatic schema-update everywhere (including production) is a deliberate trade-off**: Chosen explicitly for environment parity and zero-step startup. The risk (an unintended or partial schema change applied automatically on a production deploy) is accepted for now and should be revisited if/when production data and uptime guarantees make a gated migration step preferable.
- **Existing repository assets are reused/aligned**: The existing local orchestration, placeholder container definitions, email templates/service, and design tokens are reused; the local database engine version is aligned to the constitution's specified version.
- **Local email capture**: Local transactional email is captured by the existing local mail tool; the deployed email provider is configured later.
- **Container-only local + test workflow**: Local development and all automated test execution run exclusively through the container-based environment — there is no supported host-level dev-server workflow. Anything needed to run the app or its tests must work inside containers.
- **Responsive by default**: The product is used on both desktop and mobile; all frontend work is built and validated responsively from the start, following the design system's layout/responsive guidance. Native mobile apps are out of scope (responsive web only).
- **Deferred items are genuinely out of scope**: CI/CD, cloud infrastructure, real-time transport, seed data, and the Teams/Tournaments/Forum domains are not part of this feature and will not be partially implemented here.
