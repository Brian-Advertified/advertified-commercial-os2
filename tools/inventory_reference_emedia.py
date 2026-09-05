"""Independent visual enumeration of the June 2026 eMedia rate card."""

from __future__ import annotations

from typing import Any, Callable


EMEDIA = "217bebb97b2d136c2875addedaf9f5619613e013c5171f678e8d027ab74b117d"

# Each value is one visibly printed rate cell or package price.  Repeated values
# remain repeated because they are distinct sellable cells in the source.
RATE_GROUPS: tuple[tuple[int, str, str, tuple[int | str, ...]], ...] = (
    (3, "Openview", "monthly packages", (640000, 490000, 430000, 320000, 270000)),
    (4, "Openview", "monthly packages", (1000000, 150000, 180000, 290000, 630000, 50000, 110000)),
    (5, "e.tv", "new packages", (315000, 476400, 50000, 100000, 150000, 200000, 300000, 500000)),
    (7, "e.tv channel 104", "weekday programmes", (
        5000, 8000, 8000, 15000, 18000, 18000, 27000, 48000, 48000,
        48000, 48000, 14000, 27000, 30000, 40000, 40000, 52000, 80000,
        92000, 68000, 93000, 42000, 32000, 20000, 5000,
    )),
    (7, "e.tv channel 104", "Saturday programmes", (
        6000, 18000, 18000, 38000, 20000, 20000, 35000, 30000, 35000,
        35000, 65000, 18000,
    )),
    (7, "e.tv channel 104", "Sunday programmes", (
        6000, 18000, 18000, 20000, 20000, 25000, 22000, 22000, 24000,
        24000, 35000, 40000, 40000, 65000, 20000, 18000,
    )),
    (9, "eNCA channel 403", "weekday programmes", (
        11000, 11000, 11000, 11000, 11000, 8000, 3000, 1000, 500, 500,
    )),
    (9, "eNCA channel 403", "Saturday programmes", (
        7000, 7000, 7000, 7000, 7000, 7000, 7000, 5000, 5000, 5000,
        3000, 2000, 2000, 500, 500,
    )),
    (9, "eNCA channel 403", "Sunday programmes", (
        7000, 7000, 7000, 7000, 7000, 7000, 7000, 7000, 7000, 7000,
        7000, 7000, 2000, 2000, 2000, 500,
    )),
    (9, "eNCA channel 403", "squeeze-back rates", ("9.00", "4.50")),
    (10, "eNCA channel 403", "monthly news packages", (50000, 100000, 160000, 240000)),
    (11, "eMedia", "Daddy-cation contextual AI packages", (50000, 100000)),
    (12, "eExtra channel 105", "weekday programmes", (
        1000, 4000, 4000, 7000, 7000, 14000, 22000, 25000, 22000, 15000,
        12000, 13000, 16000, 18000, 28000, 42000, 35000, 38000, 41000,
        28000, 12000, 4000,
    )),
    (12, "eExtra channel 105", "Saturday programmes", (
        500, 500, 500, 10000, 10000, 6000, 6000, 6000, 6000, 9000,
        11000, 11000, 9000, 9000, 9000,
    )),
    (12, "eExtra channel 105", "Sunday programmes", (
        3000, 4000, 3000, 3000, 3000, 6000, 9000, 12000, 12000, 12000,
        5000, 500,
    )),
    (13, "eMovies channel 106", "weekday time bands", (500, 4000, 5000, 11000, 5000, 3000, 500)),
    (13, "eMovies channel 106", "Saturday time bands", (500, 1000, 5000, 9000, 10000, 17000, 15000, 8000, 500)),
    (13, "eMovies channel 106", "Sunday time bands", (3000, 6000, 8000, 12000, 14000, 20000, 15000, 6000, 500)),
    (15, "eMovies Extra channel 107", "weekday time bands", (500, 3000, 6000, 8000, 12000, 16000, 6000, 8000)),
    (15, "eMovies Extra channel 107", "Saturday time bands", (500, 5000, 8000, 12000, 18000, 22000, 20000, 8000)),
    (15, "eMovies Extra channel 107", "Sunday time bands", (5000, 12000, 14000, 21000, 22000, 18000, 6000, 500)),
    (17, "eReality channel 108", "weekday time bands", (500, 1000, 2000, 5000, 7000, 12000, 8000, 7000, 5000, 3000, 1000)),
    (17, "eReality channel 108", "Saturday time bands", (500, 2000, 3000, 5000, 4000, 3000, 500)),
    (17, "eReality channel 108", "Sunday time bands", (500, 1000, 4000, 3000)),
    (18, "eToonz channel 130", "weekday time band", (5000,)),
    (20, "ePlesier channel 112", "weekday time bands", (1000, 2000, 3000, 4000, 5000, 4000, 3000, 2000, 1000)),
    (20, "ePlesier channel 112", "Saturday time bands", (1000, 2000, 3000, 2000)),
    (20, "ePlesier channel 112", "Sunday time bands", (500, 1000, 2000, 3000, 2000, 500)),
    (22, "eSeries channel 130", "weekday time bands", (500, 1000, 2000, 4000, 5000, 6000, 5000, 8000, 3000, 500)),
    (22, "eSeries channel 130", "weekend time bands", (500, 1000, 2000, 3000, 4000, 3000, 1000)),
    (23, "SA Music", "weekday time bands", (500, 3000, 1000, 500)),
    (23, "SA Music", "weekend time bands", (500, 2000, 4000, 3000, 1000)),
    (23, "SA Music", "Vibes packages", (25000, 50000)),
    (23, "People's Planet", "time bands", (500, 500)),
    (23, "People's Planet", "Home packages", (15000, 35000)),
    (25, "Sporty TV", "weekday time bands", (500, 2000)),
    (25, "Sporty TV", "Saturday time bands", (500, 3000, 500)),
    (25, "Sporty TV", "Sunday time bands", (500, 3000, 2000)),
    (25, "Sporty TV", "monthly package", (50000,)),
    (27, "Star Khanya", "monthly packages", (50000, 100000, 150000)),
    (28, "Star Khanya", "weekday time bands", (500, 2000, 5000, 6000, 5000, 6000, 5000, 6000, 5000, 9000, 6000, 1000)),
    (28, "Star Khanya", "weekend time bands", (500, 2000, 5000, 6000, 10000, 8000, 1000)),
    (29, "StarLife", "weekday time bands", (3000, 5000, 6000, 8000, 15000, 17000, 25000, 22000, 15000, 10000, 5000)),
    (29, "StarLife", "Saturday time bands", (3000, 6000, 8000, 9000, 20000, 13000, 10000, 3000)),
    (29, "StarLife", "Sunday time bands", (500, 3000, 6000, 9000, 11000, 20000, 26000, 22000, 13000, 10000, 3000)),
    (29, "StarLife", "monthly packages", (85000, 160000, 330000)),
    (31, "eVOD", "video and banner rates", (1, 500)),
)


def emedia_entries(
    make_entry: Callable[..., dict[str, Any]], source_hash: str
) -> list[dict[str, Any]]:
    """Return one reference entry per visually printed commercial rate."""
    if source_hash != EMEDIA:
        return []
    result = []
    for page, channel, section, amounts in RATE_GROUPS:
        for occurrence, amount in enumerate(amounts, start=1):
            result.append(make_entry(
                source_hash,
                f"visual:page={page};section={section};rate={occurrence}",
                f"{channel} - {section} - occurrence {occurrence}",
                rate=str(amount),
                raw_rate=f"R{amount:,}" if isinstance(amount, int) else f"R{amount}",
                currency="ZAR",
                channel=channel,
                section=section,
                ambiguity=(
                    "Rate cells spanning multiple schedule rows are enumerated once; "
                    "no unsupported programme-to-cell split is inferred."
                ),
            ))
    if len(result) != 360:
        raise ValueError(f"Expected 360 eMedia rate entries, found {len(result)}.")
    return result
