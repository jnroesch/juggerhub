---
id: TASK-2
title: Mitigate account pre-hijacking in register/verify flow
status: To Do
assignee: []
created_date: '2026-06-30 10:35'
labels: []
dependencies: []
references:
  - 'https://www.usenix.org/conference/usenixsecurity22/presentation/sudhodanan'
ordinal: 2000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Security review of feature 002 (Authentication) flagged a MEDIUM account pre-hijacking risk: registration sets the password before email verification, and VerifyEmailAsync only flips EmailConfirmed. An attacker who registers a victim's email first (with an attacker-known password) can have the account activated if the victim clicks the verification email, leading to takeover. Common to email+password+verify designs, but worth hardening given the security-first posture.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 A new registration for an existing UNVERIFIED email re-establishes password ownership (e.g. overwrites the unverified account's password with the new registrant's and rotates the prior verification token), OR the password is (re)set at verification time
- [ ] #2 Clicking a verification link cannot activate an account with a password set by a different, unverified party
- [ ] #3 Behavior covered by an integration test simulating the pre-hijacking sequence
- [ ] #4 Decision + chosen mitigation recorded in the 002 spec/research
<!-- AC:END -->
