# 26. External integration contracts

**Adapter rule:** External providers implement Advertified-owned ports. Provider SDK objects, status names and identifiers remain inside adapters. The domain stores stable Advertified identifiers plus an external reference and raw receipt where required for reconciliation.

| **Integration**       | **Port capabilities**                                                                                                               | **Authentication**                                                                               | **Resilience and boundary**                                                                |
|-----------------------|-------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------|
| Identity/OIDC         | Authenticate, logout, invite acceptance, session refresh, MFA claim                                                                 | Issuer/audience/signing keys from managed config; secure cookie                                  | Fail closed; session expiry is recoverable                                                 |
| AWS Bedrock           | Structured completion, cancellation where supported, usage and provider request ID                                                  | IAM role/managed credentials; allow-listed model IDs                                             | Timeout/circuit breaker; ambiguous acceptance requires reconciliation                      |
| Docling               | Classify/render/extract structure, coordinates and assets                                                                           | Internal service identity; pinned model bundle                                                   | Checkpoint by file hash and parser version; alternate parser only by policy                |
| S3-compatible storage | Put/get intent, immutable object version, hash, metadata, short-lived URL                                                           | IAM least privilege and server-side encryption                                                   | Multipart resume; no public buckets; object access audited                                 |
| Resend                | Send templated transactional email and receive delivery events                                                                      | Managed API key; verified advertified.com sender                                                 | Idempotency; bounce/suppression handling; no blind resend                                  |
| Maps/geography        | Geocode, reverse geocode, routes, POIs and map tiles                                                                                | Provider key restricted by origin/service                                                        | Cache lawful results; surface uncertainty; manual correction                               |
| Payment/funding       | VodaPay intent, manual EFT reconciliation and manual Advertise Now Pay Later route; receive status and reconcile external reference | Provider-specific managed secret and signed callbacks; manual routes require authorised evidence | Accepted proposal plus signed PO before invoice; Advertified does not imply funds are held |
| Supplier systems      | Catalogue/rate/availability import, RFQ and booking status where available                                                          | Per-supplier credentials and scopes                                                              | Canonical inventory review still applies; no trusted bulk overwrite                        |
| Measurement           | Import delivery and performance facts with methodology metadata                                                                     | Provider or user-supplied access                                                                 | Quality status and limitations required before interpretation                              |

## 26.1 Integration implementation checklist

- Define a provider-neutral interface, typed request/response, timeout, retry classification, idempotency strategy, health signal and test double before adding the vendor SDK.

- Keep credentials in managed secrets or local uncommitted environment configuration. Validate their presence and scope without printing values.

- Record external request ID, Advertified correlation ID, start/end time, outcome and cost where applicable. Redact payload fields according to privacy policy.

- Provide contract tests against a deterministic fake and a guarded sandbox test. Production certification requires a deliberate canary with named approval.

- Feature flags control provider availability, not domain correctness. When a provider is unavailable, preserve canonical state and show a business-safe recovery path.

## 26.2 Webhook and callback contract

| **Concern**    | **Required behaviour**                                                                                    |
|----------------|-----------------------------------------------------------------------------------------------------------|
| Verification   | Verify signature, timestamp and allowed clock skew before parsing business content                        |
| Idempotency    | Use provider event ID plus provider code; duplicate delivery returns success without duplicate effect     |
| Ordering       | Do not assume order; compare provider occurrence time and current canonical state                         |
| Persistence    | Store minimal immutable receipt and enqueue processing before returning within provider timeout           |
| Authorisation  | Map external reference to one tenant/resource; reject ambiguous or cross-tenant association               |
| Errors         | Return provider-appropriate retry response only for safe transient failures; dead-letter invalid receipts |
| Reconciliation | Scheduled job compares unresolved intents with provider state and creates human task for ambiguity        |

# 27. Production, security, privacy and operations

## 27.1 Environments and deployment topology

| **Environment** | **Profile**                                                                                                 | **Hard boundary**                                                                   |
|-----------------|-------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------|
| Local           | Docker Compose, deterministic providers, synthetic data, local object storage                               | No production secrets or production network access                                  |
| CI              | Ephemeral database/services, deterministic tests, builds, scans and generated-contract checks               | No paid provider by default                                                         |
| Staging         | Production-like containers and managed dependencies, synthetic or approved masked data                      | Deterministic/sandbox providers; live Bedrock remains disabled during certification |
| Production      | AWS af-south-1, TLS ingress, web/API/runtime/workers, managed PostgreSQL, S3, managed secrets and telemetry | Least privilege, backups, alerting and change approval                              |

