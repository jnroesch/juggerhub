# Feature Specification: Azure AKS Hosting & Infrastructure-as-Code

**Feature Branch**: `015-hosting`

**Created**: 2026-07-11

**Status**: Draft

**Input**: User description: "Hosting & infrastructure-as-code for JuggerHub on Azure using Terraform + Kubernetes (AKS). Define the setup once so it runs on multiple environments (Dev, Prod, later Staging) with identical architecture and only sizing/config differences."

## Overview

JuggerHub currently runs only locally via `docker-compose`. This feature establishes
reproducible, code-defined hosting on Azure so the application can be deployed to
multiple environments that are architecturally identical and differ only in
configuration and sizing. The stakeholders are the **platform operators** (who
provision and evolve environments) and the **deploying developers** (whose merges
flow to Dev and Prod through the pipeline). The "users" throughout this spec are
these operators and the automated deployment pipeline, not end users of JuggerHub.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Provision a complete environment from code (Priority: P1)

An operator can stand up a brand-new, fully working JuggerHub environment on Azure
by selecting an environment target and applying the infrastructure definition. The
result is a running cluster hosting the backend API, the frontend SPA, and the
database, reachable over the network, with no manual portal clicking.

**Why this priority**: Without the ability to provision an environment at all, no
other capability matters. This is the minimum viable slice — one command path from
"nothing" to "a reachable running app."

**Independent Test**: Target the Dev environment, apply the definition against an
empty Azure subscription (state backend pre-existing), and confirm the frontend
loads in a browser via the environment's public address and can register/log in a
user (exercising backend + database end-to-end).

**Acceptance Scenarios**:

1. **Given** an empty Azure subscription with only the pre-existing state backend,
   **When** the operator applies the definition for Dev, **Then** a cluster is
   created running backend, frontend, and database, and the frontend is reachable
   over the network.
2. **Given** a freshly provisioned environment with its DNS A record set to the
   environment's static IP, **When** a user opens the environment's HTTPS hostname,
   **Then** the SPA loads over a valid certificate, API calls to `/api` succeed, and
   real-time `/hubs` connections upgrade to secure WebSockets (`wss`) successfully.
3. **Given** a provisioned environment, **When** the operator inspects it, **Then**
   no secret values are present in the infrastructure code or state in plaintext,
   and configuration was injected from the environment's secret store.

---

### User Story 2 - Add a new environment without changing architecture (Priority: P1)

An operator can introduce an additional environment (e.g. Staging) by adding one
new per-environment variable file and one workspace, without copying, forking, or
diverging the infrastructure definition. Every environment is guaranteed to share
the same architecture.

**Why this priority**: The core user requirement is "define once, run on many
environments." A new environment must not require editing shared resource
definitions — only supplying different values.

**Independent Test**: With Dev and Prod already defined, add a Staging variable
file and workspace, apply it, and confirm Staging comes up with the same set of
resources as Dev/Prod, differing only in the values supplied (size/counts).

**Acceptance Scenarios**:

1. **Given** existing Dev and Prod definitions, **When** the operator adds a
   Staging variable file and selects a Staging workspace, **Then** Staging can be
   provisioned with no changes to the shared resource definitions.
2. **Given** a diff of any two environments' definitions, **When** compared,
   **Then** the only differences are variable values (sizing, counts, names), never
   the set or shape of resources.

---

### User Story 3 - Size each environment independently (Priority: P2)

An operator can give each environment different sizing — smaller and cheaper for
Dev, larger and redundant for Prod — purely through per-environment values.
Specifically, Prod runs multiple backend instances while Dev runs one, and compute
and storage sizes differ per environment.

**Why this priority**: Cost control and production resilience are the practical
reason for multiple environments; without independent sizing the operator would
overpay for Dev or underserve Prod.

**Independent Test**: Apply Dev and Prod from the same definition and confirm Prod
has more backend instances and larger compute/storage than Dev, with both driven
solely by their respective variable files.

**Acceptance Scenarios**:

1. **Given** a Dev variable file specifying one backend instance and a Prod file
   specifying two or more, **When** each is applied, **Then** Dev runs exactly one
   backend instance and Prod runs the specified higher count.
2. **Given** per-environment compute/storage values, **When** each environment is
   applied, **Then** node/VM size and database storage size match that
   environment's values.
3. **Given** a Prod environment under increased load, **When** capacity thresholds
   are exceeded, **Then** the node pool scales out automatically; Dev does not
   autoscale.

---

### User Story 4 - Isolated, shared remote state (Priority: P1)

An operator's applies use a single, durable remote state location shared by all
environments, with each environment's state isolated from the others so an apply to
one environment can never read or mutate another's state.

**Why this priority**: State corruption or cross-environment contamination is the
highest-consequence failure mode in infrastructure-as-code; it must be structurally
prevented from the first environment onward.

**Independent Test**: Apply Dev and Prod, then confirm two separate state entries
exist in the one shared state container and that selecting one environment's
workspace shows only that environment's resources.

