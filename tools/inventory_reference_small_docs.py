"""Human-reviewed reference entries for compact inventory documents."""

from __future__ import annotations

from collections.abc import Callable
from typing import Any


EntryFactory = Callable[..., dict[str, Any]]


def mamg_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "431507e0ee69f124aed0e49df632945446675cdc5b9bfa6e39fdef0d5987c0fc"
    shared = {"channel": "OOH", "currency": "ZAR", "product": "Multi-functional LED truck"}
    result: list[dict[str, Any]] = []
    page_two = [
        ("Truck branding option 1", "32500.00", None, "artwork printing; artwork application"),
        ("Truck branding option 2", "40625.00", None, "artwork design; artwork printing; artwork application"),
        ("Truck stripping", "12500.00", None, "removing approved artwork"),
        ("Truck hiring and flighting - daily", "35000.00", "full working day", "driver; operations assistant"),
        ("Truck hiring and flighting - weekly", "175000.00", "5 days", "driver; operations assistant"),
        ("Truck hiring and flighting - monthly", "700000.00", "20 days", "driver; operations assistant"),
        ("Truck live feeds - daily", "5040.00", "full working day", "switcher"),
        ("Truck live feeds - weekly", "25200.00", "5 days", "switcher"),
        ("Truck live feeds - monthly", "100800.00", "20 days", "switcher"),
    ]
    for index, (name, rate, duration, included) in enumerate(page_two, start=1):
        result.append(entry(source, f"pdf:page=2;offer={index}", name, rate=rate,
                            duration=duration, included=included, **shared))
    services = [
        ("Brand ambassadors (promoters)", "PER_HOUR", "550.00"),
        ("DJ", "PER_DAY", "6500.00"), ("MC", "PER_DAY", "7000.00"),
        ("On site Medic", "PER_DAY", "3200.00"), ("VIP protection", "PER_DAY", "4500.00"),
        ("Roving Camera", "PER_DAY", "7500.00"), ("Photography", "PER_DAY", "9500.00"),
        ("Social media marketing", "PER_MONTH", "15000.00"),
        ("Sound & Visual technician", "PER_DAY", "6000.00"),
        ("Videography & editing", "PER_DAY", "11500.00"),
        ("PA system, stage & trussing", "PER_DAY", "RATE_ON_REQUEST"),
    ]
    modifiers = "branding includes flighting and maintenance; optional MAMG artwork production costs 25% of total branding fee; first artwork change free; second change costs 12% of branding fee including artwork"
    for index, (name, basis, rate) in enumerate(services, start=1):
        result.append(entry(source, f"pdf:page=3;service-row={index}", name,
                            rate=rate, charging_basis=basis, source_terms=modifiers, **shared))
    return result


def smile_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "4284b7c25e4b6797246591184af1c90e0687542eb53b07f726a11eb1b4dece68"
    shared = {"currency": "ZAR", "vat": "EXCLUDED", "package": "Impact Plus", "duration": "4 weeks"}
    components = [
        ("Generics", "RADIO", "25 spots", "60960", "15240", "45720", "weeks 1-2: 13 then 12 spots"),
        ("Feature sponsorship", "RADIO", "10 sponsorships", "59250", "14813", "44438", "week 3 AM drive; week 4 PM drive; opening and closing billboards"),
        ("Social media Facebook & X", "SOCIAL", "8 posts", "28000", "2800", "25200", "one X and one Facebook post weekly"),
        ("Social media Facebook boosting", "SOCIAL", "4 boosted posts", "2000", "0", "2000", "one Facebook post weekly boosted at R500 each"),
    ]
    result = [
        entry(source, f"pdf:page=1;detail-page={index + 1}", f"Smile 90.4FM Impact Plus - {name}",
              channel=channel, quantity=quantity, gross_value=gross, discount=discount,
              net_investment=net, schedule=schedule, **shared)
        for index, (name, channel, quantity, gross, discount, net, schedule) in enumerate(components, start=1)
    ]
    result.append(entry(source, "pdf:pages=1-5", "Smile 90.4FM Impact Plus package",
                        channel="MULTI_CHANNEL", gross_value="150210", discount="32853",
                        net_investment="117358", quantity="43 total exposures",
                        relationship="contains four enumerated components",
                        payment_terms="hard costs, presenter fees and prizes payable upfront", **shared))
    return result


