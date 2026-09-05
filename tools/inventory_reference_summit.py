"""Human-reviewed reference entries for Summit OOH source decks."""

from __future__ import annotations

from collections.abc import Callable
from typing import Any


EntryFactory = Callable[..., dict[str, Any]]


def summit_main_market_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "63f197392e11ca891447eddb870a92bcdb8cb99a28ebcf4bfbdcc90e4819567f"
    shared = {
        "channel": "DOOH",
        "publisher": "Summit OOH Media",
        "illumination": "Digital Screen",
        "ara_compliance": "Yes",
        "availability": "Immediate",
        "availability_context": "source deck is labelled 2025; not current availability evidence",
        "monthly_impressions": "TBC",
        "frequency": "TBC",
        "rate": "NOT_STATED",
    }
    sites = [
        (3, "YJB 169", "Noord Street Taxi Rank Mall, JHB CBD", "JHB CBD", "Johannesburg", "Gauteng", "2.5m x 5m", "-26.198334, 28.047715", "TBC", "TBC", "1122748573", "840(h) x 1680(w)", "Animation & Static", "10 or 15 seconds"),
        (5, "GDP01", "Chris Hani Road, Soweto - P4 Digital Pods", "Soweto", "Johannesburg", "Gauteng", "1.92m x 1.28m per screen", "-26.259334, 27.929752", "8655290", "VACm", "376688845", "432(h) x 288(w)", "Animation & Static", "10 or 15 seconds"),
        (7, "GDP02", "Ivory Park Mall & Taxi Rank, Tembisa - P4 Digital Pods x 3", "Tembisa", "Johannesburg", "Gauteng", "1.92m x 1.28m per screen", "-25.981314, 28.205149", "1101000", "VACm", "948161206", "432(h) x 288(w)", "Animation & Static", "10 or 15 seconds"),
        (9, "GDP03", "Hammanskraal Mall & Taxi Rank - P4 Digital Pods 2", "Hammanskraal", "Pretoria", "Gauteng", "1.92m x 1.28m per screen", "-25.408721, 28.281199", "432730", "VACm", "905033718", "432(h) x 288(w)", "Animation & Static", "10 or 15 seconds"),
        (11, "GDP04", "East Rand Mall Taxi Rank", "Boksburg", "Johannesburg", "Gauteng", "1.92m x 1.28m per screen", "-26.183358, 28.241425", "270060", "VACm", "876187595", "432(h) x 288(w)", "Static only", "15 or 30 seconds"),
        (13, "GDP05", "Alexandra Mall & Taxi Rank", "Alexandra", "Johannesburg", "Gauteng", "1.92m x 1.28m per screen", "-26.105251, 28.118363", "1 Million", "footcount", "1048480539", "432(h) x 288(w)", "Animation & Static", "10 or 15 seconds"),
        (15, "GDP06", "Bushbuckridge Mall & Taxi Rank", "Bushbuckridge", "Bushbuckridge", "Mpumalanga", "1.92m x 1.28m per screen", "-24.836545, 31.070155", "1.95m commuters; 550000 shoppers per month", "two source-stated audience measures", "1053868404", "432(h) x 288(w)", "Animation & Static", "10 or 15 seconds"),
    ]
    return [
        entry(
            source, f"pptx:slides={slide - 1}-{slide};specs-slide=16", f"{code} - {name}",
            site_code=code, site_name=name, suburb=suburb, city=city, province=province,
            screen_size=size, gps_coordinates=coordinates, monthly_reach=reach,
            reach_basis=reach_basis, broadsign_ssp_id=ssp_id, artwork_pixels=pixels,
            artwork_format=format_name, artwork_duration=duration,
            file_format="MP4 or JPEG only", **shared,
        )
        for slide, code, name, suburb, city, province, size, coordinates, reach,
        reach_basis, ssp_id, pixels, format_name, duration in sites
    ]


