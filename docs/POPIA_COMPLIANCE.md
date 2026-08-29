# POPIA engineering control register

**Status:** DRAFT — NOT LEGAL APPROVAL  
**Named Information Officer:** UNASSIGNED  
**Legal reviewer:** UNASSIGNED  
**Implementation state:** No production personal-information processing is approved

This document is an engineering checklist. It does not certify compliance, replace legal advice, register an Information Officer, or authorise processing.

## Controlling position

Advertified must implement privacy by design, purpose limitation, data minimisation, access control, retention, correction/deletion workflows, processor governance, incident handling, and evidence. The responsible party remains accountable for determining the lawful basis and purpose for each processing activity.

The South African Information Regulator's [official POPIA guidance](https://inforegulator.org.za/popia/) is the primary public regulatory source. Its breach-notification guidance is framed around notifying the Regulator and affected data subjects as soon as reasonably possible after discovery. This repository must not invent a universal fixed 72-hour POPIA deadline. Counsel must determine obligations for each incident and any overlapping law or contract.

## Decisions that remain blocked

| Decision | Required owner/evidence |
|---|---|
| Responsible party/operator allocation | Named legal/privacy owner and contracts |
| Information Officer registration and duties | Named person and registration evidence |
| Lawful basis per processing purpose | Legal review and processing register |
| Special personal information | Explicit scope decision and controls |
| Children's data | Explicit prohibition or approved lawful process |
| Cross-border transfers | Destination, safeguards, contracts, legal approval |
| Direct marketing | Channel-specific consent/opt-out decision |
| Retention periods | Purpose/legal schedule and deletion proof |
| Data-subject identity verification | Security/privacy design |
| Incident notification decision tree | Counsel-approved runbook and contacts |

Until resolved, the system must minimise collection, avoid special/children's data, keep provider access disabled, and fail closed on an affected workflow.

## Required engineering controls

| Control | Minimum evidence |
|---|---|
| Data inventory | Field, source, purpose, lawful basis, owner, recipients, location, retention |
| Tenant isolation | Negative read/write/object/job/tool tests |
| Least privilege | Server-authoritative roles, assignments, approvals, audit |
| Encryption | Approved in-transit/at-rest configuration and key ownership |
| Secret handling | Managed secret store; no browser/image/log/source secret |
| Evidence provenance | Immutable source, locator, hash, review and permitted purpose |
| Consent/objection | Versioned record where required; withdrawal/opt-out propagation |
| Access/correction | Authenticated request, search/export/correction workflow, audit |
| Deletion/restriction | Legal-hold-aware workflow, downstream propagation, proof |
| Retention | Automated policy, exception/hold, deletion verification |
| Processor governance | Contract, subprocessor inventory, purpose limits, incident duties |
| Logging | No unnecessary personal data; access monitoring and retention |
| Incident response | Containment, scope, evidence, legal assessment, notification decision |
| Non-production data | Synthetic or irreversibly de-identified by default |
| AI use | Approved evidence only; provider policy; no hidden memory or training reuse |
| Upload safety | Quarantine, type/malware checks, access isolation, deletion policy |

## Incident rule

Engineering must immediately contain and preserve evidence, identify affected tenants/data/recipients, and escalate to the named security and privacy owners. Only authorised humans decide notification content, recipients, timing, and regulatory filings. An AI may summarise evidence but may not make the legal decision or send a notification.

## Gate closure evidence

No privacy-related gate may close without:

1. named accountable people;
2. approved processing and retention registers;
3. processor/subprocessor decisions;
4. tested data-subject and incident workflows;
5. tenant and access negative tests;
6. backup/restore/deletion interaction evidence;
7. legal/privacy sign-off referencing the exact release.

Missing evidence remains NO-GO.
