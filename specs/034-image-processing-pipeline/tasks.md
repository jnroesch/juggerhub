---
description: "Task list for feature 034 — server-side image processing pipeline"
---

# Tasks: Server-Side Image Processing Pipeline

**Input**: Design documents from `specs/034-image-processing-pipeline/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. The spec defines an Independent Test per story plus measurable Success Criteria, the repo has an established xUnit + Testcontainers suite, and the constitution values tests — so test tasks are generated.

**Organization**: Grouped by user story (spec priorities P1→P3). Each story is independently *testable*.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1 / US2 / US3 (maps to spec user stories)

> ⚠️ **Feature-specific note on parallelism**: this is a single pipeline. The processing stages for US1, US2, and US3 all live in **one file** — `backend/Services/Media/ImageSharpImageProcessor.cs` — so the three stories are **sequential (P1 → P2 → P3), not parallel across stories**. Parallel [P] opportunities exist *within* the foundational phase and *within* each story (separate test files vs impl file), not between stories. This is a deliberate deviation from the template's usual "stories run in parallel" and reflects the feature's shape.

## Path Conventions

Single backend project: `backend/` (source), `backend/tests/JuggerHub.Api.IntegrationTests/` (tests). Paths below are exact.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Bring the imaging dependency into the project.

- [X] T001 Add `SixLabors.ImageSharp` (pinned to its current major version, per Dependency Management) to `backend/JuggerHub.Api.csproj` and run `dotnet restore backend`. Confirms the pure-managed library resolves (research D1).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish the reusable seam, config, result types, DI, and the `SetAvatarAsync` rewire — everything the pipeline needs to compile and be called, before any processing behavior exists.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 [P] Create `ImageProcessingResult` record + `ImageProcessingStatus` enum (`Success, Empty, UnsupportedType, InputTooLarge, DimensionsTooLarge, Unreadable, OutputTooLarge`) in `backend/Services/Media/ImageProcessingResult.cs` (data-model §3).
- [X] T003 [P] Create `ImageProcessingOptions` (`SectionName = "ImageProcessing"`, `MaxInputBytes`, `MaxDecodePixels`, `AllowedContentTypes`, `Avatar` profile) and `ImageProcessingProfile` (`ResizeMode { Fit, SquareCrop }`, `MaxDimension`, `Quality`, `MaxOutputBytes`) with safe defaults in `backend/Common/ImageProcessingOptions.cs` (data-model §1–§2; FR-013).
- [X] T004 [P] Define the `IImageProcessor` interface (`ImageProcessingResult Process(byte[] input, ImageProcessingProfile profile)`) in `backend/Services/Media/IImageProcessor.cs` (contracts/image-processor.md; FR-011).
- [X] T005 Create `ImageSharpImageProcessor` **skeleton** implementing `IImageProcessor` in `backend/Services/Media/ImageSharpImageProcessor.cs` — control-flow scaffold returning a passthrough `Success` with a clear `// TODO` for each stage, so DI and the caller compile. (Real stages land in US1–US3.) Depends on T002, T003, T004.
- [X] T006 [P] Extend `AvatarSetStatus` with `DimensionsTooLarge` and `Unreadable` in `backend/Services/Profile/IProfileService.cs` (data-model §4; FR-003).
- [X] T007 Register `IImageProcessor` → `ImageSharpImageProcessor` as a **singleton** and add `builder.Services.Configure<ImageProcessingOptions>(...)` in `backend/Program.cs`; add an optional `ImageProcessing` section to `backend/appsettings.json` (defaults already suffice). Depends on T004, T005, T003.
- [X] T008 Rewire `ProfileService.SetAvatarAsync` to call `_imageProcessor.Process(content, _imageOptions.Avatar)` and map **every** `ImageProcessingStatus` → `AvatarSetResult` (Success stores `result.Bytes`/`result.ContentType` via the existing INSERT/UPDATE path and discards the original; non-success returns `Fail(status, reason)` without touching stored data). Inject `IImageProcessor` + `IOptions<ImageProcessingOptions>` into the constructor. In `backend/Services/Profile/ProfileService.cs` (contracts/image-processor.md consumer contract; FR-009, FR-015). Depends on T002, T004, T006.

**Checkpoint**: Solution compiles; the avatar path calls the processor through the seam. No real normalization yet.

---

## Phase 3: User Story 1 - Uploads normalized to a small, consistent form (Priority: P1) 🎯 MVP

**Goal**: Every uploaded image is decoded, flattened to a single frame, resized within bounds (avatar = center square-crop, never upscaled), and re-encoded to a compact WebP under the output ceiling.

