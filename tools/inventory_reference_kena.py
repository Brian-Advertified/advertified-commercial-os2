"""Human-reviewed reference entries for Kena Outdoor inventory documents."""

from __future__ import annotations

from collections.abc import Callable
from typing import Any


EntryFactory = Callable[..., dict[str, Any]]


def kena_digital_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "9dcebc87869fff582f6b2ab6eae37cc428fc57452d032437904f696c856bd844"
    sites = [
        ("KOD 01", "Immink Drive - Diepkloof, Soweto, Gauteng", "3m H x 6m W; 18sqm", "574110", "576", "3-8", "35000"),
        ("KOD 02", "Vilakazi Street - Orlando West, Soweto, Gauteng", "3m H x 6m W; 18sqm", "574110", "576", "3-8", "35000"),
        ("KOD 03", "Pimville Square - Soweto, Gauteng", "3m H x 6m W; 18sqm", "574110", "576", "3-8", "35000"),
        ("KOD 04", "Nelson Mandela Square - Sandton, JHB, Gauteng", "4m H x 8m W; 32sqm", "592186", "545", "4-10", "65000"),
        ("KOD 05", "Katherine Drive - Sandton, JHB, Gauteng", "3m H x 6m W; 18sqm", "592186", "576", "3-10", "35000"),
        ("KOD 06", "Carnival City Casino - Brakpan, Gauteng", "3m H x 6m W; 18sqm", "327414", "576", "3-10", "35000"),
        ("KOD 07", "Rivonia Street - Sandton, JHB, Gauteng", "3m H x 6m W; 18sqm", "592186", "576", "3-8", "35000"),
        ("KOD 08", "Meat Meet Digital Screen - Soweto, JHB, Gauteng", "3m H x 6m W; 18sqm", "574110", "576", "3-8", "35000"),
    ]
    return [
        entry(source, f"pdf:page={index + 5};site={code}", f"{code} - {location}",
              channel="DOOH", site_code=code, location=location, dimensions=dimensions,
              audience_reach=reach, slot_duration="15 seconds", slots_per_loop="10",
              flightings_per_day=flightings, audience_lsm=lsm, material="Digital Screen",
              monthly_media_rate=rate, currency="ZAR",
              source_stated_availability="Immediately in September 2025 source; current availability not verified")
        for index, (code, location, dimensions, reach, flightings, lsm, rate) in enumerate(sites, start=1)
    ]


