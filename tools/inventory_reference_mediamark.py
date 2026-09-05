"""Human-reviewed reference entries for the visible Mediamark 2026 rate card."""

from __future__ import annotations

from collections.abc import Callable
from typing import Any


EntryFactory = Callable[..., dict[str, Any]]
SOURCE = "30f98d7fdd70c073ea4334a07c700ed7d9010a71a4ca47b7275f170b07f7c2de"
JACARANDA_SOURCE = "4da5d3e9b7286b7b06bfddc448b80dd54998c2b0e1c19d025aa2af81d8e7e921"
RADIO_TERMS = {
    "channel": "RADIO", "currency": "ZAR", "vat": "EXCLUDED", "duration_seconds": "30",
    "effective_from": "2026-07-01",
    "duration_multipliers": "5s=.5;10s=.6;15s=.7;20s=.8;25s=.9;30s=1;35s=1.17;40s=1.33;45s=1.5;50s=1.67;55s=1.83;60s=2",
    "loadings": "live read=1.6x; feature=1.3x; preferred spot=1.4x",
}


def _band_entries(entry: EntryFactory, page: int, station: str,
                  bands: list[tuple[str, str, str]]) -> list[dict[str, Any]]:
    return [
        entry(SOURCE, f"pdf:page={page};station={station};rate-row={index}",
              f"{station} - {day} {band}", station=station, day_scope=day,
              time_band=band, rate=rate, charging_basis="PER_30_SECOND_SPOT", **RADIO_TERMS)
        for index, (day, band, rate) in enumerate(bands, start=1)
    ]


def _full_footprint(entry: EntryFactory) -> list[dict[str, Any]]:
    ecr = [
        ("Monday-Friday", "00:00-04:00", "360"), ("Monday-Friday", "04:00-06:00", "1590"),
        ("Monday-Friday", "06:00-09:00", "21075"), ("Monday-Friday", "09:00-12:00", "7800"),
        ("Monday-Friday", "12:00-15:00", "7455"), ("Monday-Friday", "15:00-19:00", "10845"),
        ("Monday-Friday", "19:00-22:00", "2175"), ("Monday-Friday", "22:00-24:00", "390"),
        ("Saturday", "00:00-06:00", "390"), ("Saturday", "06:00-09:00", "6555"),
        ("Saturday", "09:00-12:00", "6555"), ("Saturday", "12:00-15:00", "2145"),
        ("Saturday", "15:00-19:00", "2130"), ("Saturday", "19:00-24:00", "735"),
        ("Sunday", "00:00-06:00", "360"), ("Sunday", "06:00-09:00", "2565"),
        ("Sunday", "09:00-12:00", "2625"), ("Sunday", "12:00-15:00", "1455"),
        ("Sunday", "15:00-19:00", "1425"), ("Sunday", "19:00-24:00", "435"),
    ]
    jacaranda = [
        ("Monday-Friday", "00:00-04:00", "570"), ("Monday-Friday", "04:00-06:00", "4410"),
        ("Monday-Friday", "06:00-09:00", "24555"), ("Monday-Friday", "09:00-12:00", "11595"),
        ("Monday-Friday", "12:00-15:00", "10725"), ("Monday-Friday", "15:00-19:00", "13770"),
        ("Monday-Friday", "19:00-22:00", "1830"), ("Monday-Friday", "22:00-24:00", "570"),
        ("Saturday", "00:00-06:00", "570"), ("Saturday", "06:00-09:00", "7740"),
        ("Saturday", "09:00-12:00", "8385"), ("Saturday", "12:00-15:00", "2700"),
        ("Saturday", "15:00-19:00", "2625"), ("Saturday", "19:00-24:00", "1170"),
        ("Sunday", "00:00-06:00", "600"), ("Sunday", "06:00-09:00", "3690"),
        ("Sunday", "09:00-12:00", "3900"), ("Sunday", "12:00-15:00", "2295"),
        ("Sunday", "15:00-19:00", "2250"), ("Sunday", "19:00-24:00", "870"),
    ]
    return _band_entries(entry, 4, "East Coast Radio", ecr) + _band_entries(entry, 6, "Jacaranda FM", jacaranda)


