# Implementation Plan: Server-Side Image Processing Pipeline

**Branch**: `034-image-processing-pipeline` (working branch `claude/image-storage-processing-9licw5`) | **Date**: 2026-07-30 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/034-image-processing-pipeline/spec.md`

## Summary

Introduce a reusable, DI'd `IImageProcessor` service that every uploaded image passes through before storage: content-type allow-listing, a header-only **pixel-dimension guard** (decompression-bomb protection) *before* full decode, EXIF-orientation bake-in, metadata (EXIF/GPS/ICC/XMP/IPTC) stripping, per-context resize (avatar = square-crop, future gallery = fit), and re-encode to WebP at a configured quality — with a stored-output size ceiling. The current avatar path (`ProfileService.SetAvatarAsync`) is rewired to call it in place of the sniff-and-size-cap-only logic; the endpoints, URLs, and the `ProfileAvatar` schema are unchanged. Processing is synchronous and in-request (uploads are rare and human-paced). Concrete library: **SixLabors.ImageSharp** (pure-managed — no native binary, so environment parity is trivial), behind the `IImageProcessor` seam so the choice is swappable. This deliberately supersedes the 003-profile "no new NuGet packages" bar (recorded drift).

## Technical Context

**Language/Version**: C# / .NET 10 (`backend/`, `TreatWarningsAsErrors=true`, nullable enabled)

**Primary Dependencies**: **SixLabors.ImageSharp** (new; pinned major). Existing: EF Core 10, Npgsql 10, ASP.NET Core MVC controllers, `Microsoft.Extensions.Options`.

**Storage**: Unchanged — `ProfileAvatars` table (`bytea` + `ContentType`) via the existing `IProfileService` seam. **No new migration.** (Object storage is issue #97, out of scope.)

**Testing**: xUnit. Unit tests for `IImageProcessor` with synthetic in-memory images built by ImageSharp (no binary fixtures). Integration tests via `JuggerHubApiFactory` (WebApplicationFactory + Testcontainers Postgres/Redis) — extend `ProfileTests`.

**Target Platform**: Linux server (Docker, AKS deployed). ImageSharp is pure-managed, runs identically on local compose / Dev / Prod with no per-arch native dependency.

**Project Type**: Web service (backend); no frontend change.

**Performance Goals**: A single upload within the input cap processes in **< 1 s** (SC-006). Uploads are low-frequency (≈ one per member, rarely changed), so synchronous in-request processing is acceptable.

**Constraints**: Peak transient memory bounded by the pixel-dimension guard (reject before decoding an over-limit image). No raw exceptions/secrets to the client (Principle I). Config via options with safe defaults, identical shape across environments (Principle V). Distinct, non-technical rejection reasons (spec FR-003, Clarifications).

**Scale/Scope**: One new service (interface + impl), one options class, a rewired service method, an extended status enum, one new options section (optional — defaults suffice), plus tests. Reusable by profiles today and teams/galleries later (#99).

## Constitution Check

*GATE: evaluated pre-research and re-checked post-design. Constitution v1.4.0.*

| Principle / Gate | Assessment | Verdict |
|---|---|---|
| **I. Security-First, Never Trust the Client** | Type is determined by decoding server-side, not the declared content type (kept). Adds a decompression-bomb guard (OWASP-aligned). Decode/encode failures are caught and surfaced as generic, non-technical reasons — no stack traces or internals reach the client. No secrets. | ✅ Pass (strengthens security) |
| **II. Thin Controllers, Service-Centric** | New logic lives in a DI'd `IImageProcessor` behind an interface; the controller is unchanged and stays thin. The processor returns an internal result record (not a client DTO); the client-facing response shape is unchanged. No object mapper. | ✅ Pass |
| **III. Disciplined Data Access** | **No entity or schema change**; `ProfileAvatar` still holds `Bytes`+`ContentType`. No new migration, no list endpoint (pagination N/A). Existing avatar write path keeps its explicit INSERT/UPDATE handling. | ✅ Pass |
| **IV. Secure Auth & Session** | Untouched — same `[Authorize]` upload / `[AllowAnonymous]` + visibility-gated read. | ✅ Pass |
| **V. Environment Parity & Containerized Deployment** | ImageSharp is pure-managed → byte-identical behavior on local/Dev/Prod with **no native binary, no new service, no new secret**. Limits are options with safe defaults, identical in shape per environment. | ✅ Pass |
| **VI. Consistent Conventions & Tooling** | Backend C# only; no `.sh` added; no frontend/Angular change. | ✅ Pass |
| **VII. Resilient by Default** | No new network call — in-process CPU work. Work is bounded by input-size + pixel-count caps (safe defaults, never unbounded). No retry/breaker needed. | ✅ Pass (N/A surface) |
| **Dependency Management** | Adds `SixLabors.ImageSharp` pinned to its **major** version; Dependabot raises majors as individual PRs. **Supersedes** the 003-profile plan's "No new NuGet packages" bar — documented drift, not a silent change. | ✅ Pass (with recorded drift) |

**Result**: No violations. Complexity Tracking table intentionally empty.

**Licensing note (owner decision, does not block design)**: ImageSharp ships under the **Six Labors Split License** — free for open-source and for organizations under the annual gross-revenue threshold; a commercial license is required above it. If JuggerHub's situation requires an unencumbered license, **SkiaSharp** (BSD, but a per-arch native binary — a small parity cost) is the drop-in alternative behind the same `IImageProcessor` seam. Captured in [research.md](./research.md).

## Project Structure

### Documentation (this feature)

```text
specs/034-image-processing-pipeline/
├── plan.md              # This file
├── research.md          # Phase 0 — library + technique decisions
├── data-model.md        # Phase 1 — config/result shapes (no DB entity change)
├── quickstart.md        # Phase 1 — validation guide
├── contracts/
│   ├── image-processor.md    # Internal IImageProcessor contract
│   └── avatar-endpoints.md   # Existing HTTP endpoints — unchanged shape, changed stored/served type + new reasons
├── checklists/
│   └── requirements.md  # Spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
backend/
├── Services/
│   ├── Media/                         # NEW — reusable, owner-agnostic media processing
│   │   ├── IImageProcessor.cs         # NEW — the seam (FR-011)
│   │   ├── ImageSharpImageProcessor.cs# NEW — ImageSharp implementation
│   │   └── ImageProcessingResult.cs   # NEW — status enum + success payload (bytes, contentType, dimensions)
│   └── Profile/
│       ├── IProfileService.cs         # EDIT — extend AvatarSetStatus with distinct failure reasons (FR-003)
│       └── ProfileService.cs          # EDIT — SetAvatarAsync delegates to IImageProcessor
├── Common/
│   ├── ProfileOptions.cs              # EDIT — MaxAvatarBytes reframed as the INPUT acceptance cap
│   └── ImageProcessingOptions.cs      # NEW — per-context profiles (resize mode, max dim, quality, output ceiling, pixel guard)
├── Controllers/
│   └── ProfilesController.cs          # (minimal/none) — new statuses already fall through to 400+reason
├── Program.cs                         # EDIT — register IImageProcessor + Configure<ImageProcessingOptions>
├── appsettings.json                   # EDIT (optional) — ImageProcessing section; safe defaults mean config is not required
└── JuggerHub.Api.csproj               # EDIT — add SixLabors.ImageSharp (pinned major)

backend/tests/JuggerHub.Api.IntegrationTests/
├── Media/
│   └── ImageProcessorTests.cs         # NEW — unit tests (resize, no-upscale, EXIF/GPS stripped, orient, webp, alpha, animated→still, bomb, corrupt, ceiling)
└── Profile/
    └── ProfileTests.cs                # EDIT — served type now image/webp; add processing assertions
```

**Structure Decision**: Single backend project (constitution's monorepo .NET). The processing capability lives in a **new `backend/Services/Media/`** namespace — deliberately *not* under `Profile/` — because FR-011 requires reuse by the future team/gallery contexts (#99). The `IProfileService` avatar seam is the only caller wired up in this feature.

## Complexity Tracking

> No constitution violations — table intentionally empty.
