# Advertified Unified - Capability Ledger

This ledger tracks the implementation status of every capability specified in the Advertified Unified Production Build Specification v1.1.

## Status Legend

- **ABSENT**: Not yet implemented
- **SCAFFOLDED**: Basic structure exists but lacks functionality
- **IMPLEMENTED**: Code exists but not verified
- **VERIFIED**: Tested and meets acceptance criteria
- **BLOCKED**: Cannot proceed due to external dependency

## Gate 0: Repository Baseline

| Capability | Status | Evidence | Notes |
|------------|--------|----------|-------|
| Project structure created | IMPLEMENTED | Directory structure exists | web, api, agent-runtime, workers, shared, infrastructure, tests, docs |
| Setup instructions documented | IMPLEMENTED | README.md created | Basic setup guide |
| Capability ledger created | IMPLEMENTED | This file | Initial version |
| Logical repository map defined | IMPLEMENTED | IMPLEMENTATION_PLAN.md | Full implementation plan |
| Toolchain pins established | IMPLEMENTED | Projects created | .NET 8.0, React 19.2.0, Python 3.12 |
| Legacy disposition recorded | IMPLEMENTED | Documentation pending | No legacy yet - clean build |
| Docker environment | IMPLEMENTED | docker-compose.yml | PostgreSQL, MinIO, Redis, Mailhog |
| Environment configuration | IMPLEMENTED | env.example and .env | Full configuration template |
| ADR template created | IMPLEMENTED | docs/adr/0000-adr-template.md | Architecture decision process |
| Common schemas | IMPLEMENTED | shared/schemas/common.json | Base data contracts |
| Setup guide | IMPLEMENTED | docs/SETUP_GUIDE.md | Developer onboarding |

## Gate 1: Architecture Guardrails

| Capability | Status | Evidence | Notes |
|------------|--------|----------|-------|
| 400-line CI rule | IMPLEMENTED | GitHub Actions CI workflow | File size checks in CI |
| Architecture analyzers | IMPLEMENTED | ESLint, .NET analyzers configured | Linting rules established |
| Master data registry | IMPLEMENTED | shared/contracts/master-data.json | Canonical master data defined |
| ADR template | IMPLEMENTED | docs/adr/0000-adr-template.md | Template and first ADR created |
| Generated contracts | ABSENT | OpenAPI pending | Need API first |
| Dependency rules | IMPLEMENTED | CI dependency scanning | Security scanning configured |
| Architecture boundary tests | IMPLEMENTED | tests/architecture/boundary-tests.py | Enforces separation rules |
| Technology stack ADR | IMPLEMENTED | docs/adr/0001-use-amazon-bedrock-agentcore.md | AgentCore decision recorded |
| ESLint configuration | IMPLEMENTED | .eslintrc.json | Web application linting |
| .NET configuration | IMPLEMENTED | Updated csproj file | Code quality rules enabled |

## Gate 2: Canonical Foundation

| Capability | Status | Evidence | Notes |
|------------|--------|----------|-------|
| Tenant domain model | ABSENT | C# domain pending | Core entity |
| User domain model | ABSENT | C# domain pending | Core entity |
| Membership domain model | ABSENT | C# domain pending | Core entity |
| Value objects | ABSENT | C# classes pending | Money, VAT, etc |
| Audit system | ABSENT | C# implementation pending | Append-only events |
| Idempotency system | ABSENT | C# implementation pending | Key-based deduplication |
| Outbox pattern | ABSENT | C# implementation pending | Event publishing |
| Database migrations | ABSENT | EF Core pending | Schema management |
| OpenAPI specification | ABSENT | Swagger pending | API contracts |

## Gate 3: Authenticated Shell

| Capability | Status | Evidence | Notes |
|------------|--------|----------|-------|
| Sign-in functionality | ABSENT | React components pending | OIDC integration |
| Invite system | ABSENT | React/API pending | Token-based invites |
| Workspace selection | ABSENT | React components pending | Tenant context |
| Role dashboard | ABSENT | React components pending | KPI displays |
| Route guards | ABSENT | React Router pending | Permission checks |
| Error states | ABSENT | React components pending | UX patterns |
| Accessibility states | ABSENT | React components pending | WCAG 2.2 AA |

