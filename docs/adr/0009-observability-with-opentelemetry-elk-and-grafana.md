# ADR-0009: Observability with OpenTelemetry, ELK and Grafana

- **Status:** Accepted
- **Date:** 2026-10-05

## Context

In a distributed, asynchronous system, a single business operation (placing an order) spans two
services, two databases, Redis and two Kafka hops, possibly seconds apart. Without correlated
telemetry, answering "why was order X rejected?" or "why is placing orders slow?" means grepping
logs on several machines. Observability must be a built-in property of every service, not an
afterthought.

## Decision

**Instrumentation: OpenTelemetry**, configured once in `BuildingBlocks.Web`
(`AddServiceDefaults`), so every service gets the same telemetry:

- **Traces:** ASP.NET Core, HttpClient, Npgsql and Redis instrumentation, plus custom spans for
  every command/query (dispatcher behavior) and every Kafka publish/process.
  W3C trace context is **propagated through Kafka headers**, so the whole saga is **one trace**.
- **Metrics:** HTTP server/client, .NET runtime, and custom business and technical metrics
  (`ordering.orders.*`, `messaging.*`, `catalog.cache.requests`, `app.request.duration`).
- Export via **OTLP** to an **OpenTelemetry Collector**, which routes traces to **Jaeger** and
  exposes metrics to **Prometheus**. **Grafana** provides dashboards (provisioned as code in
  `deploy/observability/grafana`).

**Logging: Serilog → ELK**

- Structured logs (message templates, not string concatenation), enriched with `ServiceName`
  and `Environment`.
- Compact JSON (CLEF) carries `@tr` / `@sp`, the trace and span ids, so **logs and traces are
  correlated**. Grafana links a Jaeger span to its log lines in Elasticsearch, and back.
- Shipped by Serilog's HTTP sink to **Logstash** → **Elasticsearch** → **Kibana**. JSON is also
  written to stdout for container log collectors.

**Health:** `/health/live` (process) and `/health/ready` (dependencies), mapped to Kubernetes
liveness and readiness probes.

Exporters are enabled by configuration only (`OTEL_EXPORTER_OTLP_ENDPOINT`, `Logstash:Url`), so
tests and ad-hoc runs need no backends.

## Consequences

**Positive**

- Vendor-neutral instrumentation: switching to Azure Monitor, Grafana Cloud, Datadog, etc. is a
  collector configuration change, with no code change.
- Each signal is backed by the tool best suited to it, a common setup in SRE teams.
- RED metrics (rate, errors, duration) plus business metrics on one dashboard.

**Negative / accepted trade-offs**

- Many components to run locally (~6 GB RAM for the full Compose stack). Infrastructure can be
  started selectively.
- Two log paths (ELK) vs. metrics and traces (OTel). OpenTelemetry logs via OTLP could unify them
  later; ELK was kept because it remains widespread in enterprises.
- The local stack has no security, retention tuning or high availability. In AKS, use managed
  backends (Azure Monitor / Managed Grafana / Elastic Cloud).

## Alternatives considered

- **Vendor SDKs (Application Insights SDK, Datadog tracer):** quicker to start, but lock-in.
- **Grafana LGTM stack (Loki, Tempo, Mimir):** cohesive and lighter than ELK. A strong
  alternative, and the collector makes it a configuration swap.
- **Logs only:** cannot reconstruct cross-service causality or latency breakdowns.