| **Component**         | **Production requirement**                                                                                                                       |
|-----------------------|--------------------------------------------------------------------------------------------------------------------------------------------------|
| Edge                  | CloudFront plus ALB, DNS/TLS, request size/rate controls and security headers                                                                    |
| Web                   | React/Vite immutable static build served through CloudFront with environment-safe API origin and no embedded secrets                             |
| Commercial API        | C#/.NET container on ECS/Fargate; restartable instances, separated health/readiness and only canonical write boundary                            |
| Agent runtime/workers | Python/FastAPI AgentCore-compatible runtime and owned workers on ECS/Fargate with bounded concurrency, checkpoints and provider circuit breakers |
| Events/workflows      | Commercial outbox -\> EventBridge/SQS; Step Functions only for approved coarse-grained durable workflow coordination                             |
| PostgreSQL            | Managed production service with encryption, point-in-time recovery, restricted network and migration role                                        |
| Object storage        | Versioning, encryption, lifecycle policy, private access and malware quarantine prefix                                                           |
| Secrets               | Managed store, per-service IAM, rotation procedure and no secret in image/log/browser                                                            |
| Telemetry             | Central structured logs, metrics, traces, dashboards and alerts correlated to business action                                                    |

## 27.2 Security controls

| **Area**      | **Required control**                                                                                                       |
|---------------|----------------------------------------------------------------------------------------------------------------------------|
| Identity      | OIDC, secure session, MFA capability for privileged roles, account lock/revocation and audited invitation                  |
| Authorisation | API resource checks, tenant predicates, assignment and approval separation; deny by default                                |
| Network       | TLS, private database/storage paths, restricted security groups and no public admin endpoints                              |
| Application   | Input/schema validation, parameterised queries, output encoding, CSRF for cookies, CORS allow-list and secure headers      |
| Files         | Size/type allow-list, malware scan, quarantine, safe names, no execution and permissioned download                         |
| Secrets       | Managed injection, rotation, secret scanning and redaction; no credential in prompts or client bundle                      |
| Supply chain  | Pinned dependencies/images, SBOM, vulnerability scan and controlled update policy                                          |
| AI            | Prompt/tool allow-list, tenant-scoped retrieval, evidence policy, output validation, cost cap and provider data-use review |
| Audit         | Append-only consequential actions, approvals, external sends, access exceptions, provider/model and cost                   |
| Abuse         | Rate limits, bounded queries/uploads, job concurrency, invitation controls and anomaly alerting                            |

## 27.3 POPIA and retention defaults

The application must implement these as versioned policy defaults and expose lawful override by data category. Legal/Privacy sign-off is required before production data collection; an override records purpose, authority, owner and effective date.

| **Category**                                          | **Default retention**                                                         | **Purpose**                                 |
|-------------------------------------------------------|-------------------------------------------------------------------------------|---------------------------------------------|
| Commercial records, proposals, bookings and approvals | 5 years after relationship/campaign end                                       | Legal/commercial record and dispute defence |
| Approved evidence snapshots used in decisions         | 5 years after related commercial record closes                                | Decision lineage                            |
| Raw permitted crawl not promoted to approved evidence | 180 days                                                                      | Reprocessing and challenge window           |
| Rejected/unpublished extraction candidates            | 90 days                                                                       | Quality evaluation and correction           |
| AI request/response content                           | 90 days unless attached to an approved artefact; then evidence policy applies | Debug/evaluation with minimisation          |
| AI usage/cost metadata                                | 5 years                                                                       | Financial and operational audit             |
| Security/access logs                                  | 12 months                                                                     | Security investigation                      |
| Inactive contact/profile data                         | 24 months after relationship end unless law/consent requires otherwise        | Purpose limitation                          |
| Backups                                               | 35-day rolling retention; expiry follows tested disposal                      | Recovery                                    |

- Record purpose, legal basis/authority, source, recipients, storage location, retention and owner in a processing register.

- Support access/export, correction, objection/restriction where applicable and approved deletion/anonymisation without corrupting financial/audit obligations.

- Collect the minimum contact, profile, audience and device data required. Do not build a consumer identity graph as a prerequisite.

- Provider and cross-border data processing requires contract and transfer review before enabling production traffic.

## 27.4 SLO, monitoring and recovery

| **Objective**       | **Target**                                                                             | **Measurement**                                       |
|---------------------|----------------------------------------------------------------------------------------|-------------------------------------------------------|
| Availability        | 99.5% monthly for authenticated web/API excluding announced maintenance                | Synthetic login/API checks and error-budget dashboard |
| API latency         | p95 standard authenticated reads \<2s; non-AI commands \<3s                            | Route metrics excluding asynchronous work             |
| Job pickup          | p95 queued durable job starts \<60s under normal capacity                              | Queue age and worker saturation                       |
| User feedback       | Long work returns run ID immediately and visible progress within 2s                    | Playwright and trace assertion                        |
| Recovery            | RPO 15 minutes; RTO 4 hours                                                            | Point-in-time recovery and quarterly restore exercise |
| Notifications       | 95% of accepted transactional messages handed to provider within 2 minutes             | Outbox age and provider receipt                       |
| Inventory freshness | Policy-compliant active rate and availability coverage visible daily                   | Scheduled freshness dashboard                         |
| Cost                | Every live provider attempt has ledger entry; no workflow exceeds account cap silently | Usage reconciliation alert                            |

