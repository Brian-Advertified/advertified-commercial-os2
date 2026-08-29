# ADR-0008: Browser validation, notifications and human-facing errors

## Status

Accepted for local non-production implementation — Brian Rabuthu, 2026-08-29. Remote publication, production and deployment remain prohibited.

## Context

Advertified pages must be simple to operate while retaining the technical and commercial depth needed for planning. The owner requires Zod validation, Toastr notifications, useful graphs/charts/animations/icons and no exposure of internal messages.

The legacy `toastr` npm package is jQuery-dependent and has not been published since 2017. Introducing jQuery into the clean React 19 application would add avoidable legacy coupling.

## Decision owner and reviewers

| Responsibility | Actual name | Decision/date |
|---|---|---|
| Accountable owner | Brian Rabuthu | Accepted, 2026-08-29 |
| Product/UX reviewer | Brian Rabuthu | Accepted, 2026-08-29 |
| Engineering reviewer | Not required for local-only implementation | Independent review before publication |
| Accessibility/security reviewer | Not required for local-only implementation | Independent review before publication/production |

Brian Rabuthu is the sole required reviewer for this reversible local-only decision. Independent reviews remain mandatory before publication, production or deployment.

## Proposed decision

- Zod 4 is the browser runtime validator.
- Every untrusted browser boundary is parsed before use: forms, route parameters, query strings, browser storage and API responses.
- The C# generated OpenAPI contract remains canonical. Zod schemas must correspond to versioned public contracts and cannot create a second business model.
- One `NotificationService` owns success, information, warning and failure notifications. Components never import the toast package directly.
- The Toastr experience is implemented through a maintained React-native adapter, React-Toastify, rather than the legacy jQuery `toastr` package.
- Toasts acknowledge an outcome; they do not replace inline field validation, blocking dialogs, persistent task states or audit evidence.
- The Commercial API returns `application/problem+json` with stable domain code, safe title/detail, correlation ID and safe field errors.
- Internal exceptions, SQL/provider messages, stack traces, prompts, private reasoning, job internals and raw lifecycle codes are logged server-side and never rendered.
- The web maps stable codes to plain human content. An unknown failure uses a neutral recovery message and may show only a support reference.
- Pages lead with the user task and next action. Technical detail is available through progressive disclosure, comparison panels and dedicated detail pages.
- Charts, graphs, maps, icons and animation must explain a real comparison, trend, status, geography or workflow. They use persisted data, never fabricated progress.
- Motion is subtle and respects reduced-motion preferences. Visuals remain accessible without color alone.

## Consequences

This adds Zod and one notification adapter when their first real boundary is implemented. It prevents direct dependency spread and makes replacement reversible. Chart and icon libraries are selected only when the first real Gate 3 screen proves the need.

## Verification

Only behavior-bearing checks are required:

- representative malformed form, route, storage and API payloads fail safely;
- stable API codes map to approved human wording;
- an unknown internal failure cannot expose raw server content;
- components cannot import the notification adapter outside its infrastructure module;
- one accessibility journey proves toast announcement and reduced-motion behavior.

Do not test Zod, React-Toastify or browser framework internals.

## References

- Normative sections 21.1, 24 and 25.4
- [Zod documentation](https://zod.dev/)
- [React-Toastify documentation](https://fkhadra.github.io/react-toastify/introduction)
- [ASP.NET Core API error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api)
- [Legacy toastr package constraints](https://www.npmjs.com/package/toastr)
- [Omni Solutions visual reference](https://www.omc.com/omni/)

## Decision record

- Proposed by: implementation agent for Brian Rabuthu
- Proposed date: 2026-08-29
- Accepted/rejected by: Brian Rabuthu
- Decision date: 2026-08-29
- Supersedes/superseded by: none
