# Feature Specification: Self-Hosted Umami Analytics

**Feature Branch**: `033-umami-analytics`

**Created**: 2026-07-28

**Status**: Draft

**Input**: User description: "I want to self-host Umami analytics — privacy-friendly, cookieless web analytics so I can see which parts of JuggerHub actually get used, without handing visitor data to a third party. Served same-origin so the measurement isn't silently dropped by privacy blockers; Dev + Prod + an opt-in local profile; analytics data on the existing in-cluster database."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The owner can see what people actually use (Priority: P1)

The person running JuggerHub opens an analytics dashboard and sees, for a period they choose, how many people visited, which pages they viewed, where they arrived from, and what devices they used. Today this is entirely invisible: decisions about what to build next are made on intuition, because no usage signal exists at all.

**Why this priority**: This is the entire point of the feature. Everything else — privacy posture, environment separation, hardening — is a constraint on this outcome rather than a separate benefit. Delivered alone, it already answers "is anyone using the trainings tab?" and is a complete, useful product.

**Independent Test**: Visit several pages of the running app as an ordinary visitor, then open the dashboard and confirm those exact page views appear with correct paths, counts, and timestamps.

**Acceptance Scenarios**:

1. **Given** a visitor loads the JuggerHub landing page, **When** the owner opens the analytics dashboard, **Then** that visit is counted and the landing page appears in the page list.
2. **Given** a visitor navigates between several in-app screens without a full page reload, **When** the owner views the page list, **Then** each screen is recorded as its own page view rather than being collapsed into a single entry.
3. **Given** a week of traffic has accumulated, **When** the owner selects a date range, **Then** they see visitor counts, most-viewed pages, referrers, and device/browser/country breakdowns for that range.
4. **Given** the owner wants to compare periods, **When** they change the selected range, **Then** figures update for the newly selected period without needing an export or a manual query.

---

### User Story 2 - Visitors are measured without being identified (Priority: P2)

A person browsing JuggerHub is counted in aggregate usage figures, but nothing is stored on their device and nothing recorded about *them* can single them out — not their account, not their email, not their full network address. If they have actively signalled that they do not wish to be tracked, nothing about them is recorded at all.

Note the deliberate boundary: the *viewer* is never identified, but by owner decision (FR-008) the *page* is recorded verbatim, so a view of a member's profile records which profile it was.

**Why this priority**: Choosing to self-host rather than use a hosted third party was motivated by privacy. A version of this feature that quietly collected identifying data would defeat its own purpose, and — because the platform serves EU users — would also create a legal exposure the owner did not sign up for. It ranks below P1 only because P1 is the outcome and this is the constraint shaping it.

**Independent Test**: Complete a full browsing session, then inspect browser storage for any analytics-set cookie or stored identifier (expect none), and inspect every stored analytics field for any value identifying the *viewer* (expect none — page paths excepted, per FR-008). Repeat with Do Not Track enabled and confirm nothing at all is recorded.

**Acceptance Scenarios**:

1. **Given** a visitor browses the site, **When** their browser storage is inspected afterwards, **Then** the analytics tooling has set no cookie, no local storage entry, and no other persistent identifier.
2. **Given** a visitor has enabled Do Not Track or Global Privacy Control, **When** they browse the site, **Then** no analytics event is sent or recorded for them.
3. **Given** a signed-in member uses the app, **When** their recorded events are inspected, **Then** nothing links those events to *their own* account, username, email, or profile.
4. **Given** any recorded event, **When** its stored fields are inspected, **Then** no full network address is retained.
5. **Given** a recorded page view of a member's profile, **When** the record is inspected, **Then** it shows which profile was viewed (per FR-008) but not who viewed it.

---

### User Story 3 - Measurement that isn't silently thrown away (Priority: P2)

