# ADR-0001: Record architecture decisions

- **Status:** Accepted
- **Date:** 2026-10-05

## Context

This repository is a *reference* architecture: its value lies as much in **why** things are built
a certain way as in the code. Code shows *what* was decided. It doesn't show the alternatives
that were weighed or the trade-offs that were consciously accepted. Without that record, future
readers either repeat old debates or undo decisions without knowing what they protected.

## Decision

Significant architectural decisions are recorded as ADRs in `docs/adr`, one Markdown file per
decision, numbered sequentially and following [`0000-template.md`](0000-template.md).

- A decision is "significant" if it is hard to reverse, affects more than one service, or
  introduces a technology.
- Accepted ADRs are not edited (apart from typos and links). A changed decision gets a new ADR
  that supersedes the old one, and the old ADR's status is updated to point to it.
- ADRs are reviewed in pull requests like code.

## Consequences

**Positive**

- Onboarding: newcomers learn the rationale, not just the result.
- Decisions become explicit and reviewable before they are implemented.

**Negative / accepted trade-offs**

- Some writing overhead, kept low by the short template.

## Alternatives considered

- **Wiki pages:** drift away from the code they describe and are not versioned with it.
- **Code comments only:** good for local decisions, but they cannot capture cross-cutting ones.