def home_channel_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "d735f3ebf1853ae8e83836774b5dd8e256d5a2dd9f7a70cac1d6e314e57c171b"
    offers = [
        ("Monday-Friday", "08H00-12H00", "Morning", "3500"),
        ("Monday-Friday", "12H00-15H00", "Midday", "4000"),
        ("Monday-Friday", "15H00-18H00", "Premium shoulder time", "5000"),
        ("Monday-Friday", "18H00-22H00", "Prime time", "6000"),
        ("Monday-Friday", "22H00-24H00", "Premium shoulder time", "5000"),
        ("Saturday-Sunday", "07H00-09H00", "Early morning", "3500"),
        ("Saturday-Sunday", "09H00-12H00", "Mid morning", "5000"),
        ("Saturday-Sunday", "12H00-18H00", "Prime", "6000"),
        ("Saturday-Sunday", "18H00-24H00", "Premium shoulder time", "5000"),
    ]
    return [
        entry(source, f"pdf:page=3;rate-row={index}", f"The Home Channel 176 - {day} {band}",
              channel="TELEVISION", station="The Home Channel 176", day_scope=day,
              time_band=band, segment=segment, rate=rate, currency="ZAR",
              charging_basis="PER_30_SECOND_SPOT", vat="EXCLUDED",
              agency_settlement_discount="EXCLUDED", programme_specific_loading="20%",
              average_spot_rate_context="5000")
        for index, (day, band, segment, rate) in enumerate(offers, start=1)
    ]


def y_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "993575fcaf9af64ba43214c5cb642fc9ad47aa1987cfb8fd84ec00ae703aea42"
    offers = [
        ("E-commerce package", "live reads + digital", "1 week", "120875", "22382"),
        ("50 for 50 package", "generics", "4 weeks", "302200", "51374"),
        ("100 for 100 package", "generics + digital", "4 weeks", "601400", "118252"),
    ]
    return [
        entry(source, f"pdf:page=1;package={index}", name, channel="MULTI_CHANNEL",
              components=components, duration=duration, gross_value=value,
              net_investment=investment, currency="ZAR")
        for index, (name, components, duration, value, investment) in enumerate(offers, start=1)
    ]


def virgin_active_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "8adeb8c5aade108ae17b71889414d74e3d7636c09e1d0fa77c234453f49c8274"
    return [
        entry(source, "pdf:page=5;site=RVDN01", "National Virgin Active Digital Network",
              channel="DOOH", site_code="RVDN01", scope="40 clubs nationwide",
              location="high-traffic areas in clubs", screen_size="1.44m H x 2.56m W",
              rate="250000.00", currency="ZAR", impressions="9200 per day",
              traffic="1460000 per month", slot_duration="15 seconds",
              loop="16 clients; 4 minutes", audience="LSM 7-10",
              restriction="all campaigns require Virgin Active pre-approval"),
    ]


def ignition_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "1cdca7e2c3a4873a0313f8a1050d07f184758fe3b1a97adebb3275cea806bae6"
    offers = [
        ("Saturday", "24H00-08H00", "Early morning off peak", "3000"),
        ("Saturday", "08H00-24H00", "All day weekend prime", "4500"),
        ("Sunday", "24H00-06H00", "Early morning off peak", "3000"),
        ("Sunday", "06H00-24H00", "All day weekend prime", "4500"),
        ("Monday-Friday", "24H00-18H00", "All day off peak", "3000"),
        ("Monday-Friday", "18H00-23H30", "Prime time", "4500"),
        ("Monday-Friday", "23H30-24H00", "Late night off peak", "3000"),
    ]
    return [
        entry(source, f"pdf:page=2;rate-row={index}", f"Ignition 189 - {day} {band}",
              channel="TELEVISION", station="Ignition 189", day_scope=day, time_band=band,
              segment=segment, rate=rate, currency="ZAR", charging_basis="PER_30_SECOND_SPOT",
              vat="EXCLUDED", agency_settlement_discount="EXCLUDED",
              programme_specific_loading="20%", average_spot_rate_context="3750")
        for index, (day, band, segment, rate) in enumerate(offers, start=1)
    ]


