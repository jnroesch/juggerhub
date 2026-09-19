# Feature Specification: PWA Shell for Web Push

**Feature Branch**: `054-pwa-shell`

**Created**: 2026-09-19

**Status**: Draft

**Input**: User description: "PWA shell for web push — GH #307. Make the web app installable and give it a registered service worker, which is the prerequisite for web push (#308) and chat push (#309). Today the app is a plain SPA: index.html has no manifest link, there is no manifest file, no @angular/service-worker, no ngsw-config.json, no worker registration in main.ts. Scope ends at "a service worker is registered at root scope and the app is installable on Android and iOS Home Screen"; subscriptions, VAPID keys and sending are #308. Three owner decisions confirmed up front and to be recorded as Clarifications: (1) a hand-written PUSH-ONLY service worker registered manually, NOT @angular/service-worker (ngsw) — no asset caching, no versioning, nothing to invalidate; the additional reason is that nginx injects the per-environment analytics snippet into index.html at serve time via sub_filter (033), and a caching worker would freeze that on the device; (2) NO install prompt/banner/copy ships in this feature — manifest + worker + registration only, zero new user-facing text; the "add to Home Screen" affordance belongs to #308's enable-push flow where the user has a reason to install; (3) NO offline mode, ever, written into the spec so nobody adds caching later by reflex — the app is entirely auth-gated live data (026). Known consequences to capture as edge cases: iOS delivers push only to a Home Screen PWA and an installed iOS app has its own cookie jar, so the user must sign in again inside the installed app (auth is an HttpOnly cookie); display standalone removes browser chrome, and the bottom nav / chat shell / admin shell already pad with env(safe-area-inset-bottom) — verify on device; nginx must serve the worker path with an exact-match location and no-cache header so the location / SPA fallback cannot shadow it (the 038 shadowing hazard); the worker file lives in apps/web/public as a static asset; manifest colours come from DESIGN.md tokens; icon set 192/512 + maskable rasterised from the existing favicon.svg (#304 is not a dependency)."

## Context: what this enables, and what it must not disturb

This feature is a prerequisite, not a product change. Nothing a player does today changes.
What changes is what the product *can* do next:

- **GH #308** (push as a notification channel) and **GH #309** (chat push) both need the
  browser to hold a background worker that can receive a message while no tab is open. Without
  one, web push cannot reach a device at all. This feature ships that worker and stops there —
  subscriptions, keys and sending are #308.
- **iOS** delivers web push only to an app that has been added to the Home Screen. Making the
  app installable is therefore not decoration; on the platform most players use, it is the
  only path by which push can arrive. This feature makes the app installable and stops there —
  telling players to install, and why, belongs to #308's enable-push flow.

Three earlier decisions constrain the shape and are honoured, not reversed:

- **Feature 026** made the whole product sign-in-only, live data throughout. There is nothing
  useful to show a player who is offline, which is why this feature adds no offline mode and
  forbids one (Clarification 3).
- **Feature 033** injects each environment's analytics configuration into the app's entry page
  at serve time, so it can change without a release. A worker that cached the entry page on the
  device would freeze that injection and silently break the property 033 was built on. That is
  the second reason (Clarification 1) the worker here never caches anything.
- **Feature 038** documented how the web server's catch-all for the app can shadow a file that
  must be served as itself. The worker script is exactly such a file: served from the app's root
  under any other page, it would be the app page, not a worker, and the failure would be silent.

## The gap, concretely

The app is a plain single-page application. Its entry page names the app and its icon, and
nothing more: no manifest that tells a phone how to install it, no background worker for the
browser to keep. A player on Android sees no install offer. A player on iPhone can add the page
to the Home Screen, but it opens as a browser bookmark, not an app. And on every platform, the
browser has nothing to hand a push message to, so #308 cannot begin.

## Clarifications

### Session 2026-09-19

- Q: Which kind of background worker — a push-only worker written for this product, or the
  framework's full worker that also caches and versions the app's files? → A: **A push-only
  worker.** It never caches, never serves, never intercepts a request; there is nothing on the
  device to go stale and nothing to invalidate on release. The framework's worker was declined
  for two reasons: it adds a class of stale-release defects for an app that has no offline value
  (026), and it would cache the entry page that 033 rewrites at serve time, freezing the
  per-environment analytics configuration on the device.
- Q: Does this feature ship an install prompt, banner or hint (Android's install offer, iOS's
  "Share → Add to Home Screen" instruction)? → A: **No.** Manifest, worker and registration
  only, with zero new user-facing text. An install affordance without a reason to install is
  noise; the reason arrives with #308's "enable notifications on this device" flow, and the
  affordance ships there, with its copy and its UI review.
