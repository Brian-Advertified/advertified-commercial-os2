"""Human-reviewed reference entries for the SB Outdoor programmatic deck."""

from __future__ import annotations

from collections.abc import Callable
from typing import Any


EntryFactory = Callable[..., dict[str, Any]]


def sb_outdoor_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "a576685e402f2b7b481822d9365df43cb85cf1303ffa359588ad9f08b8c8d18c"
    shared = {
        "channel": "DOOH",
        "publisher": "SB Outdoor",
        "currency": "ZAR",
        "rate": "143.00",
        "charging_basis": "CPM",
        "advert_duration": "15 seconds",
        "loop_duration": "3 minutes",
        "loop_capacity": "12 advertisements; source states full",
        "average_flight_time_per_slot_per_month": "14400",
    }
    sites = [
        (4, "SBFSD4003", "Nelson Mandela Drive", "Bloemfontein", "Bloemfontein", "Free State", "3 x 6", "922243", "LSM 6-10"),
        (5, "SBGPD4018", "Linksfield Road", "Dowerglen", "Edenvale", "Gauteng", "3 x 6", "737000", "LSM 6-10"),
        (6, "SBGPD4009", "Stoneridge Drive", "Greenstone Hill", "Edenvale", "Gauteng", "3 x 6", "660000", "LSM 6-10"),
        (7, "SBGPD3005", "Greenside High Energy Strip", "Greenside", "Johannesburg", "Gauteng", "3 x 6", "610000", "LSM 6-10"),
        (8, "SBGPD4001", "Van Buuren Road", "Bedfordview", "Bedfordview", "Gauteng", "3 x 6", "954091", "LSM 6-10"),
        (9, "SBGPD4002", "Woodlands Drive", "Woodmead", "Sandton", "Gauteng", "3 x 6", "832649", "LSM 6-10"),
        (10, "SBGPD3001", "N3 Highway", "Bedfordview", "Bedfordview", "Gauteng", "4.5 x 18", "1870000", "LSM 6-10"),
        (11, "SBKZND4008", "Kloof", "Kloof", "Kloof", "KwaZulu-Natal", "3 x 6", "944120", "LSM 7-10"),
        (12, "SBWCD4016", "Stellenbosch", "Stellenbosch", "Stellenbosch", "Western Cape", "3 x 6", "350000", "LSM 7-10"),
        (13, "SBWCD4006", "Somerset Mall", "Somerset West", "Somerset West", "Western Cape", "3 x 6", "770000", "LSM 6-10"),
    ]
    return [
        entry(
            source,
            f"pptx:slide={slide};site-code={site_code}",
            f"{site_code} - {name}",
            site_code=site_code,
            site_name=name,
            suburb=suburb,
            city=city,
            province=province,
            screen_size_metres=size,
            monthly_traffic=traffic,
            audience=audience,
            **shared,
        )
        for slide, site_code, name, suburb, city, province, size, traffic, audience in sites
    ]
