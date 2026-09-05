"""Retired source-rewriting entry point; retained to explain old operator references."""

def main() -> int:
    raise RuntimeError(
        "Retired: runtime tooling must not rewrite C# publication contracts. "
        "Missing-price policy and schema changes require the governed source and EF migration."
    )

if __name__ == "__main__":
    raise SystemExit(main())