**Acceptance Scenarios**:

1. **Given** the shared state backend, **When** Dev and Prod are each applied,
   **Then** each environment has its own isolated state entry within the single
   shared container.
2. **Given** the state backend's hosting resource group, **When** the
   infrastructure definition is applied, **Then** that resource group and its
   storage are never created, modified, or destroyed by the definition (managed
   outside it).
3. **Given** an operator selects the wrong environment by mistake, **When** they
   plan, **Then** the plan reflects only the selected environment's state and does
   not surface another environment's resources.

---

### User Story 5 - Continuous deployment through the pipeline (Priority: P2)

When code is merged, the pipeline builds and publishes container images, applies the
infrastructure for the target environment, and rolls the new images out to the
cluster, with secrets and configuration injected from the environment's secret store
rather than living in code.

**Why this priority**: Automated delivery is the day-to-day value once environments
exist; it is P2 because environments can be operated manually in the interim.

**Independent Test**: Merge a trivial change, observe the pipeline build/publish
images, apply the target environment, and confirm the running app serves the new
build without manual intervention.

**Acceptance Scenarios**:

1. **Given** a merge to the deployment trigger, **When** the pipeline runs, **Then**
   images are published to the registry and the target environment is updated to run
   them.
2. **Given** per-environment secrets in the environment's secret store, **When** the
   pipeline deploys, **Then** those secrets reach the workloads as runtime
   configuration and never appear in code, logs, or state.
3. **Given** a failed rollout, **When** the new instances do not become healthy,
   **Then** the previous version keeps serving traffic (no downtime from a bad
   deploy).

---

### Edge Cases

- **Wrong environment selected**: applying without an explicitly selected
  environment target (e.g. a default/unset workspace) must not silently create or
  mutate resources — it must be prevented or clearly refused.
- **DNS not yet pointed**: after an environment is provisioned but before its A
  record resolves, certificate issuance (HTTP-01) cannot complete; the system MUST
  keep retrying and succeed automatically once the A record points at the static IP,
  without a re-apply.
- **Certificate renewal**: certificates MUST renew automatically before expiry with
  no downtime and no manual action.
- **Registry pull failure**: if the cluster cannot authenticate to the image
  registry, provisioning surfaces a clear, actionable failure rather than a partly
  running environment.
- **Real-time connections**: the ingress path must sustain long-lived, upgraded
  WebSocket connections for `/hubs`, not just short request/response calls.
- **Database persistence**: restarting or rescheduling the database workload must not
  lose data; storage must survive pod restarts.
- **Secret rotation**: changing a secret in the environment's secret store must be
  deployable without changing infrastructure code.

## Requirements *(mandatory)*

### Functional Requirements

**Definition & multi-environment**

- **FR-001**: The hosting setup MUST be defined once as infrastructure-as-code and
  applied to multiple environments (initially Dev and Prod; Staging added later)
  that are architecturally identical.
- **FR-002**: Environments MUST differ only in configuration and sizing values,
  never in the set or shape of resources or in application behavior.
- **FR-003**: Adding a new environment MUST require only adding a new
  per-environment variable file and selecting a new isolated environment target — no
  edits to shared resource definitions.
- **FR-004**: Each environment MUST be selectable via a per-environment variable
  file (`dev`, `prod`, and later `staging`).

**Sizing**

- **FR-005**: The number of backend application instances MUST be configurable per
  environment; Dev defaults to one and Prod to two or more.
- **FR-006**: Compute size (nodes/VMs) and database storage size MUST be
  configurable per environment.
- **FR-007**: Prod MUST automatically scale compute capacity out under load; Dev MUST
  NOT autoscale.

**Workloads**

- **FR-008**: Each environment MUST run the backend API, the frontend SPA, and the
  database as the deployed workloads (mirroring the local compose stack minus
  local-only tooling).
- **FR-009**: The frontend MUST be reachable over the network and MUST route API and
  real-time traffic to the backend, including successful WebSocket upgrades for
  real-time connections.
- **FR-010**: The database MUST run inside the cluster with storage that persists
  across pod restarts and rescheduling.
- **FR-011**: Local-only tooling (the local mail catcher) MUST NOT be deployed to any
  environment.

**State & backend**

- **FR-012**: Terraform remote state MUST live in a single shared state container,
  with each environment's state isolated from the others.
- **FR-013**: The resource group and storage hosting the remote state MUST be created
  and managed outside this definition (a documented bootstrap step) and MUST never be
  created, modified, or destroyed by applying the definition.
- **FR-014**: Applying without an explicitly selected environment target MUST NOT
  create or mutate resources.

**Registry & images**

- **FR-015**: The cluster MUST pull application images from the project's container
  registry, authenticating via credentials injected from the environment's secret
  store.

**Networking & domain**

- **FR-016**: Each environment MUST be reachable at a stable, environment-specific
  hostname under the project's custom domain (`juggerhub.com`): Prod at the apex
  `juggerhub.com` (with `www.juggerhub.com` redirecting to it), Dev at
  `dev.juggerhub.com`, and Staging (later) at `staging.juggerhub.com`.
