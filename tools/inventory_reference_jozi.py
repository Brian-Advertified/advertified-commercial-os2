"""Human-reviewed reference entries for the three Jozi FM package PDFs."""

from __future__ import annotations

from collections.abc import Callable
from typing import Any


EntryFactory = Callable[..., dict[str, Any]]


def jozi_entries(entry: EntryFactory) -> list[dict[str, Any]]:
    preroll = "41b83d0d780dad7cb298babad11581560bc770252373706f24e72e1fcc190030"
    sponsorship = "67ac61bf7dc0af5e9ade722cee683cc9884dac44cd0507ca3a596509d674c7ab"
    generic = "7f26bb48a0c3a6cd15961d9373212e96f528f0154ef64720f021c4c643dc0922"
    shared = {
        "channel": "RADIO", "currency": "ZAR",
        "vat": "EXCLUDED_FROM_STATED_NET_VALUES", "billing_period": "6 months",
    }
    return [
        entry(preroll, "pdf:page=1;page=2", "Jozi FM generic 30 second component",
              **shared, gross_value="478860.00", net_investment="239430.00",
              quantity="180 spots", schedule="Monday-Sunday"),
        entry(preroll, "pdf:page=1;page=3", "KAYA STREAM pre-roll component",
              **shared, gross_value="97500.00", net_investment="97500.00",
              summary_exposure="150000 pre-rolls", scheduled_quantity="6 spots",
              source_wording="25,000 x 30 second recorded pre-rolls per month",
              ambiguity="Jozi package names this component KAYA STREAM; exposure and scheduled spots are distinct measures"),
        entry(preroll, "pdf:pages=1-3", "Jozi generic and streaming pre-roll package",
              **shared, gross_value="576360.00", net_investment="336930.00",
              monthly_cost="56155.00", relationship="contains generic and streaming pre-roll components",
              ambiguity="filename says 2026; source package and campaign text say 2025"),
        entry(sponsorship, "pdf:page=1;page=2", "Jozi FM generic 30 second component",
              **shared, gross_value="327240.00", net_investment="163620.00",
              quantity="120 spots", schedule="Monday-Sunday"),
        entry(sponsorship, "pdf:page=1;page=3", "Jozi FM news/weather/traffic/sport sponsorship component",
              **shared, gross_value="249975.00", net_investment="124987.50",
              quantity="60 sponsorships", schedule="Monday-Friday"),
        entry(sponsorship, "pdf:pages=1-3", "Jozi Plan A generic and sponsorship package",
              **shared, gross_value="577215.00", stated_summary_net_investment="288608",
              exact_component_net_sum="288607.50", monthly_cost="48101",
              relationship="contains generic and sponsorship components",
              restrictions="limited to 15; six consecutive months; no changes or cancellations; mid-month flighting excludes December"),
        entry(generic, "pdf:page=1;page=2", "Jozi FM generic 30 second component",
              **shared, gross_value="478860.00", net_investment="239430.00",
              quantity="180 spots", schedule="Monday-Sunday"),
        entry(generic, "pdf:pages=1-2", "Jozi Plan A generic-only package",
              **shared, gross_value="478860.00", net_investment="239430.00",
              monthly_cost="39905.00", relationship="contains the generic component",
              restrictions="limited to 15; six consecutive months; no changes or cancellations; mid-month flighting excludes December"),
    ]