**Independent Test**: Upload a large multi-MB photo as an avatar; retrieve it and confirm it is a materially smaller WebP bounded to the configured dimension, with the endpoints/URLs unchanged.

### Tests for User Story 1

- [X] T009 [P] [US1] Unit tests in `backend/tests/JuggerHub.Api.IntegrationTests/Media/ImageProcessorTests.cs`: large image → WebP with largest side ≤ `MaxDimension` and ≥90% smaller bytes (SC-001); small image not upscaled (fit preserves dims; square-crop centers) (C6, C7); output is `image/webp` (C8/D5); alpha preserved (C11, FR-016); animated input → single static frame (C10, FR-017).
- [X] T010 [US1] In `backend/tests/JuggerHub.Api.IntegrationTests/Profile/ProfileTests.cs`: update `Avatar_upload_accepts_a_valid_png` to assert served `Content-Type` is `image/webp` (the one existing assertion that changes); add a test uploading a large PNG and asserting the fetched avatar is a smaller WebP.

### Implementation for User Story 1

- [X] T011 [US1] Implement the core pipeline in `backend/Services/Media/ImageSharpImageProcessor.cs`: empty check → input-size check (`InputTooLarge`) → `Image.Load` → flatten animation to first frame → resize per `ResizeMode` (Fit = `ResizeMode.Max`, SquareCrop = `ResizeMode.Crop` center, **both no-upscale**, research D4) → encode `WebpEncoder { Quality }` → enforce `MaxOutputBytes` (`OutputTooLarge`) → return `Success(bytes, "image/webp", w, h)`. (Metadata-strip and pre-decode guard are added in US2/US3.)
- [X] T012 [US1] Reconcile input vs output caps (FR-012, research D7): reframe `ProfileOptions.MaxAvatarBytes` as the generous **input** cap (~8 MB, aligned with the endpoint's `[RequestSizeLimit(8MB)]`) in `backend/Common/ProfileOptions.cs`; ensure the `InputTooLarge` reason text reflects the input limit. The small stored-output bound lives in `ImageProcessingProfile.MaxOutputBytes`.

**Checkpoint**: MVP — uploads are shrunk to a bounded WebP. Deployable/demoable.

---

## Phase 4: User Story 2 - Personal metadata stripped & orientation corrected (Priority: P2)

**Goal**: Stored images carry no EXIF/GPS/ICC/XMP/IPTC metadata and are baked to display-upright.

**Independent Test**: Upload a phone photo with EXIF GPS + a rotation flag; the stored/served image has no metadata and is upright.

### Tests for User Story 2

- [X] T013 [P] [US2] Unit tests in `backend/tests/JuggerHub.Api.IntegrationTests/Media/ImageProcessorTests.cs`: input JPEG carrying an EXIF GPS tag → output has no `ExifProfile`/IPTC/XMP/ICC (SC-002, C8); input with an EXIF orientation flag → output pixels are upright and no orientation flag remains (SC-003, C9). (Build these inputs in-memory with ImageSharp — research D10.)

### Implementation for User Story 2

- [X] T014 [US2] In `backend/Services/Media/ImageSharpImageProcessor.cs`, add `image.Mutate(x => x.AutoOrient())` and clear `Metadata.ExifProfile`, `IptcProfile`, `XmpProfile`, `IccProfile` immediately before encode (research D3; FR-005). Sequential after T011 (same file).

**Checkpoint**: US1 + US2 — normalized, small, metadata-free, upright.

---

## Phase 5: User Story 3 - Malicious, corrupt & oversized uploads rejected safely (Priority: P3)

**Goal**: Decompression bombs, corrupt/truncated files, non-images, and over-limit inputs are rejected with distinct, non-technical reasons, without exhausting memory or altering existing media.

**Independent Test**: Submit a decompression-bomb, a truncated image, a non-image, and an over-limit file; each is rejected with a clear distinct reason, the server stays healthy, and a pre-existing avatar is unchanged.

### Tests for User Story 3

- [X] T015 [P] [US3] Unit tests in `backend/tests/JuggerHub.Api.IntegrationTests/Media/ImageProcessorTests.cs`: header declaring huge dimensions → `DimensionsTooLarge` with no full decode/memory spike (SC-004, C4); truncated/corrupt bytes → `Unreadable`, no exception thrown (C5); non-image bytes → `UnsupportedType` (C3); over-`MaxInputBytes` → `InputTooLarge` (C2); over-`MaxOutputBytes` encode → `OutputTooLarge` (C12).
- [X] T016 [US3] Integration test in `backend/tests/JuggerHub.Api.IntegrationTests/Profile/ProfileTests.cs`: set a valid avatar, then submit a corrupt upload; assert `400` with a distinct reason and that the previously stored avatar is byte-for-byte unchanged (SC-005, FR-009).

### Implementation for User Story 3

- [X] T017 [US3] In `backend/Services/Media/ImageSharpImageProcessor.cs`, add the pre-decode guard **before** `Image.Load`: `Image.Identify` header read → validate detected format ∈ `AllowedContentTypes` (`UnsupportedType`) → reject `width * height > MaxDecodePixels` (`DimensionsTooLarge`) (research D2; FR-004). Wrap `Load` in try/catch → `Unreadable` (never throw, FR-003). Ensure each failure status returns a distinct, non-technical `Reason` (contracts/avatar-endpoints.md). Sequential after T011/T014 (same file).

**Checkpoint**: All three stories functional — normalize, protect privacy, and reject abuse safely.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T018 [P] Document the `ImageProcessing` config section (values + safe defaults) in `backend/appsettings.json` and note it needs no secret (Principle V / Secret Management).
- [ ] T019 ⚠️ NOT RUN LOCALLY (no .NET SDK / no Docker daemon in this environment) — CI on the PR is the verification path. Run [quickstart.md](./quickstart.md) validation: `dotnet build backend` (clean under `TreatWarningsAsErrors`) and `dotnet test backend/tests/JuggerHub.Api.IntegrationTests`; confirm all new/updated tests pass.
- [X] T020 Security pass (Principle I / OWASP): verify decode/format failures surface only generic non-technical reasons (no stack traces/internals), and that the `Identify` guard bounds peak memory. Review `ImageSharpImageProcessor.cs` + the controller mapping in `backend/Controllers/ProfilesController.cs`.
- [X] T021 [P] Confirm scope boundaries hold: no caching-header changes to `GET /{handle}/avatar` (deferred to #97), no schema/migration added, no frontend change. Tick issue #98 acceptance criteria.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup. **Blocks all stories.**
- **User Stories (Phase 3–5)**: depend on Foundational. **Sequential P1 → P2 → P3** because US1/US2/US3 edit the same `ImageSharpImageProcessor.cs`.
- **Polish (Phase 6)**: depends on the stories you intend to ship.

### Within Each Story

- Tests are written first and fail against the skeleton/prior stage, then the implementation task makes them pass.
- US1 T011 before T012 (T012 tunes caps the core path uses).
- US2 T014 is after US1 T011 (same file). US3 T017 is after T011/T014 (same file).

### Parallel Opportunities

- **Foundational**: T002, T003, T004 in parallel (separate new files); T006 in parallel (different file). T005 waits on T002–T004; T007/T008 wait on their inputs.
- **Within a story**: the test-authoring task ([P], its own test file) can be written alongside/ahead of that story's impl task.
- **Across stories**: none — see the parallelism note at the top.

---

## Parallel Example: Foundational Phase

```bash
# These three foundational tasks touch separate new files — run together:
Task T002: "Create ImageProcessingResult + status in backend/Services/Media/ImageProcessingResult.cs"
Task T003: "Create ImageProcessingOptions + Profile in backend/Common/ImageProcessingOptions.cs"
Task T004: "Define IImageProcessor in backend/Services/Media/IImageProcessor.cs"
# T006 (enum edit in Profile/IProfileService.cs) can also run alongside these.
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational (blocks everything) → 3. Phase 3 US1.
4. **STOP and VALIDATE**: upload a large photo → confirm a small WebP is stored/served. This alone delivers the headline win ("don't bloat the DB or the wire").

### Incremental Delivery

1. Setup + Foundational → seam ready.
2. US1 → normalized/shrunk uploads (**MVP**).
3. US2 → metadata stripped + upright (privacy).
4. US3 → abuse-resistant rejection.
5. Polish → validate + security pass.

Each story builds on the last in the same pipeline file; ship after any checkpoint.

---

## Notes

- [P] = different files, no incomplete-task dependency. The processor impl file is shared across US1–US3, so those impl tasks are **not** [P] relative to each other.
- No database migration and no frontend change (by design — plan.md Structure Decision).
- Commit after each task or logical group. The existing `image/png`→`image/webp` test change (T010) is expected drift, documented in contracts/avatar-endpoints.md.
- `/speckit-implement` can execute this list, or implement task-by-task with small commits per CLAUDE.md.
