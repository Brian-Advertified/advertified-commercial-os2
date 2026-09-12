"""Immutable field vocabulary for Advertified production certification."""

from __future__ import annotations

import re
from pathlib import Path

SCHEMA_VERSION = "advertified.production-certification.v2"
REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_MASTER_DATA = REPO_ROOT / "shared" / "contracts" / "master-data.json"
OOH_JOURNEY = "OOH_ONLY"
JOURNEY_COUNTS = {
    OOH_JOURNEY: 10,
    "FULL_CAMPAIGN": 10,
    "UNBRIEFED_OPPORTUNITY": 10,
}
LIFECYCLE_CHECKS = {
    OOH_JOURNEY: (
        "brief_capture_approval",
        "immutable_campaign_mode",
        "geography_routes_pois",
        "inventory_eligibility",
        "scored_shortlist",
        "supplier_response",
        "rate_revision",
        "agency_selection_approval",
        "proposal_delivery",
        "booking_fulfilment_reconciliation",
    ),
    "FULL_CAMPAIGN": (
        "complete_approved_brief",
        "evidence_backed_strategy",
        "cross_channel_allocation",
        "supply_per_plan_line",
        "channel_periods_pricing",
        "human_approvals",
        "commercial_totals",
        "proposal_delivery",
        "booking_fulfilment",
        "proof_invoice_reconciliation",
    ),
    "UNBRIEFED_OPPORTUNITY": (
        "creation_without_brief",
        "research_evidence_growth_case",
        "critic_resolution",
        "approved_strategy_plan",
        "hypotheses_labelled",
        "recommendations_not_supply",
        "proposal_approval_delivery",
        "booking_audit_history",
    ),
}
CERTIFICATION_CHECKS = (
    "fresh_database_migration",
    "existing_database_upgrade",
    "backend_test_suite",
    "agent_runtime_suite",
    "frontend_production_build",
    "browser_end_to_end_suite",
    "security_tenant_isolation",
    "recovery_idempotency",
    "performance_production_inventory",
    "inventory_seed_rehearsal",
    "payment_integrations",
    "production_infrastructure_encryption",
    "dns_tls_secure_headers",
    "durable_object_storage",
    "production_email_delivery",
    "maps_geocoding",
    "observability_alerts",
    "deployment_rollback_rehearsal",
    "backup_restore",
    "production_smoke",
)
UNIQUE_SCENARIO_FIELDS = {
    "scenarioId": "scenarioId",
    "pdfPath": "proposalPdf.path",
    "pdfHash": "proposalPdf.sha256",
}
GOVERNANCE_SIGNOFFS = (
    "legal",
    "privacy",
    "asset_rights",
    "finance",
    "operations",
    "security",
)
HEX_64 = re.compile(r"^[0-9a-f]{64}$")
COMMIT_ID = re.compile(r"^[0-9a-f]{40}(?:[0-9a-f]{24})?$")