- Q: Is an offline mode ever wanted? → A: **No, and the spec says so.** The product is
  sign-in-only live data (026); there is nothing to show offline. This is recorded as a
  requirement so that caching is not added later by reflex when someone next touches the worker.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The app can be installed on a phone's home screen (Priority: P1)

A player on an Android phone visits the product and the browser offers to install it; a player
on an iPhone uses the browser's own "Add to Home Screen". Either way, what lands on the home
screen carries the product's name and mark, and opening it shows the product as an app — no
browser address bar, no browser controls — laid out so that nothing sits under the phone's own
screen edges.

**Why this priority**: On iPhone, installation is the precondition for push. If the product
cannot be installed there, #308 reaches Android and desktop only, and the players most affected
by the reach gap (#309) are exactly the ones on phones.

**Independent Test**: On an Android device, visit the product and confirm the browser's install
offer appears and installs an app with the right name and icon. On an iPhone, add the page to
the Home Screen and confirm the icon and name match, and that opening it shows the product
without browser controls. Both without any change to what the product shows or says.

**Acceptance Scenarios**:

1. **Given** a player visits the product in a current Android browser, **When** the browser
   evaluates the page, **Then** it treats the product as installable and offers to install it
   through its own means, with the product's name and mark.
2. **Given** a player on an iPhone adds the product to the Home Screen, **When** they open it
   from there, **Then** it opens as a standalone app showing the product's name and mark, without
   browser controls.
3. **Given** the product is opened as an installed app on a phone with a home indicator or
   notch, **When** any screen with a fixed bottom bar or a full-height view is shown, **Then**
   the bar and the view stay clear of the device's own screen areas, exactly as they do in the
   browser today.
4. **Given** a player opens the installed app without a signed-in session, **When** it starts,
   **Then** it lands where a visit to the product's front door lands today — the sign-in screen —
   and a signed-in player lands where they land today.
5. **Given** a player on a desktop browser that supports installation, **When** they choose to
   install, **Then** the product installs as a standalone app with the same name and mark.

---

### User Story 2 - The browser holds a background worker for the product (Priority: P1)

Whenever a player opens the product in a browser that supports background workers, the browser
registers the product's worker for the whole site. The worker does nothing a player can see
today. It exists so that a later feature can hand a push message to it while no tab is open.

**Why this priority**: This is the whole reason the issue was filed. Without a registered worker
the browser has nowhere to deliver push, and #308 cannot start.

**Independent Test**: Open the product in a supporting browser, and confirm through the
browser's own developer tools that a worker is registered for the site's root, is active, and
belongs to the product. Confirm the product behaves exactly as before with the worker present.

**Acceptance Scenarios**:

1. **Given** a player opens any page of the product in a supporting browser, **When** the app
   has started, **Then** a worker is registered whose scope covers the whole site, and it becomes
   active without any further action from the player.
2. **Given** the worker is registered and active, **When** the player uses the product, **Then**
   nothing about what they see, request, or receive differs from a browser without the worker.
3. **Given** a browser that does not support background workers, **When** a player opens the
   product, **Then** the product works exactly as today; no error, no notice, no difference.
4. **Given** the worker fails to register for any reason (the script cannot be fetched, the
   browser refuses it), **When** a player opens the product, **Then** the product still starts
   and works exactly as today; the failure is invisible to the player.
5. **Given** a request for the worker's script, **When** it is served, **Then** the response is
   the worker script itself, never the app's entry page, and the browser is told not to keep a
   stale copy.

---

### User Story 3 - Every visit is the current release, always (Priority: P2)

A player opens the product after a release and sees the new release. A player opens the product
after an environment's configuration changed without a release and sees that change. Nothing
the product ships to the device outlives the visit that fetched it, and the product never
pretends to work while the player is offline.

**Why this priority**: This is the guarantee that Clarifications 1 and 3 protect. It is P2 only
because it is true today by default; the point of stating it is that it must remain true with
the worker in place.

**Independent Test**: Deploy a change and reload; the change is visible on the first reload.
Change an environment's serve-time configuration (033) without a release and reload; the change
is visible. Cut the connection and reload; the browser's own offline page appears, not a cached
version of the product.

**Acceptance Scenarios**:

1. **Given** a new release is deployed, **When** a player who had the product open reloads or
   navigates, **Then** they receive the new release; no previously fetched copy is served.
2. **Given** an environment's serve-time configuration changes without a release, **When** a
   player reloads, **Then** the new configuration is in effect.
3. **Given** the device has no connection, **When** a player opens the product or reloads it,
   **Then** the browser's ordinary offline behaviour appears; the product does not present a
   stored copy of itself or of any data.
4. **Given** a later release changes the worker itself, **When** a player next opens the
   product, **Then** the browser picks up the new worker on that visit or the next, never later
   than the browser's own ceiling for re-checking a worker.

---

### Edge Cases

- **Installed on iOS, signed in nowhere.** An app installed on an iPhone's Home Screen keeps
  its own sign-in state, separate from the browser it was installed from. A player who was
  signed in when they installed opens the app and is asked to sign in again. This is a
  platform property, not a defect; it must not be "fixed" by any attempt to share the session,
  and the app must behave exactly as a fresh visit does. #308's install hint will need to say
  so; this feature ships no text.
- **iOS older than 16.4.** The app can still be installed and opens as a standalone app; push
  will never be available to it. This feature makes no distinction; the distinction is #308's.
- **The worker script is shadowed.** If the web server ever answered a request for the worker
  script with the app's entry page, the browser would try to run a web page as a worker,
  registration would fail, and nothing would tell anyone. The requirement that the script is
  served as itself at its own address is therefore load-bearing and must be verified, not
  assumed.
- **Off-shell public pages.** The privacy policy, imprint, terms and the registration screen
  are reachable without signing in. Opening any of them registers the same worker for the whole
  site; the worker has no behaviour, so this changes nothing on those pages.
- **Installed app and external links.** A standalone app has no address bar. Links to other
  sites still open (in the platform's browser or an in-app view, at the platform's discretion);
  links within the product stay inside the app. No link the product renders today changes.
- **A browser that blocks workers or storage.** Private windows, hardened settings, or
  enterprise policy may refuse worker registration. The product must start and work regardless;
  registration is attempted once per visit and its failure is swallowed.
- **Two tabs, one worker.** The worker is shared by every open tab of the product. Because it
  does nothing, sharing it has no visible effect; this matters only for #308 and is noted so it
  is not designed against.
- **Analytics objection signals.** A player with Do-Not-Track or Global Privacy Control set
  receives no analytics (033/038). The worker is unrelated to analytics and is registered
  regardless; it sends nothing anywhere.

## Requirements *(mandatory)*

### Functional Requirements

#### Installability

- **FR-001**: The product MUST describe itself to browsers as an installable web app: its full
  name, a short name for a home-screen label, its icon set, its brand colour and its page
  background colour (both taken from the product's design tokens, never chosen ad hoc), the
  standalone display mode, and the product's front door as the start address.
- **FR-002**: The icon set MUST be derived from the product's existing mark (the one the
  favicon carries) and MUST include the sizes home screens require plus a variant that survives
  the platform's icon masking without the mark being cropped. No new mark is drawn; visual
  identity work is GH #304 and is not a dependency.
- **FR-003**: The installable description MUST be linked from the product's single entry page so
  that every route of the product is installable, including the public pages that render outside
  the signed-in shell.
- **FR-004**: When opened as an installed app, every screen MUST keep its fixed bottom bar and
  its full-height views clear of the device's own screen areas (home indicator, notch), as the
  product already does in a browser. No screen may rely on the browser's own controls existing.
- **FR-005**: The product MUST NOT ship any install prompt, banner, hint, or copy about
  installing (Clarification 2). No new user-facing text is added in any language; the
  translation catalogues are unchanged.

#### The background worker

- **FR-006**: On every visit in a supporting browser, the product MUST register a background
  worker whose scope is the whole site, once the app has started.
- **FR-007**: The worker MUST have no behaviour of its own in this feature: it MUST NOT
  intercept, answer, or cache any request, MUST NOT store any data on the device, MUST NOT
  contact any server, and MUST NOT show anything. Reacting to a push message is #308's.
- **FR-008**: Registration MUST be attempted exactly once per visit and MUST be fire-and-forget:
  if it fails, or if the browser does not support workers, the product MUST start and behave
  exactly as it does today, with no error surfaced to the player and no change in behaviour.
- **FR-009**: The worker script MUST be served at a fixed address at the site's root, as itself:
  a request for it MUST never receive the app's entry page or any other document in its place,
  and the web server MUST tell browsers not to keep a stale copy of it, so that a changed worker
  is picked up on the next visit and never later than the browser's own re-check ceiling.
- **FR-010**: The presence of the worker MUST NOT change any request the product makes, any
  response it receives, or anything it shows. A visit with the worker and a visit without it are
  indistinguishable to the player and to the server.

#### Freshness, and the prohibition on offline

- **FR-011**: The product MUST NOT provide an offline mode (Clarification 3). Nothing the
  product ships to a device — its pages, its scripts, its data — may be stored for use on a
  later visit by anything this feature adds, and no future change to the worker may add such
  storage without a new owner decision recorded in a spec.
- **FR-012**: Every visit MUST receive the current release and the current serve-time
  configuration of its environment (033). Nothing this feature adds may cause a device to keep
  presenting a previous release or a previous configuration.

#### Boundaries

- **FR-013**: This feature MUST NOT add any subscription, key, permission request, or sending of
  any kind. It ends when a worker is registered and the app is installable; everything after is
  #308.
- **FR-014**: This feature MUST NOT change anything on the server side of the product other than
  how the worker script and the installable description are served. No new data is stored about
  any player and no new request reaches the backend.

#### Disclosure

- **FR-015**: The privacy policy's account of what is kept in the player's browser MUST name the
  background worker, in all three languages with German authoritative, in generic and durable
  terms — what it is (part of the site's own program that the browser keeps so the site can act
  while no tab is open, such as receiving a notification the player asked for; not a record about
  the player), never what it happens to do or not do today. The policy's statement that the
  listed items are all that is kept MUST remain true after this feature. *(Added during planning:
  the policy's list was found to be exhaustive, and a registered worker is visible to any player
  in their browser's site data — see plan.md, research R10.)*

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a current Android browser and on a current desktop browser that supports
  installation, the product is judged installable by the browser's own criteria and installs
  with the product's name and mark, in 100% of attempts across all three environments.
- **SC-002**: On an iPhone, adding the product to the Home Screen yields a standalone app with
  the product's name and mark, and every screen with a fixed bottom bar or full-height view stays
  clear of the home indicator, verified on device.
- **SC-003**: In a supporting browser, a background worker for the site's root is registered and
  active within 5 seconds of the app starting, on 100% of visits, on every route including the
  public pages.
- **SC-004**: A request for the worker script returns the worker script, never a document, in
  all three environments; the response instructs the browser not to keep a stale copy.
- **SC-005**: A release deployed to an environment is visible on the first reload in 100% of
  cases; a serve-time configuration change (033) is visible on the first reload without a
  release.
- **SC-006**: With no connection, opening or reloading the product shows the browser's own
  offline behaviour in 100% of cases; no stored copy of the product is presented.
- **SC-007**: The product's interface catalogues gain zero entries in every language, and no
  screen, message, or control is added or changed for the player. The privacy policy gains
  exactly one disclosure paragraph in each of its three languages (FR-015) and nothing else.
- **SC-008**: A visit with the worker registered makes no requests beyond the installable
  description, the worker script, and the icons the browser chooses to fetch from the
  description; none of them reaches the backend, and no request the product already makes
  changes.
- **SC-009**: In a browser that refuses or does not support workers, every existing automated
  and manual check of the product passes unchanged.

## Assumptions

- **No backend change.** The worker and the installable description are static files served by
  the web tier; the product's backend is untouched. If planning finds a backend change
  necessary, the plan must say why.
- **No new dependency.** A push-only worker is a short hand-written file; no framework worker
  package, no build plugin, no icon-generation dependency is needed at runtime. Rasterising the
  icons from the existing mark is a one-time step whose output is committed.
- **Icons come from the existing mark.** The favicon's vector mark is the source for every
  icon size; the maskable variant pads it inside the platform's safe zone. GH #304 (visual
  identity) may later replace the mark; when it does, the icons are regenerated from it.
- **Safe-area handling already exists.** The bottom navigation, the chat shell, and the admin
  shell already pad for the device's bottom screen area. FR-004 is a verification on device,
  not new layout work; if the device walk finds a screen that does not clear the home indicator,
  that is a defect of that screen and is fixed under FR-004.
- **The install affordance, its copy, and its UI review belong to #308.** This feature has no
  new markup and no new copy, so the product's UI review gate has no surface to review here; the
  device walk under SC-002 is the verification.
- **Privacy policy gains one paragraph (corrected during planning).** The spec first assumed the
  policy could stay unchanged and asked planning to confirm. Reading it showed that its "what's
  kept in your browser" section is an exhaustive list and that the no-banner reasoning says "that's
  all there is here" — and a registered worker is something a player can see listed under the
  site's data in their browser. So the worker is disclosed there in one generic paragraph, German
  authoritative (FR-015). This is disclosure prose, not interface copy; FR-005 stands. #308, which
  stores subscriptions and adds a third-party recipient, is where the policy changes again.
- **Environments stay identical.** The worker address, the no-stale-copy instruction and the
  installable description are the same in local, Dev and Prod; nothing here is per-environment
  configuration.
- **Browser support baseline.** "Supporting browser" means current Chrome, Edge, Firefox and
  Safari on desktop, current Chrome on Android, and Safari on iOS 16.4 or later for push; older
  browsers get the product exactly as today (FR-008).
- **Dependency on nothing; dependency for #308 and #309.** This feature depends on no open
  work. #308 depends on it entirely, and #309 on #308.