| **Runbook**                 | **Minimum response**                                                                                    |
|-----------------------------|---------------------------------------------------------------------------------------------------------|
| API unavailable             | Verify edge/health/database, enter degraded mode, restore service, reconcile failed commands            |
| Worker backlog              | Pause new heavy work, inspect poison class, scale safely, resume from checkpoint                        |
| Database failure            | Fail writes closed, activate managed recovery, validate migrations and reconcile outbox                 |
| Provider outage/cost spike  | Open circuit, preserve run state, use approved fallback or human continuation, notify owner             |
| Cross-tenant/security event | Revoke access, preserve evidence, isolate scope, notify security/privacy owner and follow incident plan |
| Bad deployment              | Stop rollout, roll back image/config, preserve forward-only data changes and run compensating procedure |
| Extraction regression       | Stop publish, retain raw imports, roll back parser/prompt version and rerun labelled corpus             |

## 27.5 Deployment procedure

1.  Build immutable images from a clean commit; generate SBOM; run tests, scans, migrations dry-run and contract checks.

2.  Deploy to staging; run migration, smoke, critical Playwright, provider sandbox and rollback verification.

3.  Create production backup/restore point and confirm monitoring, incident owner and change window.

4.  Apply backward-compatible migrations before application rollout. Use expand-migrate-contract for breaking storage changes.

5.  Roll out incrementally, observe SLO/business errors, run production-safe smoke tests and reconcile outbox/jobs.

6.  Record release commit, images, migrations, flags, approver, checks and rollback result. Remove expired flags after stability window.

# 28. Test and production certification specification

## 28.1 Required test layers

| **Layer**            | **Coverage**                                                                                        | **Gate**                                                            |
|----------------------|-----------------------------------------------------------------------------------------------------|---------------------------------------------------------------------|
| Domain unit          | Invariants, value objects, money/VAT, lifecycle and stale-input rules                               | Fast; critical invariants fully covered                             |
| Application          | Commands, permissions, idempotency, audit/outbox and error codes                                    | Real domain with test ports                                         |
| Contract             | OpenAPI, events, tool schemas, agent envelopes and generated clients                                | Backward compatibility and schema fixtures                          |
| Integration          | PostgreSQL/PostGIS/pgvector, object storage, jobs, Docling adapter and renderer                     | Containerised real dependencies                                     |
| Migration            | Empty upgrade, representative upgrade, rollback/compensation and master-data seed                   | Every migration in CI                                               |
| Agent evaluation     | Golden/adversarial corpus, evidence binding, unsupported claims, cost and recovery                  | Deterministic provider; zero live/paid Bedrock during certification |
| Inventory evaluation | Known and held-out files, products, fields, assets, coordinates, precision/recall and publish gates | Per supported document class/channel                                |
| Security             | Tenant negatives, IDOR, injection, file abuse, CSRF/CORS, secrets and dependency scan               | Launch blocking                                                     |
| Accessibility        | Automated WCAG checks plus keyboard and screen-reader review of critical journeys                   | No critical/high issue                                              |
| Playwright           | Role-specific vertical journeys, business copy, responsive states, failure/recovery and PDF preview | Screenshots/traces retained on failure                              |
| Performance          | Catalogue search, dashboard, API commands, worker throughput and document import                    | Meets Section 27 SLO                                                |
| Restore/operations   | Backup restore, worker restart, provider outage, bad deploy and incident runbook                    | Exercise before production                                          |

## 28.2 Mandatory end-to-end journeys