The owner trusts the numbers because they are broadly complete. Measurement is served from JuggerHub's own address, so the common privacy blocklists that drop recognisable third-party analytics hosts do not quietly erase a large share of the audience — which, for a technically-inclined sport community, would otherwise be substantial.

**Why this priority**: Equal in priority to P2 because it determines whether P1's numbers mean anything. Analytics that under-counts by an unknown and audience-dependent margin is worse than no analytics, because it invites confident wrong conclusions.

**Independent Test**: With a widely-used content blocker enabled in the browser, visit several pages and confirm those views still appear in the dashboard.

**Acceptance Scenarios**:

1. **Given** a visitor with a mainstream content blocker enabled, **When** they view pages, **Then** those views are still recorded.
2. **Given** the site is deployed, **When** its analytics is set up, **Then** no additional domain name, DNS record, or certificate is required.
3. **Given** a period of traffic, **When** recorded page views are compared against the web server's own access log for the same period, **Then** the great majority of real page views are present in analytics.

---

### User Story 4 - Development traffic never pollutes the real numbers (Priority: P3)

Developers and the Dev environment generate traffic constantly — clicking through flows, running tests, demoing changes. None of it appears in the figures the owner uses to make decisions about the live platform.

**Why this priority**: Without this the numbers become untrustworthy over time, but the pollution accumulates gradually rather than breaking anything on day one. It can follow the first three stories.

**Independent Test**: Generate traffic in the local and Dev environments, then confirm the production figures show none of it.

**Acceptance Scenarios**:

1. **Given** traffic is generated locally and in Dev, **When** production figures are viewed, **Then** none of that traffic appears.
2. **Given** a developer starts the local stack in the ordinary way, **When** the stack comes up, **Then** no analytics containers are started and no extra resources are consumed.
3. **Given** a developer wants to verify analytics behaviour locally, **When** they opt in explicitly, **Then** the full analytics stack starts and behaves as it does when deployed.
4. **Given** the same released application build, **When** it is deployed to Dev and to Prod, **Then** each records into its own separate site without rebuilding the application.

---

### User Story 5 - The dashboard is not a new way into the platform (Priority: P3)

The analytics dashboard is reachable from the internet and therefore is treated as a login surface in its own right: no shipped default credentials survive, its secrets come from the same protected store as the platform's other secrets, and a compromise of it cannot reach member data.

**Why this priority**: Adding an internet-facing administrative login to a platform whose defining constraint is security-first is the main risk this feature introduces. It is P3 only because it is a gate on release rather than an independently demonstrable user benefit — no part of this feature may ship without it.

**Independent Test**: Attempt the product's documented default credentials against the deployed dashboard (expect failure); confirm the analytics datastore is unreachable from outside the cluster; confirm the analytics credentials cannot read application data.

**Acceptance Scenarios**:

1. **Given** the deployment has completed, **When** anyone attempts the product's documented default administrator credentials, **Then** access is refused.
2. **Given** the deployed configuration, **When** version-controlled files are inspected, **Then** no analytics secret is present in any of them.
3. **Given** the analytics service's database credentials, **When** they are used to attempt to read application tables, **Then** access is refused.
4. **Given** the analytics datastore, **When** it is probed from outside the cluster, **Then** it is unreachable.

---

### Edge Cases

