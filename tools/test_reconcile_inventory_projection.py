"""Focused regression tests for independent-to-projection matching."""

from __future__ import annotations

import unittest
import tracemalloc
from time import perf_counter

from tools.reconcile_inventory_projection import (
    candidate_view,
    match_entries,
    match_score,
    rate_variant_candidates,
    reference_unit,
    reference_view,
    source_accounting,
)


class ReconciliationMatchTests(unittest.TestCase):
    def test_same_page_and_price_without_identity_does_not_match(self) -> None:
        reference = self.reference("BS-113", "R20 000")
        candidate = self.candidate("Escalator advertising", 2_000_000)

        score = match_score(reference_view(reference), candidate_view(candidate))

        self.assertEqual(0, score)

    def test_same_page_price_and_identity_matches(self) -> None:
        reference = self.reference("BS-113", "R20 000")
        candidate = self.candidate("BS-113 Rosebank screen", 2_000_000)

        score = match_score(reference_view(reference), candidate_view(candidate))

        self.assertGreaterEqual(score, 2)

    def test_same_page_and_site_locator_matches_for_field_audit(self) -> None:
        reference = self.reference("BS-113", "R20 000")
        candidate = self.candidate("Unreadable source image", 2_000_000)
        candidate["sourceLocator"] = "docling:page=7;site-card=1"

        score = match_score(reference_view(reference), candidate_view(candidate))

        self.assertGreaterEqual(score, 4)

    def test_rate_variants_are_distinct_reconciliation_units(self) -> None:
        candidate = self.candidate("Spot", 10000)
        candidate["values"]["rateVariants"] = [
            {"amountMinor": 10000, "sourceLocator": "cell:1"},
            {"amountMinor": 20000, "sourceLocator": "cell:2"},
        ]

        units = rate_variant_candidates(candidate)

        self.assertEqual(2, len(units))
        self.assertEqual(["cell:1", "cell:2"], [item["sourceLocator"] for item in units])

    def test_reference_units_do_not_count_packages_as_products(self) -> None:
        reference = self.reference("Launch package", "R20 000")
        reference["expected_fields"]["relationship"] = "contains two products"

        self.assertEqual("package", reference_unit(reference))

    def test_legacy_outputs_report_unobservable_loss_funnel(self) -> None:
        self.assertEqual(
            "NOT_OBSERVABLE_IN_PROJECTION_ARTIFACT",
            source_accounting([{"response": {}}])["status"],
        )

    def test_five_thousand_unit_reconciliation_is_bounded(self) -> None:
        references = [self.reference(f"SKU{index:05d}", "R100")
                      for index in range(5_000)]
        candidates = [self.candidate(f"SKU{index:05d}", 10_000)
                      for index in range(5_000)]
        tracemalloc.start()
        started = perf_counter()

        matches = match_entries(references, candidates)
        elapsed = perf_counter() - started
        _, peak = tracemalloc.get_traced_memory()
        tracemalloc.stop()

        self.assertEqual(5_000, len(matches))
        self.assertLess(elapsed, 10)
        self.assertLess(peak, 256 * 1024 * 1024)

    @staticmethod
    def reference(identity: str, rate: str) -> dict:
        return {
            "identity": identity,
            "locator": "source-map:page=7;site=1",
            "expected_fields": {"source_rates": [rate]},
        }

    @staticmethod
    def candidate(name: str, rate_minor: int) -> dict:
        return {
            "sourceLocator": "docling:page=7;text=42;line=1",
            "values": {"name": name, "rateAmountMinor": rate_minor},
            "evidence": [],
        }


if __name__ == "__main__":
    unittest.main()