| **ID** | **Journey**            | **Pass condition**                                                                                                                    |
|--------|------------------------|---------------------------------------------------------------------------------------------------------------------------------------|
| E2E-01 | Tenant isolation       | Two tenants with similar IDs; all browser/API/job/tool cross-access attempts denied and audited                                       |
| E2E-02 | Unbriefed opportunity  | Capture permitted sources -\> approve evidence -\> interpretation -\> opportunity angle -\> strategy/critic -\> approved BriefVersion |
| E2E-03 | Full campaign          | Approved brief -\> STP -\> multi-channel media mix -\> shortlist -\> supply/forecast -\> approved plan -\> proposal options -\> branded PDF |
| E2E-04 | OOH-only automation    | Signed email to configured mailbox -\> immutable OOH-only Brief -\> STP -\> OOH/DOOH-only mix -\> eligible benchmarked inventory -\> current supply -\> approved plan -\> branded proposal -\> exactly-once delivery without per-request user input |
| E2E-05 | Unseen inventory file  | Upload held-out file -\> extract structure/assets -\> review -\> publish -\> dedicated detail page -\> searchable inventory           |
| E2E-06 | Supplier self-service  | Supplier creates/updates listing, rate and availability -\> review/publish -\> RFQ -\> response -\> confirmation                      |
| E2E-07 | Proposal send          | Approver reviews exact version/PDF -\> resolves recipient -\> sends once -\> delivery receipt -\> selected tier                       |
| E2E-08 | Campaign delivery      | Booking -\> creative versions -\> approval -\> live -\> proof -\> performance facts -\> client report                                 |
| E2E-09 | Failure and resume     | Interrupt runtime/worker/provider at each checkpoint -\> resume -\> no duplicate state, external action or paid call                  |
| E2E-10 | Stale commercial input | Rate/availability changes after plan -\> plan/proposal marked stale -\> user resolves before approval/send                            |
| E2E-11 | Role dashboards        | Each role sees correct KPIs/tasks and cannot navigate or deep-link outside scope                                                      |
| E2E-12 | Recovery               | Restore backup into isolated environment and prove authentication, canonical counts, outbox and object references                     |

## 28.3 Release gates

| **Gate**         | **Required evidence**                                                                                                                                                               |
|------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Code quality     | Formatting, lint, type check, 400-line guard, complexity, architecture boundaries, no magic registry values and no secret scan finding                                              |
| Build            | Web, Commercial API, runtime, worker, generated clients and document renderer build from clean checkout                                                                             |
| Data             | Migrations and master-data seeds pass empty/upgrade tests; tenant constraints and indexes verified                                                                                  |
| Contracts        | OpenAPI, events, agents/tools and clients are current and backward-compatible or deliberately versioned                                                                             |
| Functional       | Targeted and clean new test suites pass; all mandatory journeys for the release gate pass                                                                                           |
| UX               | Authenticated screens visually reviewed, business wording approved, no dark green, accessibility passes and Playwright evidence captured                                            |
| AI               | Agent evaluation, evidence, critic, provider policy, cost cap and resume/no-duplicate tests pass                                                                                    |
| Inventory        | Precision/recall, held-out file, assets, large catalogue and publish gates pass                                                                                                     |
| Security/privacy | Negative tenant tests, security scan, processing register, retention jobs and Legal/Privacy launch approval                                                                         |
| Operations       | Staging deployment, telemetry, alerts, backup restore, runbooks, rollback and named incident ownership proven                                                                       |
| Proof integrity  | No manual database edits, hidden endpoints, invented supplier/client responses, static mock UI, screenshot-only proof, silent self-repair or cosmetic patch presented as completion |

**Legacy disposition:** Record the legacy full-suite timeout and create a bounded retain/migrate/reference/delete register. The legacy full suite is not the clean release gate. New production certification comes from the tests and journeys defined here.

## 28.4 Production greenlight certification

| **Cohort**          | **Volume**                                           | **Acceptance proof**                                                                                                                                       |
|---------------------|------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------|
| OOH-only            | 10 genuine inbound or supplied briefs                 | Each uses the same STP/planning spine with only OOH/DOOH selected; complete mailbox cases send exactly once without per-request input, while incomplete/non-OOH cases send nothing and enter review |
| Full multi-channel  | 10 genuine briefs                                    | Each reaches an accepted exact immutable proposal option, approved STP/plan and premium branded PDF                                                         |
| Unbriefed discovery | 10 genuine opportunities including Rayetsa Furniture | Approved evidence -\> interpretation -\> opportunity/strategy -\> complete approved BriefVersion -\> plan -\> accepted proposal                            |
| Total               | 30 accepted cases                                    | Not generated demos: real owner/UAT acceptance, canonical audit, inventory/pricing evidence and final artefact retained                                    |

| **Certification area** | **Non-negotiable greenlight condition**                                                                                                  |
|------------------------|------------------------------------------------------------------------------------------------------------------------------------------|
| AI provider            | Zero live or paid Bedrock calls during redevelopment and certification; deterministic surrogate proves equivalent contracts and recovery |
| Quality cycle          | Each material defect follows Agent/implementation -\> QA -\> correction -\> QA retest with retained evidence                             |
| Durability             | Docker/container restart, route change, worker crash and provider timeout tests resume from persisted checkpoints without duplicates     |
| Artefacts              | Every final DOCX/PDF/PPTX is rendered and visually inspected; exact accepted version, input versions and hash retained                   |
| Commercial truth       | Only approved inventory/pricing and client-accepted sites/lines are booked or invoiced; no invented response or silent substitution      |
| Team sign-off          | Named Product, Engineering, QA, Commercial, Inventory Operations, Security/Privacy and Operations reviewers all sign GO                  |
| Decision               | One unresolved NO-GO, missing reviewer, failed gate or unverifiable case means overall NO-GO                                             |