- **FR-016a**: Each environment MUST expose a **static** public IP address that does
  not change across deploys, so registrar-managed DNS A records remain valid.
- **FR-017**: Each environment MUST serve all traffic over HTTPS with an
  automatically issued and renewed certificate for its hostname(s); plain HTTP MUST
  redirect to HTTPS. Certificate issuance MUST NOT require manual steps beyond the
  one-time DNS A-record setup.

**Secrets & configuration**

- **FR-018**: Deployed secrets and configuration MUST be sourced from the
  environment's secret store and injected at deploy time; they MUST NOT appear in
  code, logs, or state in plaintext. A dedicated cloud secret-vault service MUST NOT
  be used.
- **FR-019**: The backend MUST receive, per environment, its database connection,
  token signing key, transactional-email credentials and sender/base-URL settings,
  and the administrator allowlist.
- **FR-020**: Rotating a secret MUST be deployable without changing infrastructure
  code.

**Delivery**

- **FR-021**: The deployment pipeline MUST build and publish images, apply the target
  environment, and roll new images out to the cluster.
- **FR-022**: A failed rollout MUST NOT take down the currently serving version.

**Governance**

- **FR-023**: The project constitution MUST be amended to reflect the platform choice
  (container-orchestration platform instead of the previously mandated app-hosting
  platform), keeping the existing registry choice and the "no dedicated secret-vault
  service" rule intact.

### Key Entities *(include if feature involves data)*

- **Environment**: a named, isolated deployment target (Dev, Prod, later Staging)
  with its own configuration values, sizing, isolated state, and secret set; shares
  architecture with all other environments.
- **Environment configuration**: the per-environment set of values (compute size,
  instance counts, storage size, autoscaling on/off, names/addresses) that
  parameterize the shared definition.
- **Remote state**: the single shared, durable store holding one isolated state
  entry per environment; its hosting resources are managed outside the definition.
- **Workload**: a deployed application component within an environment (backend,
  frontend, database) with its own sizing and configuration.
- **Secret set**: the per-environment collection of sensitive values injected at
  deploy time and never persisted in code or state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An operator can provision a complete, reachable environment from an
  empty subscription (state backend pre-existing) with a single apply and no manual
  portal steps.
- **SC-002**: Adding a new environment requires changes to only per-environment
  configuration (one new variable file and workspace) and zero changes to shared
  resource definitions.
- **SC-003**: A diff between any two environments' definitions shows only differences
  in configuration values, never in the set or shape of resources.
- **SC-004**: Prod runs at least two backend instances while Dev runs exactly one,
  driven solely by per-environment configuration.
- **SC-005**: Each environment's state is isolated within one shared state container,
  and the state's hosting resource group is never touched by applying the definition.
- **SC-006**: No secret value appears in plaintext in the infrastructure code or
  state; all deployed secrets originate from the environment's secret store.
- **SC-007**: A newly provisioned environment serves the SPA, successful API calls,
  and a successful real-time WebSocket connection through its HTTPS hostname over a
  valid, trusted certificate.
- **SC-008**: Once an environment's DNS A record points at its static IP, a valid
  certificate is issued automatically (no manual steps) and renews before expiry;
  plain HTTP redirects to HTTPS.
- **SC-009**: A merge results in updated images running in the target environment
  with no manual intervention, and a failed rollout leaves the prior version serving.

## Assumptions

- The target cloud is Azure; the orchestration platform is a managed Kubernetes
  service (Standard tier), and the infrastructure tool is Terraform using workspaces
  plus per-environment variable files (confirmed with the user).
- The database runs in-cluster (not a managed database service), and the operator
  accepts ownership of its availability and upgrades (confirmed with the user).
- The container registry remains the project's existing GitHub-hosted registry
  (confirmed with the user); the cluster authenticates to it via an injected pull
  credential.
- A dedicated cloud secret-vault service is intentionally not used; secrets live in
  the environment's secret store and are injected at deploy time (per constitution).
- The state backend's resource group and storage account already exist (or are
  created by a one-time documented bootstrap) before any environment is applied.
- Initial environments are Dev and Prod; Staging is anticipated but not provisioned
  as part of this feature.
- The custom domain `juggerhub.com` has been purchased; DNS stays with the domain
  registrar (not delegated to Azure), so A records are created manually against each
  environment's static IP. Certificates are issued per hostname via HTTP-01 (no DNS
  credentials, no wildcard).

## Out of Scope

- **Database backup, restore, and disaster recovery** (scheduled dumps, retention,
  point-in-time recovery) — deferred to its own dedicated feature.
- Delegating DNS to Azure DNS, wildcard certificates, and DNS-01 challenges (registrar
  keeps DNS; per-host HTTP-01 is used instead).
- Migrating to a managed database service.
- Multi-region or geo-redundant deployment.
- Use of a dedicated cloud secret-vault service.
