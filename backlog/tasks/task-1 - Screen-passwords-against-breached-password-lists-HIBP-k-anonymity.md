---
id: TASK-1
title: Screen passwords against breached-password lists (HIBP k-anonymity)
status: To Do
assignee: []
created_date: '2026-06-30 08:48'
labels: []
dependencies: []
references:
  - 'https://haveibeenpwned.com/API/v3#PwnedPasswords'
  - 'https://pages.nist.gov/800-63-3/sp800-63b.html'
ordinal: 1000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Augment the password policy with a check against known-breached passwords using the Have I Been Pwned 'Pwned Passwords' range API (k-anonymity: only the first 5 chars of the SHA-1 hash are sent, never the password). Deferred from the Authentication feature (002): the team chose the constitution's composition rules as-is to start; breach-screening is a high-value advisory-aligned addition (NIST SP 800-63B) to layer on later. Should plug into the same backend validation path used by register and password-reset so it covers every place a password is set.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 A custom IPasswordValidator<User> (or equivalent) rejects passwords found in the HIBP Pwned Passwords range API
- [ ] #2 Only the SHA-1 hash prefix (first 5 hex chars) is sent to HIBP; the full password/hash never leaves the backend
- [ ] #3 Breach-screening runs on both registration and password reset/change
- [ ] #4 Outbound HIBP failures fail open (do not block legitimate sign-up) and are logged, not surfaced to the client
- [ ] #5 A generic, non-enumerating validation message is returned when a password is rejected as breached
- [ ] #6 Behavior is covered by tests with the HIBP HTTP call mocked
<!-- AC:END -->
