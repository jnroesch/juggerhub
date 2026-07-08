# Security Policy

JuggerHub is built security-first — authorization and validation are enforced
server-side, with the [OWASP Top 10][owasp] in mind (see the
[constitution](.specify/memory/constitution.md), Principle I). It's maintained by
one person in their spare time, so please keep the guidance below in mind when
reporting.

## Reporting a vulnerability

**Please don't report security issues through public GitHub issues, discussions,
or pull requests.** Use a private channel instead:

- **GitHub Security Advisories (preferred)** —
  <https://github.com/jnroesch/juggerhub/security/advisories/new>

Helpful things to include, if you have them:

- What the issue is and its impact.
- Steps to reproduce (a proof of concept, or sample requests/responses).
- The affected area (backend API, frontend, auth, deployment config).

## What to expect

I'll acknowledge and look into reports as soon as I reasonably can — but this is a
spare-time project, so I can't promise fixed response times. Please give me a fair
chance to investigate and ship a fix before disclosing anything publicly. If you'd
like credit once it's resolved, just say so.

Thanks for helping keep JuggerHub and its community safe.

[owasp]: https://owasp.org/www-project-top-ten/