def kena_static_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "386261b0f707065d8abad9d394d52dcc0db961650ceed3462d384a943b136dc1"
    sites = [
        (7, "S0118KM", "M1 South before Smith off-ramp", "4m x 54m", "44280", "90000", None, "1010796", "7509964", "Immediately"),
        (8, "KOW 015", "M1 Highway, Waverley by Melrose Arch, Sandton", "12m x 60m", "147600", "200000", None, None, None, "Immediately"),
        (9, "KOW 016", "Woodmead Drive, Opposite Makro, Woodmead", "8m x 32m", "52480", "120000", None, None, None, "Immediately"),
        (10, "KOW 016", "Katherine Street and Pongola Crescent, Kramerville", "8m x 32m", "52480", "120000", None, None, None, "Immediately"),
        (11, "KOW 05", "African Diamond, 123 Kerk Street from Maboneng Precinct", "19m x 58m", "221400", "150000", None, None, None, "Immediately"),
        (12, "KOW 17", "Helen Joseph Street & Pixley Ka Isaka Seme Street, JHB CBD", "9.5m x 58.9m & 14.67m x 23.64m", "185801.50", "180000", None, None, None, "Immediately"),
        (13, "S021KM", "Marlboro Road towards Sandton, Johannesburg", "3m x 12m", "7380", "50000", None, "704189", "3903324", "Immediately"),
        (14, "S0109KM", "N1 North towards Pretoria, Midrand", "12m x 30m", "77400", "200000", None, "558449", "5502983", "Immediately"),
        (15, "S019KM", "N1 South towards Johannesburg, Midrand", "12m x 30m", "77400", "200000", None, "558449", "5502983", "Immediately"),
        (17, "KOB01", "Meet Meat, Orlando", "8.6m x 8m", "14760", "45000", None, "about 20000 per day", None, "Immediately"),
        (19, "S0139KM/B", "Kgosi Mampuru Street - Entrance to Pretoria CBD", "3m x 12m", "7380", "40000", None, "244989", "3449528", "Immediately"),
        (20, "NW/1/KM/A", "Lucas Mangope Highway towards Morula Sun, Mabopane - face A", "4.5m x 18m", "16605", "40000", None, "233865", "1876162", "Immediately"),
        (21, "NW/1/KM/B", "Lucas Mangope Highway towards Morula Sun, Mabopane - face B", "4.5m x 18m", "16605", "40000", None, "233865", "1876162", "Immediately"),
        (22, "PR03KM/B", "Pretoria John Vorster Drive R80", "12m x 9m", "TBC", "30000", None, None, None, "Immediately"),
        (24, "S0233KM/A", "Moshoeshoe Street, Mangaung - face A", "7.3m x 4.7m", "10530", "30000", None, "102071", "614672", "Immediately"),
        (25, "S0233KM/B", "Moshoeshoe Street, Mangaung - face B", "7.3m x 4.7m", "10530", "30000", None, "102071", "614672", "Immediately"),
        (26, "S0226KM/B", "Long Road Game Centre, Welkom", "6m x 4m", "5400", "30000", "24000", "117873", "1081734", "Immediately"),
        (27, "S0225KM/B", "State Way Road, Welkom", "6m x 4m", "5400", "20000", None, "117873", "1081734", "Immediately"),
        (28, "S079KM/A", "Mampoi Road, Phuthaditjhaba", "9m x 6m", "12150", "35000", None, "117873", "1081734", "Immediately"),
        (30, "S0222KM", "Govan Mbeki Road Harare Bridge, Khayelitsha", "3m x 16m", "10800", "40000", None, "528614", "10034850", "Immediately"),
        (31, "PR19KM", "Adam Tas Street R310, Stellenbosch - 3m x 6m", "3m x 6m", "4050", "30000", None, None, None, "Immediately"),
        (32, "PR20KM", "Adam Tas Street R310, Stellenbosch - 3m x 12m", "3m x 12m", "8100", "40000", None, None, None, "1 January 2026 subject to non-renewal by client"),
        (34, "S096KM", "R49 Exit from Mahikeng", "4.5m x 18m", "18225", "45000", None, "30344", "175535", "Immediately"),
        (35, "S0113KM", "R49 Entrance to Mahikeng", "4.5m x 18m", "18225", "45000", None, "30344", "175535", "Immediately"),
        (36, "S0206KM/A", "R556 Entrance to Sun City", "4.5m x 18m", "18225", "60000", None, "5079", "28750", "Immediately"),
        (38, "S0240KM/A", "North-East Expressway, East London", "4m x 16m", "18225", "45000", None, "44527", "938246", "Immediately"),
        (39, "EC08/B", "N2 Settlers Highway, East London", "4.5m x 18m", "18225", "45000", None, "40000 per day", None, "Immediately"),
        (40, "EC04/B", "Bonza Road, Beacon Bay, East London", "9m x 6m", "12150", "35000", None, "40000 per day", None, "Immediately"),
        (41, "EC05/A", "Golden Highway, East London", "9m x 6m", "12150", "35000", None, "40000 per day", None, "Immediately"),
        (42, "EC06/B", "Cecilia Makiwane Hospital Mdantsane, East London", "6m x 4m", "5400", "25000", None, "45000 per day", None, "Immediately"),
        (44, "S0245KM", "Prospecton Road N2, Isiphingo", "3m x 12m", "8100", "45000", None, "313794", "source displays 3 3942 830", "Immediately"),
        (46, "S0137KM/A", "Lydenburg entrance", "4m x 16m", "14400", "35000", None, "111953", "1081914", "Immediately"),
        (47, "S0137KM/B", "Lydenburg exit", "4m x 16m", "14400", "35000", None, "111953", "1081914", "Immediately"),
    ]
    return [
        entry(source, f"pdf:page={page};site={code}", f"{code} - {name}", channel="OOH",
              site_code=code, location=name, dimensions=dimensions, production_cost=production,
              rate_card=rate, discounted_rate=discount, currency="ZAR", audience_reach=reach,
              impacts=impacts, source_stated_availability=f"{availability} in September 2025 source; current availability not verified")
        for page, code, name, dimensions, production, rate, discount, reach, impacts, availability in sites
    ]
