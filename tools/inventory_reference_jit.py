"""Human-reviewed reference entries for the Jit TV digital-screen concept."""

from __future__ import annotations

from collections.abc import Callable
from typing import Any


EntryFactory = Callable[..., dict[str, Any]]


def jit_tv_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "cd533cfebd184fd6e6fb52bda58df289353a7de797c9fb89874051af3d99055e"
    shared = {
        "channel": "DOOH", "publisher": "Jit TV", "currency": "ZAR",
        "ad_frequency": "Every 3-5 minutes", "target_market": "LSM 2-8",
    }
    detailed = [
        (4, "Mbombela Taxi Rank", "White River - Nelspruit", "6m x 3m", "150000 per day", "60000"),
        (5, "Soweto Theater", "Jabulani - Soweto", "6m x 3m", "150000 per day", "60000"),
        (7, "Chris Hani Road", "Diepkloof - Soweto", "8m x 5m; 21 metres high", "90000 cars per day", "85000"),
        (9, "Bara Taxi Rank", "Diepkloof - Soweto", "6m x 3m", "350000 per day", "65000"),
        (11, "Noord Taxi Rank", "Johannesburg - CBD", "6m x 9m", "1000000 per day", "85000"),
        (13, "Wanderers Taxi Rank", "Johannesburg - CBD", "6m x 3m", "150000 per day", "60000"),
        (14, "Bree (Lilian Ngoyi) Taxi Rank", "Johannesburg - CBD", "6m x 3m", "400000 per day", "60000"),
        (15, "Randburg Taxi Rank", "Randburg", "6m x 3m", "150000 per day", "60000"),
        (17, "Germiston Taxi Rank", "Germiston", "6m x 3m", "150000 per day", "60000"),
        (18, "Shoshanguve Taxi Rank", "Shoshanguve - Pretoria", "6m x 3m", "150000 per day", "60000"),
        (19, "Midrand Taxi Rank", "Midrand", "6m x 3m", "150000 per day", "60000"),
        (20, "Kwamashu Taxi Rank", "Durban - KZN", "5m x 3m", "150000 per day", "60000"),
        (22, "Thohoyandou Taxi Rank", "Thohoyandou - Limpopo", "6m x 3m", "150000 per day", "60000"),
        (23, "Majakathata Taxi Rank", "Bloemfontein", "6m x 3m", "150000 per day", "60000"),
    ]
    result = [
        entry(
            source, f"pdf:page={page};site={name}", name, location=location,
            dimensions=dimensions, daily_viewership=viewership,
            monthly_rate=rate, **shared,
        )
        for page, name, location, dimensions, viewership, rate in detailed
    ]
    for name, location in [
        ("Mdantsane Taxi Rank", "Eastern Cape"),
        ("Khayelitsha Taxi Rank", "Cape Town"),
        ("Umlazi Taxi Rank", "KZN"),
    ]:
        result.append(entry(
            source, f"pdf:page=3;summary-only-site={name}", name, location=location,
            dimensions="NOT_STATED", daily_viewership="NOT_STATED",
            monthly_rate="NOT_STATED", evidence_scope="summary list only", **shared,
        ))
    return result
