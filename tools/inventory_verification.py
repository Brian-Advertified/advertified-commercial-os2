"""Retired heuristic inventory verifier.

The previous implementation estimated commercial entries from digit-bearing
lines and treated basic document reads as application extraction. Those
estimates are not certification evidence and conflict with the required
source-led ledger plus canonical application projection. The retained
evidence tools are ``reconcile_and_report_inventory.py`` and the local,
paused-state-only API projection verifier.
"""


def main() -> None:
    raise SystemExit(
        "Heuristic verification is retired; use the source-led ledger and "
        "canonical local projection verifier."
    )


if __name__ == "__main__":
    main()