def summit_billboard_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "acc990785d05dd17b7708f34fc47c2daa4b18a359af5dc1f157a72f9f3f1afcb"
    shared = {
        "channel": "DOOH", "publisher": "Summit OOH Media",
        "illumination": "Digital Screen", "availability": "Immediate",
        "availability_context": "source deck is labelled 2025; not current availability evidence",
        "monthly_reach": "TBC", "frequency": "TBC", "rate": "NOT_STATED",
    }
    sites = [
        (3, "YJB 108A", "N1 Highway: William Nicol to Rivonia Road", "Bryanston", "Johannesburg", "Gauteng", "5m x 12m", "26.034369, 28.037975", "16247125", "Yes", "630844871", "480(h) x 1152(w)", "Static only", "15 or 30 seconds"),
        (5, "YJB 142", "N2/Airport Approach Road - Cape Town", "N2 Airport Approach", "Cape Town", "Western Cape", "3m x 6m", "33.964633, 18.570991", "2127600", "No", "992607468", "288(h) x 576(w)", "Static only", "15 seconds"),
        (8, "YJB 109", "Oxford Road: Illovo", "Illovo/Rosebank", "Johannesburg", "Gauteng", "4m x 8m", "26.128106, 28.050114", "1240770", "Yes", "653277301", "384(h) x 768(w)", "Animation & Static", "10 or 15 seconds"),
        (10, "YJB 101", "William Nicol: Bryanston", "Bryanston", "Johannesburg", "Gauteng", "4m x 8m", "26.045883, 28.019642", "11685836", "Yes", "914842286", "384(h) x 768(w)", "Animation & Static", "10 or 15 seconds"),
        (12, "YJB 119", "Jan Smuts: Rosebank", "Rosebank", "Johannesburg", "Gauteng", "3m x 6m", "26.145920, 28.035638", "7153760", "Yes", "738375324", "288(h) x 576(w)", "Animation & Static", "10 or 15 seconds"),
        (15, "YJB 157", "Bedford Centre: Bedfordview", "Bedfordview", "Johannesburg", "Gauteng", "3m x 6m", "26.187300, 28.124562", "10114130", "Yes", "719757919", "288(h) x 576(w)", "Animation & Static", "10 or 15 seconds"),
        (17, "YJB 143", "Beyers Naude at Cresta Mall", "Cresta", "Johannesburg", "Gauteng", "4m x 8m", "26.132373, 27.973094", "3512080", "Yes", "982926367", "384(h) x 768(w)", "Animation & Static", "10 or 15 seconds"),
        (19, "RJB 101", "Hendrik Potgieter: West Rand", "Little Falls", "Johannesburg", "Gauteng", "4m x 8m", "-26.10587, 27.880887", "937260", "Yes", "518683851", "480(h) x 960(w)", "Animation & Static", "10 or 15 seconds"),
        (23, "YJB 175", "George: Western Cape Digital/N2 Knysna", "New George Area", "George", "Western Cape", "3m x 6m", "-33.978881, 22.494074", "TBC", "Yes", "TBC", "288(h) x 576(w)", "Min Animation & Static", "15 seconds"),
        (26, "YJB 151", "Nelspruit - White River Road", "White River Road", "Nelspruit", "Mpumalanga", "5m x 10m", "-25.418364, 30.973534", "1013760", "Yes", "TBC", "480(h) x 1024(w)", "Min Animation & Static", "10 or 15 seconds"),
    ]
    return [
        entry(
            source, f"pptx:detail-slide={slide};specs-slide=27;site-code={code}", f"{code} - {name}",
            site_code=code, site_name=name, suburb=suburb, city=city, province=province,
            screen_size=size, gps_coordinates_as_stated=coordinates,
            monthly_impressions=impressions, ara_compliance=ara, broadsign_ssp_id=ssp_id,
            artwork_pixels=pixels, artwork_format=format_name, artwork_duration=duration,
            file_format="MP4 or JPEG only", **shared,
        )
        for slide, code, name, suburb, city, province, size, coordinates, impressions,
        ara, ssp_id, pixels, format_name, duration in sites
    ]
