# ADR-0011: CI/CD with GitHub Actions and deployment to AKS

- **Status:** Accepted
- **Date:** 2026-10-05

## Context

Every change must be built, tested against real infrastructure and packaged reproducibly, and
deployments must be repeatable, auditable and free of stored cloud credentials. The target
runtime is Kubernetes on Azure (AKS), while local development must work on any laptop.

## Decision

**Continuous integration**: `.github/workflows/ci.yml`

1. Build with warnings as errors; run unit and architecture tests; collect coverage.
2. Run integration tests (Testcontainers on the runner's Docker).
3. Render every Kustomize overlay and validate it with **kubeconform**; validate the Compose file.
4. On `main`, build and push images to **GHCR** with a matrix per service, tagged with the
   **commit SHA** (immutable) and `latest`, with BuildKit cache, SBOM and provenance attestations.

**Packaging**

- Multi-stage Dockerfiles: the SDK image builds, the `aspnet` runtime image runs, as the
  **non-root** `$APP_UID` user. The restore layer is cached by copying project files first.
- Kubernetes manifests with **Kustomize**: an environment-agnostic `base` (probes, resources,
  HPA, PDB, security context compatible with the *restricted* Pod Security Standard) plus overlays:
  - `local`: in-cluster PostgreSQL, Redis, Kafka, OTel Collector and Jaeger, dev secrets.
  - `aks`: managed Azure services (PostgreSQL Flexible Server, Azure Cache for Redis, Event Hubs
    Kafka endpoint), **Key Vault** secrets through the Secrets Store CSI driver with **Workload
    Identity**, and the AKS application routing ingress with a TLS certificate from Key Vault.

**Continuous delivery**: `.github/workflows/cd-aks.yml`

- Triggered by a successful CI run on `main` (or manually with an image tag).
- Authenticates to Azure with **OpenID Connect** (federated credentials). No secrets are stored
  in GitHub other than identifiers.
- Gated by the `production` **environment** (required reviewers).
- Pins images to the commit SHA, runs a server-side dry run, applies, waits for the rollout, and
  **rolls back automatically** on failure.

## Consequences

**Positive**

- Every deployed image is traceable to a commit and to its tests.
- No long-lived cloud credentials anywhere, and no application secrets in Git or in GitHub.
- The same manifests describe local and cloud environments. Only overlays differ.

**Negative / accepted trade-offs**

- Azure resources (AKS, Key Vault, PostgreSQL, Redis, Event Hubs, identities) are not yet
  provisioned as code. Bicep/Terraform is on the roadmap. Placeholders in the `aks` overlay
  document what must exist.
- Push-based deployment (`kubectl apply` from CI) needs cluster access from the pipeline.
  A GitOps controller (Flux/Argo CD) would invert this and is a natural evolution.
- Azure Event Hubs' Kafka endpoint does not support every Kafka feature, and its support
  depends on the tier. Verify producer idempotence support for the chosen tier, and if needed
  set `Kafka__Properties__enable.idempotence=false` in the overlay.

## Alternatives considered

- **Azure DevOps Pipelines:** equivalent capability. GitHub Actions keeps the code and pipeline
  in one place for an open-source project.
- **Helm:** better for distributing reusable charts. Kustomize is simpler for one team's own
  environments and needs no templating language.
- **Azure Container Apps:** less to operate than AKS, but the goal here is to demonstrate
  Kubernetes-level concerns (probes, HPA, PDB, Pod Security, Workload Identity).