def soweto_screens_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "bc3015af60c2449251fe9666ea708b0b33a46103330ba401d7a5ac456a2c9bff"
    sites = [
        ("Wanderers Taxi Rank", "Gauteng", "6 x 3", "150000", "60000", "1"),
        ("Noord Taxi Rank", "Gauteng", "6 x 9", "1000000", "85000", "1"),
        ("Soweto Theater", "Gauteng", "6 x 3", "150000", "60000", "1"),
        ("Chris Hani Road", "Gauteng", "8 x 5", "90000 cars", "85000", "1"),
        ("Bara Taxi Rank", "Gauteng", "6 x 3", "350000", "65000", "1"),
        ("Randburg Taxi Rank", "Gauteng", "6 x 3", "150000", "60000", "1"),
        ("Midrand Taxi Rank", "Gauteng", "6 x 3", "150000", "60000", "1"),
        ("Shoshanguve Taxi Rank", "Gauteng", "6 x 3", "150000", "60000", "1"),
        ("Germiston Taxi Rank", "Gauteng", "6 x 3", "150000", "60000", "1"),
        ("Bree (Lilian Ngoyi) Taxi Rank", "Gauteng", "6 x 3", "400000", "60000", "1"),
        ("Kwamashu Taxi Rank", "KZN", "5 x 3", "150000", "60000", "1"),
        ("Thohoyandou Taxi Rank", "Limpopo", "6 x 3", "150000", "60000", "1"),
        ("Mbombela Taxi Rank (White River)", "Nelspruit", "6 x 3", "150000", "60000", "1"),
        ("Majakathata Taxi Rank", "Bloem", "6 x 3", "150000", "60000", "1"),
        ("Mfuleni Taxi Rank", "CT", "6 x 3", "450000", "50000", "2"),
        ("Langa Taxi Rank", "CT", "2 x 2", "450000", "50000", "1"),
        ("Nyanga Taxi Rank", "CT", "6 x 3", "450000", "50000", "1"),
        ("Gugulethu Taxi Rank", "CT", "6 x 3", "450000", "50000", "1"),
    ]
    return [
        entry(source, f"pdf:summary-pages=6-7;site-row={index}", name,
              channel="DOOH", location=location, screen_size_metres=size,
              daily_viewership=viewership, monthly_rate=rate, currency="ZAR",
              screen_count=count, advert_length="15-60 seconds",
              standard_frequency="every 3-5 minutes; adjustable")
        for index, (name, location, size, viewership, rate, count) in enumerate(sites, start=1)
    ]


def direct_kaya_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    source = "f9a8fdef3602f926119cdad6c9dba0c5963c7a50e88af3a63d8db21f97fbf17e"
    offers = [
        ("Lunch Time", "2", "16", "91920", "68920", "23000", "23000"),
        ("Workzone", "3", "20", "133950", "104950", "33350", "29000"),
        ("Weekend Package", "4", "32", "75840", "53840", "22000", "22000"),
        ("Retail Package", "5", "16", "106770", "74770", "32000", "32000"),
        ("Powerweek", "6", "38", "255240", "179240", "76000", "76000"),
    ]
    return [
        entry(source, f"pdf:page=1;detail-page={page}", f"Kaya 959 - {name}",
              channel="RADIO", station="Kaya 959", quantity=f"{spots} spots",
              duration="2 weeks", gross_value=gross, discount=discount,
              summary_net_investment=summary_net, detailed_schedule_net=detail_net,
              currency="ZAR", vat="EXCLUDED", rate_card_basis="November 2024",
              ambiguity=("summary states R33,350 while detailed schedule states R29,000"
                         if summary_net != detail_net else None))
        for name, page, spots, gross, discount, summary_net, detail_net in offers
    ]
