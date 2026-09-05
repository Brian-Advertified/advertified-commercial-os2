"""Human-reviewed reference entries for RSD/Tractor roadside digital decks."""

from __future__ import annotations

from collections.abc import Callable
from typing import Any


EntryFactory = Callable[..., dict[str, Any]]


def rsd_gauteng_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "ea1cc4fd04e56a8f9744128195425366101b9f59171703b5fe6ecfa6c761b205"
    shared = {
        "channel": "DOOH",
        "publisher": "Tractor",
        "province": "Gauteng",
        "rate": "NOT_STATED",
        "source_context": "deck title states Gauteng Roadside Digital Site Bible; source filename is labelled 2025",
    }
    sites = [
        (2, "TOGD001", "Fourways", "52%", "48%", "22%,32%,24%,13%,5%,4%", "22%,32%,25%,21%", "Fast Food Aficionados; Odds Chasers; Luxury Sommeliers"),
        (4, "TOGD003", "Craighall", "53%", "47%", "21%,27%,20%,14%,12%,6%", "19%,24%,28%,30%", "Retail Ninjas; Fast Food Aficionados; Fitness Junkies"),
        (6, "TOGD004", "Bryanston", "59%", "41%", "20%,20%,25%,21%,6%,8%", "9%,20%,25%,46%", "Auto Maestros; Cyber Cart Mavericks; Coffee Connoisseurs"),
        (8, "TOGD006", "N1 Midrand", "49%", "51%", "20%,33%,28%,12%,4%,3%", "15%,31%,31%,23%", "Retail Ninjas; Fitness Junkies; Student Faction"),
        (10, "TOGD007", "R24 Bedfordview", "57%", "43%", "19%,28%,28%,14%,7%,4%", "19%,35%,23%,23%", "Retail Junkies; Auto Maestros; Fast Food Aficionados"),
        (12, "TOGD008", "M1 Marlboro", "56%", "44%", "18%,34%,25%,15%,5%,3%", "16%,32%,27%,25%", "Retail Ninjas; Coffee Connoisseurs; Luxury Sommeliers"),
        (14, "TOGD009", "Sandton", "53%", "47%", "15%,24%,24%,24%,9%,4%", "12%,20%,28%,40%", "Retail Ninjas; Fitness Junkies; Luxury Sommeliers"),
        (16, "TOGD010", "Rosebank", "56%", "44%", "23%,30%,19%,10%,13%,5%", "20%,26%,26%,28%", "Retail Ninjas; Fitness Junkies; Student Faction"),
        (18, "TOGD011", "Parkhurst", "64%", "36%", "9%,11%,26%,25%,22%,7%", "0%,35%,20%,45%", "Taste Makers; Style Curators; Home Hustlers"),
        (20, "TOGD013", "Ferndale", "62%", "38%", "14%,27%,27%,13%,9%,11%", "12%,21%,34%,33%", "Money Masters; Care Crew; Fuel & Chill Squad"),
    ]
    return [
        entry(
            source, f"pptx:slides={slide}-{slide + 1};site-code={code}", f"{code} - {name}",
            site_code=code, site_name=name, male=male, female=female,
            age_ranges="18-24,25-34,35-44,45-54,55-64,65+",
            age_distribution=ages, sem_clusters="1-2,3,4,5",
            sem_distribution=sem, lifestyle_segments=segments,
            points_of_interest="SOURCE_PRESENT_NOT_TRANSCRIBED_AS_INVENTORY",
            **shared,
        )
        for slide, code, name, male, female, ages, sem, segments in sites
    ]


def rsd_western_cape_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "e073c802152cf95ffa4c3ec806d73e0d230ba130e69c40f8822c28f61d7c2393"
    names = [
        "Cape Town Central: Gardens", "Claremont", "R44 Strand", "Bellville",
        "Seapoint", "M5 Rondebosch", "Cape Town Central: Upper Loop", "Kenilworth",
        "Marine Drive South", "Milnerton", "N2 Somerset West", "Newlands",
        "Marine Drive North", "R27 Bloubergstrand", "M5 Mowbray",
        "Marine Drive Central", "N2 Athlone", "N2 Vanguard", "N2 Bridgetown",
        "N2 Settlers Way", "Tokai", "N2 Helderberg", "N2 Somerset Central",
        "N2 Airport Industria", "Cavendish", "Century City", "N7 Canal Walk",
        "Paarl South",
    ]
    return [
        entry(
            source,
            f"pptx:slides={index * 2}-{index * 2 + 1};site-code=WCD{index:03d}",
            f"WCD{index:03d} - {name}",
            channel="DOOH",
            publisher="Tractor",
            province="Western Cape",
            site_code=f"WCD{index:03d}",
            site_name=name,
            rate="NOT_STATED",
            demographics="SOURCE_PRESENT",
            points_of_interest="SOURCE_PRESENT_NOT_TRANSCRIBED_AS_INVENTORY",
            source_context="deck title states Western Cape Roadside Digital Site Bible; source filename is labelled 2025",
        )
        for index, name in enumerate(names, start=1)
    ]
