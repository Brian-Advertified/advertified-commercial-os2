# Gate 12 Commercial API observability evidence

**Evidence date:** 2026-08-31

**Base commit:** `53209e7f718b64a25fc5fcf9aa83365301c5a50c`

**Working tree:** Uncommitted review on `master`; this is not a release artefact

**Live provider or production resource used:** No

## Implemented

- Added structured request-completion event 12003 immediately after correlation assignment and
  outside the downstream exception handler. It records normalized method, matched route template or
  fixed `_OTHER`/`unmatched` fallbacks, final status, duration, correlation ID and trace ID.
- Reused `Activity.Current` and .NET 10's built-in `Microsoft.AspNetCore.Hosting`/
  `http.server.request.duration` instrumentation. No duplicate ActivitySource, counter, histogram or
  telemetry dependency was added.
- Configured default console output as non-indented UTC JSON and explicitly disabled ambient scopes
  because ASP.NET request scopes contain the raw request path.
- Added deterministic log, Activity, framework-metric, raw-path/query privacy, unknown-method and
  escaping-exception tests. The metric assertions prove normalized method/route/status dimensions
  and no correlation, raw path or query values.

## Verification

| Check | Result |
|---|---|
| Focused final observability tests | PASS - 3/3 in 7 seconds |
| Complete final-source Release API suite | PASS - 91/91 in 4 minutes 39 seconds |
| Final Release API/test graph | PASS - zero warnings and zero errors |
| Focused API and test formatting | PASS - no formatter changes |
| Final architecture suite | PASS - 23/23 after the fallback-label correction |
| Compose validation and health | PASS - six configured services healthy |
| Supplemental updated-binary JSON smoke | PASS - matched 200 and unmatched 404 emitted parseable UTC JSON Event 12003 records |
| Tracked/non-ignored source secret scan | PASS - zero findings |
| Diff/artifact hygiene | PASS - no whitespace error, staged file or tracked `.artifacts` output |

The updated-binary smoke sent unique raw path and query canaries. Neither appeared in the two Event
12003 JSON records; the unmatched request recorded only `GET`, `unmatched`, 404, duration,
correlation and trace identifiers.

## Findings closed during verification

1. The first focused run passed the telemetry behavior but a configuration assertion compared
   lowercase text with the typed Boolean value. More importantly, its visible JSON output showed
   that `IncludeScopes=true` serialized an ASP.NET `RequestPath` scope containing the raw canary.
   Scopes were set false, the assertion was made typed, and the full privacy case passed. No path was
   allow-listed or redacted after capture.
2. The first architecture run failed 22/23 because technical fallback `OTHER` collided with an
   existing governed business code. The fallback changed to the framework convention `_OTHER`; the
   final architecture suite passed 23/23 without weakening the governed-code check.

## Boundary retained

This packet proves local API instrumentation and the privacy shape of Event 12003 plus the observed
built-in metric tags only. It does not certify every existing application log, exception record,
framework Activity tag or baggage value. JSON stdout is not a central sink, and the repository still
has no global redaction/export filtering, collector/exporter, production sampling policy, retention
and access controls, dashboard, alert routing, SLO/error-budget calculation, synthetic
authentication, business/queue/provider metrics or named on-call owner. Those production operations
decisions and independent review remain blocked. No commit, push or deployment was performed.
