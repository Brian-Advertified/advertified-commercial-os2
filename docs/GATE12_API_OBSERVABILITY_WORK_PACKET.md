# Gate 12 Commercial API observability work packet

**Owner direction:** 2026-08-31, continue local production hardening after the verified object-byte
recovery slice.

**Verified predecessor:** The final Release Commercial API suite passes 88/88, architecture passes
23/23, the Release test graph builds with zero warnings/errors, tracked and non-ignored source has
zero secret-scan findings, all six local Compose dependencies are healthy, and API liveness/readiness
return HTTP 200. Gate 12 remains in progress.

**Authority:** Local non-production implementation and deterministic verification only. No cloud
resource, production data, live provider, external communication, deployment, commit or push.

## Bounded requirement

Add one privacy-safe Commercial API request-completion telemetry boundary. Emit a stable structured
completion event correlated to the existing request correlation ID and the current ASP.NET trace,
and configure UTC JSON console output. Reuse .NET 10's built-in ASP.NET server activity and
`http.server.request.duration` metric; do not create duplicate request spans, counters or histograms.

## Acceptance evidence

1. A request-completion middleware runs immediately after correlation assignment and outside the
   downstream exception handler. It observes handled failures and the final resolved route/status.
2. Stable event 12003 records only normalized method, matched route template or fixed `unmatched`,
   status code, finite non-negative duration milliseconds, correlation ID and trace ID.
3. Event 12003 never includes raw path, query, body, headers, cookies, credentials,
   tenant/user/client or resource identifiers. Unknown HTTP methods map to one fixed fallback value.
4. The middleware reuses `Activity.Current`, adds only the correlation ID as a trace tag, and creates
   no new ActivitySource or request metric. Tests observe the built-in ASP.NET hosting duration
   measurement with low-cardinality method, route and status tags.
5. Default console logging is configured as single-record JSON with UTC timestamps. Existing test
   logging overrides remain possible; no provider is cleared or duplicated in application code.
   Ambient console scopes are disabled because ASP.NET request scopes contain the raw request path.
6. Deterministic tests cover a matched health route, a secret-bearing unmatched path, fixed inbound
   correlation, a valid trace ID, structured state fields, Activity correlation and built-in metric
   tags. They prove raw path/query/correlation/tenant values are absent from Event 12003 and the
   observed metric tags; they do not certify every application log or framework Activity tag.
7. Focused formatting/tests, the complete Release API suite, warning-free build, architecture checks,
   Compose validation, source secret scan and runtime smoke checks pass.
8. Capability and evidence documents distinguish local instrumentation from central telemetry,
   dashboards, alerts, SLO calculation, sampling and named operations ownership.

## Verification status

Implemented and verified locally on 2026-08-31. The focused observability tests pass 3/3, the
complete final-source Release API suite passes 91/91 in 4 minutes 39 seconds, the final Release test
graph builds with zero warnings/errors, and the architecture suite passes 23/23. Compose remains
valid with six healthy services. A direct updated-binary smoke emitted parseable UTC JSON events for
matched 200 and unmatched 404 requests without the raw path/query canaries in Event 12003.

The first focused run exposed raw `RequestPath` data through ASP.NET console scopes; scopes were
disabled and the privacy case then passed. The first architecture run rejected `OTHER` because it is
also a governed business code; the HTTP fallback now uses the framework convention `_OTHER`, and the
guard passes without an exception or suppression.

This is local instrumentation evidence, not central observability or Operations approval. Detailed
evidence is retained in `docs/evidence/gate12-api-observability-20260831/`.

## Explicitly out of scope

- OpenTelemetry collector/exporter, CloudWatch account, production trace sampling or cloud mutation;
- dashboards, alert routing, error-budget/SLO certification, synthetic login or on-call ownership;
- business-outcome, queue, outbox, inventory-freshness, provider-cost or worker-saturation metrics;
- browser/page analytics, user profiling or unnecessary personal-information collection;
- global application-log redaction, framework Activity-tag review and exporter-side filtering;
- claiming Gate 12, Operations review or production readiness approved.