- **The analytics service is down or unreachable.** Every page must load and behave normally; the visitor sees no error, no delay, and no degraded experience. The affected events are lost, and that is the correct trade — analytics is never worth a broken page.
- **The analytics service is slow.** Measurement must never hold up first render or block interaction, and must not sit waiting indefinitely.
- **A burst of traffic or a retry loop.** Failed measurement must not be retried in a way that amplifies load against a service that is already struggling (constitution Principle VII).
- **A visitor blocks the tracker anyway.** Their views are simply not counted; nothing about the site breaks and no message is shown.
- **Analytics write load competes with application load.** Because analytics shares the existing database instance, analytics activity must not be able to starve the application of database capacity, and a failure or restart of analytics must not corrupt or lock application data.
- **The dashboard is left signed in on a shared machine.** Sessions must expire rather than remain valid indefinitely.
- **A page path contains a member identifier** (for example a profile or team URL containing a handle). By owner decision (FR-008) such paths are recorded verbatim, so the page list will name individual members and teams. Two consequences follow and must be planned for rather than discovered: the dashboard becomes a surface that discloses member-level browsing subjects, which raises the stakes on restricting who can reach it (User Story 5); and the page list will have a long tail of one-visit paths, which makes "most-viewed pages" less useful than it would be with grouped routes.
- **A visitor's browser blocks storage entirely or has an unusual configuration.** Measurement degrades to nothing recorded, never to an error.
- **A restricted network or corporate proxy.** Failed measurement is dropped silently.

## Requirements *(mandatory)*

### Functional Requirements

#### Measurement

- **FR-001**: The system MUST record a page view each time a visitor views a page, including navigations that happen within the application without a full page reload.
- **FR-002**: The owner MUST be able to see, for a period of their choosing: number of visitors and visits, the pages viewed and their relative popularity, where visitors arrived from, and a breakdown by device type, browser, operating system, and country.
- **FR-003**: Recorded analytics MUST remain available for at least 12 months so that year-over-year and seasonal comparisons are possible.
- **FR-004**: The system MUST record which environment a page view came from, so that figures can be viewed per environment.

#### Privacy

- **FR-005**: The system MUST NOT store or transmit any value that identifies the **visitor** — no account identifier, username, email address, display name, or full network address belonging to the person doing the browsing. This requirement is about the viewer; see FR-008 for what is recorded about the *page being viewed*.
- **FR-006**: The system MUST NOT write cookies, local storage, session storage, or any other persistent identifier to a visitor's device.
- **FR-007**: The system MUST NOT record anything for a visitor whose browser signals Do Not Track or Global Privacy Control.
- **FR-008**: Page paths MUST be recorded verbatim, including path segments that carry a member handle, team handle, or record identifier. **This is an explicit owner decision, taken with the trade-off stated** — see Assumptions → "Full page paths are recorded verbatim". The practical consequence is that the analytics store will contain which member profiles and team pages were viewed and when. It records the *subject* of a page; it never records who the viewer was (FR-005).
- **FR-009**: All analytics data MUST be stored within the EU and MUST NOT be transmitted to any third-party service.
- **FR-010**: *(Moved to Out of Scope — visitor-facing privacy disclosure is deferred to a dedicated privacy-policy feature. See Out of Scope and the "Deferred disclosure" assumption.)*

#### Non-interference

- **FR-011**: Failure, slowness, or unavailability of analytics MUST NOT block, delay, or visibly degrade any part of the application.
- **FR-012**: Measurement MUST NOT delay the first render of a page.
- **FR-013**: When measurement cannot be delivered, the affected events MUST be dropped silently — no visible error, and no retry behaviour that increases load on a failing service (constitution Principle VII).
- **FR-014**: Analytics activity MUST NOT be able to exhaust the shared database's capacity to the point of degrading the application.

#### Reach

- **FR-015**: **Measurement** — the tracker script and the collection endpoint — MUST be served from the application's own address, requiring no additional domain name, DNS record, or certificate. *(Amended during planning. This originally covered the dashboard too; that proved purchasable only by forking and continuously rebuilding the analytics product, because the only supported way to serve its dashboard from a subdirectory is a build-time setting. The requirement is now scoped to measurement, which is what it was protecting — see FR-016 and SC-002. The dashboard is served from its own hostname per environment, an accepted cost of one manually-created DNS record; its certificate is automatic. The dashboard's location has no effect on measurement completeness.)*
- **FR-016**: Measurement MUST NOT be identifiable by the address patterns that mainstream privacy blocklists match, so that measurement is not silently dropped for a large share of the audience.

#### Environments

