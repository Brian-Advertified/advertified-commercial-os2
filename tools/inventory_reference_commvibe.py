"""Human-reviewed station entries from Commvibe demographic profiles."""

from __future__ import annotations

from collections.abc import Callable
from typing import Any


EntryFactory = Callable[..., dict[str, Any]]


def commvibe_demographic_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "0d4d0a38cf28ec7d4f23616ffde2e62df2f9495e77c63c6536538fba22b287a0"
    shared = {
        "channel": "RADIO", "publisher": "Commvibe",
        "rate": "NOT_STATED", "audience_age_floor": "15+",
        "audience_source_period": "BRC RAMS Amplify October 2022 - September 2023",
    }
    stations = [
        (4, "Gauteng", "Theta FM", "208000"),
        (5, "Gauteng", "Radio Pulpit / Radio Kansel", "60000"),
        (6, "Gauteng", "Groot FM", "76000"),
        (7, "Gauteng", "Impact Radio", "51000"),
        (8, "Gauteng", "Kasie FM", "110000"),
        (9, "Gauteng", "Lekoa FM", "61000"),
        (10, "Gauteng", "Mams FM", "57000"),
        (11, "Gauteng", "Moretele FM", "87000"),
        (12, "Gauteng", "Pheli FM", "35000"),
        (13, "Gauteng", "Pretoria FM", "80000"),
        (14, "Gauteng", "Alex FM", "28000"),
        (17, "Western Cape", "Bok Radio", "108000"),
        (18, "Western Cape", "Zibonele FM", "219000"),
        (19, "Western Cape", "Radio KC", "43000"),
        (20, "Western Cape", "Voice of the Cape", "146000"),
        (21, "Western Cape", "Fine Music Radio", "57000"),
        (22, "Western Cape", "CCFM", "119000"),
        (23, "Western Cape", "Radio Tygerberg", "121000"),
        (24, "Western Cape", "Bush Radio", "55000"),
        (25, "Western Cape", "Eden FM", "84000"),
        (26, "Western Cape", "Heartbeat FM", "45000"),
        (28, "Mpumalanga", "eMalahleni FM", "59000"),
        (30, "Limpopo", "Energy FM", "41000"),
        (31, "Limpopo", "Vhembe FM", "93000"),
    ]
    result = [
        entry(source, f"pdf:page={page};station={station}", station,
              province=province, past_7_day_listenership=listenership, **shared)
        for page, province, station, listenership in stations
    ]
    collectives = [
        (3, "Gauteng collective", "678000", "Theta FM; Kasie FM; Alex FM; Pretoria FM; Groot FM; Mams FM; Pheli FM; Moretele FM"),
        (16, "Western Cape collective", "631000", "Bok Radio; Zibonele FM; Radio KC; CCFM; Voice of the Cape"),
    ]
    result.extend(
        entry(source, f"pdf:page={page};collective={name}", name,
              inventory_kind="STATION_COLLECTIVE", past_7_day_listenership=listenership,
              constituent_stations=members, **shared)
        for page, name, listenership, members in collectives
    )
    return result