## Gate 4: Evidence and Opportunity

| Capability | Status | Evidence | Notes |
|------------|--------|----------|-------|
| Evidence capture | ABSENT | API/React pending | File upload |
| Crawl policy | ABSENT | Python worker pending | Web scraping |
| Evidence review | ABSENT | React/API pending | Human approval |
| Business interpretation | ABSENT | Agent pending | Business model analysis |
| Opportunity angles | ABSENT | Agent pending | Opportunity generation |
| Strategy generation | ABSENT | Agent pending | Growth strategy |
| Critic agent | ABSENT | Agent pending | Quality control |

## Gate 5: Canonical Brief

| Capability | Status | Evidence | Notes |
|------------|--------|----------|-------|
| CampaignBrief aggregate | ABSENT | C# domain pending | Core brief entity |
| BriefVersion immutability | ABSENT | C# implementation pending | Version control |
| Brief comparison | ABSENT | React components pending | Diff display |
| Unknowns tracking | ABSENT | C# domain pending | Knowledge gaps |
| Brief approval workflow | ABSENT | API/React pending | Human gates |
| Stale downstream logic | ABSENT | C# implementation pending | Dependency tracking |

## Gate 6: Inventory Truth

| Capability | Status | Evidence | Notes |
|------------|--------|----------|-------|
| Import pipeline | ABSENT | Python worker pending | File processing |
| Document classification | ABSENT | Python implementation pending | Type detection |
| Structure extraction | ABSENT | Docling integration pending | Table parsing |
| Asset extraction | ABSENT | Python implementation pending | Logo/image handling |
| Evidence linking | ABSENT | C# implementation pending | Field lineage |
| Human review interface | ABSENT | React components pending | Bulk operations |
| Channel schemas | ABSENT | C# domain pending | Multi-channel support |
| Catalog search | ABSENT | API/React pending | Large-scale queries |
| Detail pages | ABSENT | React components pending | Product display |
| Supplier ownership | ABSENT | C# implementation pending | Access control |

## Gate 7: Planning

| Capability | Status | Evidence | Notes |
|------------|--------|----------|-------|
| Audience agent | ABSENT | Agent pending | Segment analysis |
| Media mix agent | ABSENT | Agent pending | Budget allocation |
| Eligibility engine | ABSENT | C# implementation pending | Hard constraints |
| Shortlist generation | ABSENT | Agent pending | Inventory selection |
| Benchmark calculations | ABSENT | C# implementation pending | Rate comparison |
| Supply forecast | ABSENT | API integration pending | Availability |
| MediaPlan approval | ABSENT | API/React pending | Human gates |
| Stale input handling | ABSENT | C# implementation pending | Plan invalidation |

## Gate 8: Proposal

| Capability | Status | Evidence | Notes |
|------------|--------|----------|-------|
| Tier configuration | ABSENT | C# domain pending | Multi-option proposals |
| Proposal narrative agent | ABSENT | Agent pending | Client communication |
| Proposal critic | ABSENT | Agent pending | Quality control |
| Total calculations | ABSENT | C# implementation pending | Financial reconciliation |
| Proposal approval | ABSENT | API/React pending | Human gates |
| Branded document generation | ABSENT | Python worker pending | DOCX/PDF rendering |
| Proposal sending | ABSENT | API/worker pending | External delivery |

## Gate 9: Rapid OOH

| Capability | Status | Evidence | Notes |
|------------|--------|----------|-------|
| Automatic path detection | ABSENT | C# implementation pending | Brief analysis |
| Geography resolution | ABSENT | Maps integration pending | Location services |
| Route/POI handling | ABSENT | Maps integration pending | Waypoint management |
| OOH eligibility | ABSENT | C# implementation pending | Format constraints |
| Supplier confirmation | ABSENT | API integration pending | Availability check |
| Recalculation logic | ABSENT | C# implementation pending | Dynamic updates |

## Gate 10: Supplier Marketplace

