# Feature Specification: Server-Side Image Processing Pipeline

**Feature Branch**: `034-image-processing-pipeline`

**Created**: 2026-07-30

**Status**: Draft

**Input**: User description: "Server-side image processing pipeline for uploaded images (GitHub issue #98). Add a server-side image-processing component applied to every uploaded image before it is stored. Replaces today's validation-only path (magic-byte sniff + raw size cap, no decoding). Reusable by upcoming showcase galleries for profiles and teams. Pipeline: sniff/validate type → decode → dimension guard before rasterization → auto-orient + strip EXIF/GPS/ICC → resize to a max dimension → re-encode to WebP. Reject invalid/oversized/corrupt inputs without altering existing stored media. Runs synchronously. Storage backend and the gallery feature are out of scope."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Uploaded images are normalized to a small, consistent form (Priority: P1)

A member picks a photo from their phone or computer to use as their profile picture. The file they select may be a large, high-resolution image several megabytes in size. When they upload it, the platform accepts the generous original, but what it stores and later serves is a compact, consistently-encoded version — small enough that it does not bloat storage or slow down page loads, and visually equivalent to what they chose.

**Why this priority**: This is the core value of the feature and the reason issue #98 exists — "so large uploads don't bloat the DB or the wire." Without normalization, everything else is decoration. It is also the smallest slice that delivers standalone value: even with no other behavior, shrinking every stored image is a win.

**Independent Test**: Upload a large (e.g. 4000×3000, multi-MB) JPEG as an avatar, then retrieve the stored avatar and confirm it is materially smaller, bounded to the configured maximum dimension, and served in the normalized format — with no change to the upload/retrieve endpoints callers use.

**Acceptance Scenarios**:

1. **Given** a valid high-resolution photo larger than the target dimension, **When** a member uploads it, **Then** the stored image is downscaled so its largest side does not exceed the configured maximum dimension and its stored size is a small fraction of the original.
2. **Given** a valid image already smaller than the target dimension, **When** a member uploads it, **Then** it is re-encoded to the normalized format and is **not** upscaled (in fit mode its pixel dimensions are preserved; in square-crop mode it is center-cropped to a square no larger than its shorter side, still without upscaling).
3. **Given** a member had a previous picture, **When** they upload a new valid image, **Then** the new normalized image replaces the old one and is the version returned on subsequent retrieval.

---

### User Story 2 - Personal metadata is stripped and orientation is corrected (Priority: P2)

A member uploads a photo taken on their phone. Phone photos commonly embed the exact GPS coordinates where the picture was taken, camera details, and an orientation flag. When the image is stored and later shown to anyone who can view the profile, none of that embedded personal metadata travels with it, and the picture appears the right way up rather than sideways.

**Why this priority**: This is a privacy and correctness guarantee that must hold before images are shown to other members. It is distinct from normalization (an image could be resized but still leak GPS) and independently valuable, but it ranks below P1 because the storage/wire win is the headline goal.

**Independent Test**: Upload a photo that contains EXIF GPS coordinates and a non-default orientation flag, retrieve the stored result, and confirm the served image contains no EXIF/GPS/ICC metadata and is already rotated to display upright.

**Acceptance Scenarios**:

1. **Given** an image containing EXIF/GPS/ICC metadata, **When** it is uploaded, **Then** the stored image contains none of that metadata.
2. **Given** an image whose EXIF orientation flag indicates rotation, **When** it is uploaded, **Then** the stored image pixels are rotated to the correct display orientation and no orientation flag is relied upon afterward.

---

### User Story 3 - Malicious, corrupt, and oversized uploads are rejected safely (Priority: P3)

Someone submits a file that is not really a usable image — a tiny compressed file that expands to an enormous number of pixels (a "decompression bomb"), a corrupt or truncated image, a non-image file with an image extension, or an image whose processed result would still be unreasonably large. The platform rejects the upload with a clear, non-technical reason, does not exhaust server memory attempting it, and leaves any picture the member already had untouched.

**Why this priority**: This is the safety net that makes the feature safe to expose. It is essential, but ranks P3 because it protects the happy paths delivered by P1/P2 rather than delivering new user-facing capability on its own.

**Independent Test**: Submit (a) a crafted decompression-bomb image, (b) a truncated/corrupt image, (c) a non-image file renamed to an image extension, and (d) an over-limit input; confirm each is rejected with a clear reason, the server stays healthy, and a pre-existing picture is unchanged.

**Acceptance Scenarios**:

1. **Given** an input whose declared pixel dimensions or decoded size exceed the safety limit, **When** it is uploaded, **Then** it is rejected **before** the full image is expanded into memory, and the request does not degrade server availability.
2. **Given** a corrupt, truncated, or non-image file, **When** it is uploaded, **Then** it is rejected with a clear, non-technical reason and no partial result is stored.
3. **Given** any rejected upload, **When** the member already had a stored picture, **Then** that existing picture remains exactly as it was.
4. **Given** an input larger than the accepted upload size limit, **When** it is submitted, **Then** it is rejected without being processed.

### Edge Cases

- **Animated source (e.g. animated WebP/APNG)**: accepted and flattened to a single still (first/representative) frame; the stored result is a static image.
- **Image with an unusual but valid aspect ratio (very wide/tall)**: in **fit** mode the largest side is bounded and aspect ratio preserved (no distortion); in **square-crop** mode the image is center-cropped to a square. Neither mode distorts the image.
- **Already-normalized image re-uploaded**: processing is idempotent enough that re-processing an already-small, already-stripped image yields an equivalent result without growth.
- **Valid image that re-encodes larger than the configured stored-size ceiling** (rare, e.g. noisy photo at high quality): the pipeline still guarantees the stored result respects the configured bounds, or the upload is rejected with a clear reason rather than storing an oversized blob.
- **Transparency (alpha channel)**: transparency present in the source is preserved in the normalized output.
- **Zero-byte or empty upload**: rejected as empty, consistent with today's behavior.

## Clarifications

### Session 2026-07-30

