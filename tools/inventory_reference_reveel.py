"""Human-reviewed inventory entries for the Reveel South Africa media kit."""

from __future__ import annotations

from collections.abc import Callable
from typing import Any


EntryFactory = Callable[..., dict[str, Any]]


def reveel_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "9f2f89edee393df15397d6df5843aa1dd042c99fe400cd7856fb96f4326680c5"
    sites = [
        (5, "DIGITAL", "Sandton", "Maslow Hotel"),
        (5, "DIGITAL", "Sandton", "Rivonia Road"),
        (6, "DIGITAL", "Sandton", "Katherine Street"),
        (6, "DIGITAL", "Fourways", "Cedar Fourways"),
        (7, "DIGITAL", "Sandton", "M1 Freeway Offramp"),
        (8, "DIGITAL", "Hyde Park", "Jan Smuts Drive"),
        (8, "DIGITAL", "Sandton", "Sandton Central Park"),
        (9, "DIGITAL", "Soweto", "Vilakazi Street"),
        (9, "DIGITAL", "Soweto", "Chris Hani Road"),
        (10, "DIGITAL", "Umhlanga", "Oceans Mall, Lighthouse Road"),
        (10, "DIGITAL", "Umhlanga", "Oceans Mall, Lagoon Drive"),
        (11, "DIGITAL", "Umhlanga", "Umhlanga Arch"),
        (11, "DIGITAL", "Ballito, KZN", "Ballito Junction"),
        (12, "DIGITAL", "Durban", "Durban North"),
        (12, "DIGITAL", "Durban", "Umhlanga Ridge"),
        (13, "DIGITAL", "Cape Town", "Cape Town International Airport"),
        (13, "DIGITAL_NETWORK", "Cape Town", "Market Digital Network - Lifestyle Markets"),
        (15, "DIGITAL", "V&A Waterfront", "The Arc (Amphitheatre)"),
        (15, "DIGITAL", "V&A Waterfront", "The Centre Court"),
        (16, "DIGITAL", "V&A Waterfront", "Woolies Pillars"),
        (16, "DIGITAL", "V&A Waterfront", "Vovo Telo"),
        (18, "STATIC", "Johannesburg", "Braamfontein"),
        (18, "STATIC", "Johannesburg", "M1 Freeway, Braamfontein"),
        (19, "STATIC", "Johannesburg", "R24 / N12 Interchange"),
        (19, "STATIC", "Soweto", "Thokoza Park"),
    ]
    result = [
        entry(
            source,
            "pptx:slides=5,7;site=Sandton-Rivonia-Road" if name == "Rivonia Road" else f"pptx:slide={slide};site={place}-{name}",
            f"{place} - {name}", channel="DOOH", media_format=format_name,
            publisher="Reveel", location=place, site_name=name, rate="NOT_STATED",
            source_occurrences="slides 5 and 7" if name == "Rivonia Road" else f"slide {slide}",
        )
        for slide, format_name, place, name in sites
    ]
    result.append(entry(
        source, "pptx:slides=20-22", "Virgin Active Digital Network",
        channel="DOOH", media_format="DIGITAL_NETWORK", publisher="Reveel",
        location="Virgin Active health clubs; locations not stated",
        site_name="Virgin Active Digital Network", rate="NOT_STATED",
        network_scope="NOT_STATED_IN_THIS_SOURCE",
    ))
    return result