| Capability | Status | Evidence | Notes |
|------------|--------|----------|-------|
| Supplier user management | ABSENT | API/React pending | Self-service |
| Listing creation | ABSENT | API/React pending | Inventory management |
| Freshness tracking | ABSENT | C# implementation pending | Rate monitoring |
| RFQ system | ABSENT | API/React pending | Request for quote |
| Supplier responses | ABSENT | API/React pending | Quote management |
| Booking workflow | ABSENT | API/React pending | Commitment process |
| Commercial settings | ABSENT | C# domain pending | Supplier configuration |

## Gate 11: Campaign Delivery

| Capability | Status | Evidence | Notes |
|------------|--------|----------|-------|
| Creative workflow | ABSENT | API/React pending | Asset management |
| Booking management | ABSENT | C# implementation pending | Commitment tracking |
| Proof submission | ABSENT | API/React pending | Delivery evidence |
| Performance facts | ABSENT | API integration pending | Measurement data |
| Measurement interpretation | ABSENT | Agent pending | Analytics |
| Client reporting | ABSENT | React components pending | Outcome display |

## Gate 12: Hardening

| Capability | Status | Evidence | Notes |
|------------|--------|----------|-------|
| Recovery mechanisms | ABSENT | Implementation pending | Checkpoint system |
| Security controls | ABSENT | Implementation pending | Authentication/authorization |
| POPIA compliance | ABSENT | Implementation pending | Privacy controls |
| Performance optimization | ABSENT | Implementation pending | SLO compliance |
| Observability | ABSENT | Implementation pending | Monitoring/alerting |
| Backup/restore | ABSENT | Implementation pending | Data protection |
| Runbooks | ABSENT | Documentation pending | Operational procedures |

## Gate 13: Production Launch

| Capability | Status | Evidence | Notes |
|------------|--------|----------|-------|
| 30-case certification | ABSENT | Test execution pending | Quality validation |
| Zero-Bedrock certification | ABSENT | Test execution pending | Deterministic provider |
| Unanimous greenlight | ABSENT | Sign-off pending | Stakeholder approval |
| Production deployment | ABSENT | Infrastructure pending | AWS setup |
| Monitoring setup | ABSENT | Infrastructure pending | Telemetry |
| Handover complete | ABSENT | Documentation pending | Operations transfer |

## Agent Implementation Status

| Agent | Status | Evidence | Notes |
|-------|--------|----------|-------|
| Opportunity Intelligence | ABSENT | Agent code pending | Bedrock integration |
| Business Interpretation | ABSENT | Agent code pending | Evidence analysis |
| Strategy | ABSENT | Agent code pending | Growth planning |
| Brief Drafting | ABSENT | Agent code pending | Brief generation |
| Audience | ABSENT | Agent code pending | Segment analysis |
| Inventory Intelligence | ABSENT | Agent code pending | Supply evaluation |
| Media Planning | ABSENT | Agent code pending | Mix optimization |
| Critic & Readiness | ABSENT | Agent code pending | Quality control |
| Proposal Narrative | ABSENT | Agent code pending | Client communication |
| Creative | ABSENT | Agent code pending | Concept generation |
| Measurement | ABSENT | Agent code pending | Performance analysis |

## Integration Status

| Integration | Status | Evidence | Notes |
|-------------|--------|----------|-------|
| AWS Bedrock | ABSENT | Configuration pending | AI provider |
| OIDC Provider | ABSENT | Configuration pending | Authentication |
| S3 Storage | ABSENT | Configuration pending | File storage |
| Docling | ABSENT | Installation pending | Document extraction |
| Resend Email | ABSENT | Configuration pending | Notifications |
| Maps Provider | ABSENT | Configuration pending | Geocoding |
| Payment Provider | ABSENT | Configuration pending | VodaPay/EFT |

## Summary Statistics

- **Total Capabilities**: 118
- **Implemented**: 22 (19%)
- **Verified**: 0 (0%)
- **Blocked**: 0 (0%)
- **Pending**: 96 (81%)

## Last Updated

2026-08-29 - Gate 1 completed, architecture guardrails established