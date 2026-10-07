# ADR-0012: Target .NET 8 LTS, with a planned move to .NET 10

- **Status:** Accepted
- **Date:** 2026-10-05

## Context

The project needs a Long-Term Support runtime that enterprise teams actually run in production,
with stable versions of EF Core, ASP.NET Core and the Npgsql/OpenTelemetry ecosystem.

As of this decision:

- **.NET 8 (LTS)** is the most widely deployed .NET version in enterprise environments. Its
  support **ends on 10 November 2026**.
- **.NET 10 (LTS)** was released in November 2025 and is supported until November 2028.

## Decision

Target **.NET 8** now (`net8.0`, SDK pinned in `global.json`, packages on their latest 8.x
patch or a version compatible with `net8.0`), and **plan the upgrade to .NET 10** as the next
milestone, before .NET 8 reaches end of support.

Guidelines that keep the upgrade cheap:

- One target framework, set in `Directory.Build.props`.
- Central package versions in `Directory.Packages.props`.
- No reliance on APIs deprecated in later versions. Third-party libraries are chosen for active
  maintenance.

## Consequences

**Positive**

- Matches what most teams run today, so the patterns transfer directly.
- Stable, well-documented ecosystem.

**Negative / accepted trade-offs**

- A short remaining support window. Mitigated by the planned upgrade, which mostly means changing
  `TargetFramework` and the `8.x` package lines, then adopting optional improvements:
  - `HybridCache` for stampede protection (ADR-0008);
  - built-in OpenAPI document generation (`Microsoft.AspNetCore.OpenApi`) instead of Swashbuckle;
  - EF Core 10 improvements.

## Alternatives considered

- **Start directly on .NET 10:** the longest support window, but at the time this project began
  most production estates (and the author's experience) were on .NET 8.
- **Multi-target net8.0 and net10.0:** doubles the build and test matrix for an application,
  which ships one runtime, not a library.
