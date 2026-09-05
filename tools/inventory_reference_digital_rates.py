"""Human-reviewed reference entries for Primedia's July 2026 digital rate card."""

from __future__ import annotations

from collections.abc import Callable
from typing import Any


EntryFactory = Callable[..., dict[str, Any]]
SOURCE = "749374a62ab97cb9d42f89704d6c492696c2952d40a0dcd00b23c65dca08146f"


def tier_entries(
    entry: EntryFactory,
    page: int,
    package: str,
    rows: list[tuple[str, str, str, str, str, str, str | None, str | None]],
    **shared: Any,
) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    for index, (tier, impressions, duration, value, investment, discount, clicks, posts) in enumerate(rows):
        tier_fields = {
            key: field[index] if isinstance(field, tuple) and len(field) == len(rows) else field
            for key, field in shared.items()
        }
        result.append(entry(
            SOURCE, f"pdf:page={page};package={package};tier={tier}",
            f"{package} - {tier}", impressions=impressions,
            minimum_campaign_duration=duration, value=value,
            investment=investment, discount=discount, clicks=clicks,
            posts=posts, **tier_fields,
        ))
    return result


def audio_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    shared = {"channel": "DIGITAL_AUDIO", "currency": "ZAR", "vat": "EXCLUDED"}
    result = [
        entry(SOURCE, "pdf:page=4;rate=pre-roll", "Primedia+ in-stream audio pre-roll",
              rate="200.00", charging_basis="CPM", placement="PRE_ROLL", **shared),
        entry(SOURCE, "pdf:page=4;rate=mid-roll", "Primedia+ in-stream audio mid-roll",
              rate="170.00", charging_basis="CPM", placement="MID_ROLL", **shared),
    ]
    rows = [
        ("Silver", "150000", "1 week", "30000", "27000", "10%", None, None),
        ("Gold", "500000", "2 weeks", "100000", "80000", "20%", None, None),
        ("Platinum", "1000000", "2 months", "200000", "140000", "30%", None, None),
    ]
    result += tier_entries(entry, 4, "Primedia+ in-stream audio package", rows,
                           inventory_basis="MID_ROLL", **shared)
    return result


def social_rows(
    impressions: tuple[str, str, str], posts: tuple[str | None, str | None, str | None],
    durations: tuple[str, str, str], values: tuple[str, str, str],
    investments: tuple[str, str, str], paid: tuple[str, str, str],
    totals: tuple[str, str, str], clicks: tuple[str, str, str] | None = None,
) -> list[tuple[str, str, str, str, str, str, str | None, str | None]]:
    tiers = ("Silver", "Gold", "Platinum")
    discounts = ("10%", "20%", "30%")
    return [
        (tier, impressions[index], durations[index], values[index], investments[index],
         discounts[index], clicks[index] if clicks else None, posts[index])
        for index, tier in enumerate(tiers)
    ]


def social_entries(entry: EntryFactory, page: int, publisher: str) -> list[dict[str, Any]]:
    primedia = publisher == "Primedia+"
    daily_rate = "8500" if primedia else "10500"
    reach_values = ("25500", "51000", "68000") if primedia else ("31500", "63000", "84000")
    reach_investments = ("22950", "40800", "47600") if primedia else ("28350", "50400", "58800")
    reach_totals = ("25950", "46800", "56600") if primedia else ("31350", "56400", "67800")
    traffic_values = ("49000", "66000", "142500") if primedia else ("57000", "78000", "172500")
    traffic_investments = ("44100", "52800", "99750") if primedia else ("51300", "62400", "120750")
    traffic_totals = ("50100", "64800", "117750") if primedia else ("57300", "74400", "138750")
    shared = {"channel": "SOCIAL", "publisher": publisher, "currency": "ZAR"}
    result = [entry(SOURCE, f"pdf:page={page};rate=daily-post", f"{publisher} daily social post",
                    rate=daily_rate, charging_basis="PER_DAY", platform_scope="all stated platforms", **shared)]
    reach = social_rows(("200000", "400000", "700000"), ("3", "6", "8"),
                        ("1 week", "2 weeks", "1 month"), reach_values, reach_investments,
                        ("3000", "6000", "9000"), reach_totals)
    traffic = social_rows(("100000", "250000", "400000"), ("4", "6", "15"),
                          ("1 week", "2 weeks", "1 month"), traffic_values, traffic_investments,
                          ("6000", "12000", "18000"), traffic_totals, ("2000", "3000", "5000"))
    result += tier_entries(entry, page, f"{publisher} social reach package", reach,
                           paid_media=("3000", "6000", "9000"), total_investment=reach_totals, **shared)
    result += tier_entries(entry, page, f"{publisher} social traffic package", traffic,
                           paid_media=("6000", "12000", "18000"), total_investment=traffic_totals, **shared)
    return result


def youtube_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    shared = {"channel": "DIGITAL_VIDEO", "publisher": "Eyewitness News", "currency": "ZAR", "vat": "EXCLUDED"}
    result = [
        entry(SOURCE, "pdf:page=8;rate=unskippable", "EWN YouTube unskippable pre-roll",
              rate="300", charging_basis="CPM", **shared),
        entry(SOURCE, "pdf:page=8;rate=skippable", "EWN YouTube skippable pre-roll",
              rate="250", charging_basis="CPM", **shared),
    ]
    common_impressions = ("100000", "500000", "1000000")
    durations = ("1 week", "2 weeks", "1 month")
    nonskip = social_rows(common_impressions, (None, None, None), durations,
                          ("30000", "150000", "300000"), ("27000", "120000", "210000"),
                          ("0", "0", "0"), ("27000", "120000", "210000"))
    skip = social_rows(common_impressions, (None, None, None), durations,
                       ("25000", "125000", "250000"), ("22500", "100000", "175000"),
                       ("0", "0", "0"), ("22500", "100000", "175000"))
    result += tier_entries(entry, 8, "EWN YouTube non-skippable package", nonskip, **shared)
    result += tier_entries(entry, 8, "EWN YouTube skippable package", skip, **shared)
    return result


def display_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    shared = {"channel": "DISPLAY", "publisher": "Primedia+ and Eyewitness News", "currency": "ZAR", "vat": "EXCLUDED"}
    offers = [
        ("Desktop", "300.00", "CPM"), ("Mobile", "150.00", "CPM"),
        ("Interstitial", "350.00", "CPM"), ("Anchor Ad", "350.00", "CPM"),
        ("Homepage Takeover (EWN only)", "55000.00", "DAILY_RATE"),
    ]
    result = [entry(SOURCE, f"pdf:page=10;ad-unit={index}", name, rate=rate,
                    charging_basis=basis, **shared)
              for index, (name, rate, basis) in enumerate(offers, start=1)]
    rows = social_rows(("200000", "500000", "1000000"), (None, None, None),
                       ("1 month", "6 weeks", "2 months"),
                       ("37500", "105000", "195000"), ("33750", "84000", "136500"),
                       ("0", "0", "0"), ("33750", "84000", "136500"))
    result += tier_entries(entry, 10, "Display advertising package", rows,
                           inventory_basis="combination of mobile and desktop display advertisements", **shared)
    return result


def digital_rates_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    return (audio_entries(entry) + social_entries(entry, 5, "Primedia+")
            + social_entries(entry, 7, "Eyewitness News")
            + youtube_entries(entry) + display_entries(entry))