- Q: How should the pipeline handle aspect ratio when resizing (crop vs fit)? → A: Configurable per upload context, with two modes — **fit** (downscale preserving aspect ratio so the largest side is bounded) and **square-crop** (center-crop to a square, then downscale). The avatar context uses **square-crop**; the future showcase-gallery context (#99) uses **fit**.
- Q: Does this feature reprocess already-stored avatars, or apply only to new uploads? → A: New uploads only. **No pre-existing stored avatar data exists in any environment**, so no backfill, data migration, or backward-compatibility handling is required.
- Q: How should animated images (animated WebP / APNG) be handled? → A: Accept them and store a single still (first/representative) frame; processing then proceeds normally. The stored output is always static.
- Q: How granular should a rejection reason be? → A: Distinct, non-technical reasons (unsupported type / too large / image dimensions too large / unreadable image), extending the existing status set.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST process every uploaded image through a single shared processing step before it is stored, replacing the current validate-only path.
- **FR-002**: The system MUST determine the actual image type by inspecting file content, MUST NOT trust the client-declared content type, and MUST accept only a defined allow-list of input formats (PNG, JPEG, WebP).
- **FR-003**: The system MUST reject any input that is not a decodable image of an allowed type, and MUST NOT expose internal errors or stack traces. Rejection reasons MUST be **distinct and non-technical**, covering at least: unsupported/invalid type, exceeds the accepted input-size limit, image dimensions exceed the decode safety limit, and unreadable/corrupt image — extending the existing status set (which already distinguishes invalid type, too large, and empty).
- **FR-004**: The system MUST enforce a safety limit on decoded image dimensions and MUST reject over-limit inputs **before** allocating the full decoded image, to prevent decompression/pixel-bomb memory exhaustion.
- **FR-005**: The system MUST bake in the source orientation and MUST remove all embedded metadata (including EXIF, GPS location, and ICC/color-profile data) from the stored image.
- **FR-006**: The system MUST resize images to a configured maximum dimension using a **per-context resize mode**: (a) **fit** — downscale preserving aspect ratio so the largest side does not exceed the maximum; or (b) **square-crop** — center-crop to a square, then downscale to the maximum. In both modes the system MUST NOT upscale images already smaller than the target. The avatar context uses square-crop; the future gallery context (#99) uses fit.
- **FR-007**: The system MUST re-encode the processed image to a single normalized output format (WebP) at a configured quality, and MUST report the stored content type accordingly to callers.
- **FR-008**: The system MUST guarantee the stored output respects configured size/dimension bounds; if a valid image cannot be brought within bounds, the upload MUST be rejected with a clear reason rather than stored oversized.
- **FR-009**: On any rejected or failed upload, the system MUST leave any previously stored image for that subject unchanged.
- **FR-010**: The system MUST perform processing synchronously within the upload request (no background queue or worker), returning success only after the normalized image is ready to store.
- **FR-011**: The processing capability MUST be reusable across upload contexts (profile/team avatars today, showcase galleries later) rather than being specific to a single owner type, and MUST allow its limits (maximum dimension, output quality, size ceiling) to be configured per context.
- **FR-012**: The accepted upload input-size limit and the stored-output size ceiling MUST be separately configurable, so the platform can accept a generous original (e.g. a large phone photo) while guaranteeing a small stored result.
- **FR-013**: Processing limits (maximum dimension, output quality, input and output size caps, decode safety limit) MUST be configuration with safe built-in defaults, and MUST behave identically across local, Dev, and Prod (differing only in configured values, never in behavior).
- **FR-014**: The public avatar URL, the upload/retrieve endpoints, and frontend behavior MUST remain unchanged for callers; the processing is internal to the existing service seam.
- **FR-015**: The system MUST NOT retain the original uploaded bytes after producing the normalized image (only the processed result is stored).
- **FR-016**: Transparency present in a source image MUST be preserved in the normalized output.
- **FR-017**: The system MUST accept animated image inputs (e.g. animated WebP / APNG) and store a single still frame; the stored output MUST be a static image.

### Key Entities *(include if feature involves data)*

- **Processed Image**: the normalized result of the pipeline — the encoded image bytes, its normalized content type, and its pixel dimensions. This is what gets stored; the original upload is discarded.
- **Processing Profile / Constraints**: the configurable settings applied for a given upload context — **resize mode (fit vs square-crop)**, maximum output dimension, output quality, accepted input-size limit, stored-output size ceiling, and the decode safety (pixel-count) limit. Different contexts (avatar vs future gallery) use different profiles: the avatar profile uses square-crop, the gallery profile uses fit.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A large, high-resolution photo (e.g. ~4000×3000, several MB) uploaded as an avatar results in a stored image whose largest side is at or below the configured maximum dimension and whose stored size is at least 90% smaller than the original.
- **SC-002**: 100% of stored images produced by the pipeline contain no EXIF, GPS, or ICC metadata.
- **SC-003**: 100% of images with a rotation-indicating orientation flag are stored already rotated to correct display orientation.
- **SC-004**: A crafted decompression-bomb input is rejected without the server's memory use spiking to unsafe levels, and the service remains responsive to other requests throughout.
- **SC-005**: 100% of rejected uploads (invalid type, corrupt, oversized, over-dimension) leave any pre-existing stored image byte-for-byte unchanged.
- **SC-006**: A typical single-image upload is fully processed and ready to store within a small, human-imperceptible time budget (target: under 1 second for images within the accepted input-size limit on standard infrastructure).
- **SC-007**: The same processing behavior and limits are observed identically in local, Dev, and Prod for the same input and configuration.

## Assumptions

- **Allowed input formats** are PNG, JPEG, and WebP (matching the current avatar allow-list); other formats are rejected. **Output** is always WebP.
- **Resize mode is configurable per context** (see Clarifications): the avatar context uses **square-crop** (center-crop to a square, then downscale), and the future showcase-gallery context (#99) uses **fit** (downscale preserving aspect ratio). Neither mode distorts the image.
- **No pre-existing stored avatar data exists** in any environment, so this feature requires **no backfill, data migration, or backward-compatibility handling** for already-stored images. It only defines the shared processing capability and applies it to the current upload path going forward.
- **A single default processing profile** targets the avatar use case (a modest maximum dimension suitable for profile pictures); the showcase-gallery context (#99) may configure a larger dimension when it is built. Concrete default values (maximum dimension, quality, caps) are a planning detail chosen to keep stored avatars small while visually clean.
- The **accepted input-size limit stays generous** (around the current ~8 MB request cap) so large phone photos are accepted; the **stored output** is bounded by resize + re-encode rather than by the input cap.
- **Animated inputs are accepted and flattened** to a single still frame (see Clarifications); the stored output is always a static image.
- **Processing is synchronous and in-request**, justified by the low, human-paced frequency of avatar/gallery uploads (roughly one per member, rarely changed) — no background job infrastructure is introduced.
- **Storage is out of scope**: the pipeline produces normalized bytes + content type and hands them to the existing storage path unchanged, whether that path persists to the database (today) or object storage (issue #97, separate).
- **The gallery feature is out of scope** (issue #99); this feature only makes the shared processing capability exist and applies it to the current avatar upload path.
- **Reuses the existing avatar service seam and endpoints**; no new public API surface, URL, or frontend change is introduced by this feature.

## Dependencies & Known Drift

- **Introduces the project's first image-processing dependency.** The original profile feature plan (`specs/003-profile/plan.md`) set a "No new NuGet packages" bar for avatar handling. This feature deliberately supersedes that bar; the added imaging dependency is expected **spec/plan drift** to be recorded, and any licensing consideration for the chosen library is a planning decision.
- **Related, separately-tracked work**: object/blob storage abstraction (issue #97) and showcase galleries for players and teams (issue #99). Both are out of scope here and depend on this pipeline rather than the reverse.
- Must satisfy the constitution's security (never trust the client; no raw errors to the client), environment-parity, and configuration-with-safe-defaults expectations.