- **FR-017**: Every environment (local, Dev, Prod) MUST run the same set of analytics resources, differing only in sizing and configuration (constitution Principle V).
- **FR-018**: Each environment MUST record into its own separate site, so traffic from local development and Dev never appears in Prod figures.
- **FR-019**: Starting the local stack in the ordinary way MUST NOT start any analytics component; running analytics locally MUST be an explicit opt-in.
- **FR-020**: The same released application build MUST work in every environment — environment-specific analytics configuration MUST be applied when the application is deployed or started, never fixed at build time.

#### Security

- **FR-021**: The dashboard MUST require authentication before showing any data.
- **FR-022**: The analytics product's shipped default administrator credentials MUST NOT be usable once deployment has completed.
- **FR-023**: All analytics secrets MUST come from the same protected secret store already used for the platform's other deployed secrets, and MUST NOT appear in any version-controlled file.
- **FR-024**: The analytics datastore MUST NOT be reachable from outside the cluster.
- **FR-025**: The analytics service's data access MUST be confined to its own data — it MUST NOT be able to read or modify application data, even though it shares a database instance.
- **FR-026**: Dashboard sessions MUST expire rather than remain valid indefinitely.

### Key Entities

- **Tracked Site**: One measured environment of JuggerHub (local, Dev, Prod). Has a name and its own identifier; figures are always reported per site, which is what keeps environments separate.
- **Page View**: A single record that a page was viewed. Carries the page path, referrer, coarse device/browser/operating-system descriptors, a country, and a timestamp. Carries nothing that identifies a person.
- **Visit**: A group of page views by the same visitor over a short window, derived without storing anything on the visitor's device and without retaining a durable identifier for that visitor.
- **Dashboard Account**: A credentialed login that may view figures. Held only by the owner; unrelated to and separate from platform member accounts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The owner can answer "which five pages were most visited in the last seven days" within 30 seconds of opening the dashboard, without exporting data or writing a query.
- **SC-002**: At least 90% of the page views recorded in the web server's own access log for a given period are also present in analytics for that period.
- **SC-003**: After a complete browsing session, inspection of the visitor's browser storage finds zero cookies or stored identifiers set by analytics.
- **SC-004**: A visitor browsing with Do Not Track enabled produces zero recorded events.
- **SC-005**: An audit of every stored analytics field finds zero values identifying the *viewer*, and zero full network addresses. (Page paths are excluded from this audit by FR-008.)
- **SC-006**: With analytics stopped entirely, every page still loads within its normal time budget, and no visitor-facing error appears.
- **SC-007**: Introducing measurement changes page load time by no more than 100 ms at the 95th percentile.
- **SC-008**: Over any 30-day window, production figures contain zero page views originating from local development or Dev.
- **SC-009**: A default local start-up runs the same number of containers as before this feature, consuming no additional memory.
- **SC-010**: The product's documented default credentials fail against the deployed dashboard.
- **SC-011**: Analytics data from at least 12 months ago remains queryable in the dashboard.
- **SC-012**: Given a recorded view of a member profile page, the record identifies the profile but contains no field identifying who viewed it.

## Assumptions

