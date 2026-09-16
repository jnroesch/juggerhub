#!/usr/bin/env pwsh
# check-ui-drift.ps1 — UI primitives drift guard (feature 024).
#
# Fails if any screen re-introduces a hand-assembled pattern that a shared
# primitive (app/shared/ui) now owns. Run in CI after the frontend build.
# Scope: frontend/apps/web/src/app/features and .../layout (NOT shared/ui itself,
# which legitimately contains the canonical class strings).
#
# The class names below were repointed by GH #298, which retired `text-subtle`,
# `text-faint` and the bare `text-danger`, and moved coral fills that carry a
# label onto `bg-brand-strong`. A rule naming a class that no longer exists
# matches nothing and guards nothing, which is the quiet way a drift guard dies.
#
# One rule is gone rather than repointed: `<p class="… text-danger" role="alert">`
# tracked the *legacy* red-5 error paragraphs, and #298 converted the last of them
# to `danger-fg` — so the population it watched is empty, and the token it named no
# longer exists for `contrast.spec.ts` to let back in. Repointing it at `danger-fg`
# would have been a different rule over a different, much larger set: every error
# paragraph in the product, whose migration to `<jh-alert>` is its own piece of work.
#
# Usage:  pwsh scripts/check-ui-drift.ps1
# Exit:   0 = clean, 1 = drift found (prints offending file:line).

$ErrorActionPreference = 'Stop'

$root = Join-Path $PSScriptRoot '..' 'frontend' 'apps' 'web' 'src' 'app'
$scanDirs = @(
  (Join-Path $root 'features'),
  (Join-Path $root 'layout')
) | Where-Object { Test-Path $_ }

# Each rule: a regex that must NOT appear in *.html, and why.
$rules = @(
  @{ Pattern = 'rounded-md bg-brand(-strong)? px-'; Why = 'Hand-assembled coral button — use `jhButton`.' }
  @{ Pattern = 'rounded-pill bg-brand(-strong)? px-(lg|md)'; Why = 'Pill-shaped brand action button — use `jhButton`.' }
  @{ Pattern = 'bg-brand[^"]*text-white|text-white[^"]*bg-brand'; Why = 'Raw text-white on a brand surface — use `text-on-accent` (or `jhButton`).' }
  @{ Pattern = '>\s*\+ [A-Z]'; Why = 'Literal "+" text glyph used as an icon — use `<jh-icon name="plus" />`.' }
  @{ Pattern = 'text-body-(sm|md) text-muted">Loading'; Why = 'Hand-rolled loading line — use `<jh-loading>`.' }
  @{ Pattern = '>[^<]*invitation'; Why = 'Non-canonical term in copy — use "invite" (routes/testids may keep the legacy path).' }
)

$violations = @()
foreach ($dir in $scanDirs) {
  $files = Get-ChildItem -Path $dir -Recurse -Filter '*.html' -File
  foreach ($file in $files) {
    $lines = Get-Content -LiteralPath $file.FullName
    for ($i = 0; $i -lt $lines.Count; $i++) {
      foreach ($rule in $rules) {
        if ($lines[$i] -match $rule.Pattern) {
          $rel = Resolve-Path -Relative -LiteralPath $file.FullName
          $violations += [pscustomobject]@{
            Location = "$rel`:$($i + 1)"
            Why      = $rule.Why
          }
        }
      }
    }
  }
}

if ($violations.Count -gt 0) {
  Write-Host "UI drift detected — the following re-introduce patterns owned by app/shared/ui primitives:`n" -ForegroundColor Red
  foreach ($v in $violations) {
    Write-Host ("  {0}" -f $v.Location) -ForegroundColor Yellow
    Write-Host ("     {0}" -f $v.Why)
  }
  Write-Host "`n$($violations.Count) issue(s). See specs/024-ui-primitives/." -ForegroundColor Red
  exit 1
}

Write-Host 'UI drift guard: clean — no hand-assembled primitive patterns found.' -ForegroundColor Green
exit 0