def _split_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    rows = [
        ("Monday-Friday", "04:00-06:00", "GP incl. NW", "3810"), ("Monday-Friday", "04:00-06:00", "Limpopo", "750"), ("Monday-Friday", "04:00-06:00", "Mpumalanga", "750"),
        ("Monday-Friday", "06:00-09:00", "GP incl. NW", "19980"), ("Monday-Friday", "06:00-09:00", "Limpopo", "3795"), ("Monday-Friday", "06:00-09:00", "Mpumalanga", "4200"),
        ("Monday-Friday", "09:00-12:00", "GP incl. NW", "8775"), ("Monday-Friday", "09:00-12:00", "Limpopo", "1635"), ("Monday-Friday", "09:00-12:00", "Mpumalanga", "2625"),
        ("Monday-Friday", "12:00-15:00", "GP incl. NW", "8625"), ("Monday-Friday", "12:00-15:00", "Mpumalanga", "2595"), ("Monday-Friday", "12:00-15:00", "Jacaranda FM Regional", "1575"),
        ("Monday-Friday", "15:00-16:00", "GP incl. NW", "10530"), ("Monday-Friday", "15:00-16:00", "Mpumalanga", "2505"), ("Monday-Friday", "15:00-16:00", "Jacaranda FM Regional", "1575"),
        ("Monday-Friday", "16:00-19:00", "GP incl. NW", "10530"), ("Monday-Friday", "16:00-19:00", "Limpopo", "2535"), ("Monday-Friday", "16:00-19:00", "Mpumalanga", "2505"),
        ("Saturday", "05:00-09:00", "Gauteng", "5550"), ("Saturday", "05:00-09:00", "Limpopo", "900"), ("Saturday", "05:00-09:00", "Mpumalanga", "1965"),
        ("Saturday", "09:00-10:00", "Gauteng", "5955"), ("Saturday", "09:00-10:00", "Limpopo", "915"), ("Saturday", "09:00-10:00", "Mpumalanga", "2145"),
        ("Saturday", "10:00-12:00", "Gauteng", "5955"), ("Saturday", "10:00-12:00", "Mpumalanga", "2145"), ("Saturday", "10:00-12:00", "Jacaranda FM Regional", "930"),
        ("Saturday", "12:00-14:00", "Gauteng", "2055"), ("Saturday", "12:00-14:00", "Mpumalanga", "855"), ("Saturday", "12:00-14:00", "Jacaranda FM Regional", "930"),
        ("Saturday", "14:00-15:00", "Gauteng", "2040"), ("Saturday", "14:00-15:00", "Limpopo", "780"), ("Saturday", "14:00-15:00", "Mpumalanga", "780"),
        ("Saturday", "15:00-19:00", "Gauteng", "2040"), ("Saturday", "15:00-19:00", "Limpopo", "780"), ("Saturday", "15:00-19:00", "Mpumalanga", "780"),
    ]
    restrictions = "no live reads, preferred spots, features, sponsorships or Sunday broadcasts on splits; n/a cells are not offers"
    return [
        entry(SOURCE, f"pdf:page=7;rate-cell={index}", f"Jacaranda FM - {scope} - {day} {band}",
              station="Jacaranda FM", station_scope=scope, day_scope=day, time_band=band,
              rate=rate, charging_basis="PER_30_SECOND_SPOT", restrictions=restrictions,
              **{key: value for key, value in RADIO_TERMS.items() if key != "loadings"})
        for index, (day, band, scope, rate) in enumerate(rows, start=1)
    ]


def _digital_and_research(entry: EntryFactory) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    display = [
        ("Leaderboard", "728x90 max 39k", "365"), ("Medium Rectangle", "300x250 max 39k", "365"),
        ("Half Page Advertisement", "300x600 max 39k", "415"), ("Billboard", "970x250 max 39k", "495"),
        ("Mobile Banner", "300x50 or 320x50 max 39k", "185"), ("Video", "1920x1080 max 10mb; 15, 30 or 45 seconds", "420"),
    ]
    for brand in ["East Coast Radio", "Jacaranda FM"]:
        for index, (name, specification, rate) in enumerate(display, start=1):
            result.append(entry(SOURCE, f"pdf:page=9;display={index};brand={brand}", f"{brand} display - {name}",
                                channel="DIGITAL", brand=brand, rate=rate, charging_basis="CPM",
                                currency="ZAR", vat="EXCLUDED", specification=specification))
    for brand in ["East Coast Gold", "East Coast Radio", "Jacaranda FM"]:
        for index, (name, rate) in enumerate([("Pre-roll + companion banner", "650"), ("Mid-roll + companion banner", "500")], start=1):
            result.append(entry(SOURCE, f"pdf:page=9;audio={index};brand={brand}", f"{brand} digital audio - {name}",
                                channel="DIGITAL_AUDIO", brand=brand, rate=rate, charging_basis="COMPLETED_LISTEN_CPM",
                                currency="ZAR", vat="EXCLUDED", targeting="included", duration="15-30 seconds"))
    for index, name in enumerate(["SoundInsights NeuroLab", "Insight Studio", "Sound Attribution", "Sonic Motion Sound Match"], start=1):
        result.append(entry(SOURCE, f"pdf:page=8;research-product={index}", name,
                            channel="RESEARCH", rate="NOT_STATED", currency="ZAR"))
    return result


def mediamark_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    return _full_footprint(entry) + _split_entries(entry) + _digital_and_research(entry)


def jacaranda_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    selected: list[dict[str, Any]] = []
    page_map = {"page=6": "page=5", "page=7": "page=6", "page=8": "page=7"}
    for record in mediamark_entries(entry):
        fields = record["expected_fields"]
        if not (fields.get("station") == "Jacaranda FM" or
                fields.get("brand") == "Jacaranda FM" or fields.get("channel") == "RESEARCH"):
            continue
        locator = record["locator"]
        for old, new in page_map.items():
            locator = locator.replace(old, new)
        selected.append(entry(JACARANDA_SOURCE, locator, record["identity"], **fields))
    return selected