- **Analytics product**: Umami, self-hosted, chosen by the owner. It is cookieless by design, stores no personal data, and is a mature open-source product with an active maintainer — so the privacy requirements above are largely inherent to the choice rather than something to be built.
- **Data retention is indefinite by default.** Because no personal data is stored, there is no data-minimisation obligation forcing deletion, and analytics value grows with history. FR-003 sets a floor, not a ceiling.
- **Dashboard access is owner-only.** Access is granted through the analytics product's own accounts, managed by hand. It is deliberately *not* wired to the platform's existing platform-administrator allowlist: that would couple an external tool to the platform's authorisation model for the benefit of a single user, and every platform admin does not automatically need usage figures.
- **Blocker evasion is a conscious, accepted decision.** Serving measurement first-party means visitors who use content blockers are still counted. The owner has accepted this explicitly, on the basis that this is cookieless, first-party, non-identifying measurement with no cross-site tracking and no data sold or shared — and paired with honouring Do Not Track and Global Privacy Control (FR-007), so anyone who has actively expressed a preference is still respected.
- **Nearly all measured traffic will be from signed-in members**, because the platform is authenticated-only (feature 026) — only the landing, sign-in, registration, and opt-in public profile pages are reachable without an account. This raises the stakes on FR-005 and FR-008 considerably: unlike a public marketing site, almost every measured page view belongs to a known member, so the separation between "measured" and "identified" must hold strictly.
- **The application's own database instance has spare capacity** for analytics write traffic at the platform's current scale. The owner has accepted the shared-instance trade-off (analytics shares a disk and a single-replica instance with application data) in exchange for not paying for a second database.
- **Full page paths are recorded verbatim** (FR-008) — an explicit owner decision, taken after the trade-off was put to them. The reasoning accepted: grouping paths into route patterns costs implementation effort and loses detail the owner wants, and the viewer is never identified either way. The accepted cost: the analytics store records which member profiles and team pages were viewed and when, so it holds subject-side data about members even though it holds none about viewers. Under EU data-protection law a URL containing a username is generally treated as personal data, which means this feature processes personal data and the "cookieless analytics needs no legal basis" shortcut does **not** apply to it. Revisit if member-profile traffic grows enough to make the page list a browsing-interest record.
- **No device storage occurs** (FR-006), so the consent rules that govern storing or reading information on a visitor's device are not engaged. Whether a consent banner or another legal basis is nonetheless required follows from the personal-data processing introduced by FR-008, and is a question for the deferred privacy-policy work rather than one this spec settles.
- **Deferred disclosure.** The owner has decided the visitor-facing privacy disclosure belongs to a separate, full privacy-policy feature rather than this one. Consequence, recorded plainly: between this feature shipping and that one landing, JuggerHub measures EU visitors — including the FR-008 subject-side data — with no privacy disclosure anywhere in the product. Tracked as a follow-up so it is a scheduling decision rather than an oversight.
- **Success criterion SC-002 is measured against the existing web server access log**, which already records every request and therefore provides an independent ground truth for how much measurement is being lost.

## Dependencies

- The existing single-origin architecture: the application already serves everything from one address and reverse-proxies internal services, which is what makes same-origin measurement possible without new infrastructure.
- The existing in-cluster database, which will host analytics data alongside application data in a separate database.
- The existing secret pipeline from protected deployment environments into the running cluster, used for the platform's other secrets.
- The existing single infrastructure definition applied to every environment, which analytics must fit into rather than work around.

## Out of Scope

- Replacing or supplementing backend logging, error tracking, or operational telemetry. This feature measures page usage, nothing more.
- Custom event instrumentation beyond page views — measuring specific interactions such as sign-ups completed, teams created, or trainings scheduled. Worth a follow-up once page-level data reveals where to look.
- Any change to existing application behaviour, appearance, or data model.
- Exposing usage figures to anyone other than the owner, including inside the application's admin area.
- Per-member or cohort analytics — attributing measured activity to a known member. Deliberately excluded: incompatible with FR-005. Note this is a different thing from FR-008, which records the page's subject, not the viewer.
- **The visitor-facing privacy disclosure and privacy policy page** (formerly FR-010). The application has no privacy policy, imprint, or data-protection page today; the owner has decided a complete one is its own feature. Tracked as **GH #92**. See the "Deferred disclosure" assumption for what this means in the interim.
- Grouping recorded page paths into route patterns. Excluded by the FR-008 decision; revisit if the page list's one-visit tail makes the dashboard hard to read.
- Introducing a Content-Security-Policy header. None exists today; if one is added later it will need to permit measurement, which is noted here as a forward dependency rather than work in this feature.
