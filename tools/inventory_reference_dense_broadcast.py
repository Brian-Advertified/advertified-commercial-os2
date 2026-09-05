"""Source-led enumeration for dense Commvibe and SABC rate matrices."""

from __future__ import annotations

import re
from typing import Any, Callable


COMM_VIBE = "ed68a16013d01c8363f2247c333ae9acf3a4ec0e15a3ab8fff73acb42f62247c"
SABC_TV = "ee4d6bec165d4cd47d28773db89064bc113dd53b8b8f65538a07853258e8c5b1"
SABC_RADIO = "beff939cae1ab50a198de0812b9b82de0c50a063cfc7b37db365cf33691e8cc6"
BROADCAST_HASHES = {COMM_VIBE, SABC_TV, SABC_RADIO}
MONEY = re.compile(r"R\s*\d+(?:[ ,]\d{3})*(?:[.,]\d{2})?", re.IGNORECASE)
TIME_BAND = re.compile(r"^\d{1,2}:\d{2}\s*-\s*\d{1,2}:\d{2}\*?$")
NUMBER = re.compile(r"^\d[\d ]*(?:[,.]\d{2})?$")
DAY_MARKERS = {"MON-FRI*": "MONDAY_FRIDAY", "SATURDAY*": "SATURDAY", "SUNDAY*": "SUNDAY"}


def broadcast_reference_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    if source_hash == COMM_VIBE:
        return commvibe_entries(make_entry, source_hash, source)
    if source_hash == SABC_TV:
        return sabc_tv_entries(make_entry, source_hash, source)
    return sabc_radio_entries(make_entry, source_hash, source)


def commvibe_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    result = []
    for page in range(2, 12):
        fragments = page_fragments(source, page)
        data = max(fragments, key=lambda item: len(item.get("text", "")))
        region = next(
            (clean(item.get("text")) for item in fragments
             if len(clean(item.get("text"))) < 40 and "Disclaimer" not in clean(item.get("text"))),
            f"page {page}",
        )
        result.extend(commvibe_page_entries(
            make_entry, source_hash, page, region, data
        ))
    return result


def commvibe_page_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    page: int,
    region: str,
    fragment: dict[str, Any],
) -> list[dict[str, Any]]:
    lines = [clean(line) for line in fragment.get("text", "").splitlines() if clean(line)]
    result = []
    day = "NOT_STATED"
    index = 0
    while index < len(lines):
        value = lines[index]
        if value in DAY_MARKERS:
            day = DAY_MARKERS[value]
            index += 1
            continue
        if not TIME_BAND.fullmatch(value):
            index += 1
            continue
        time_band = value.rstrip("*")
        index += 1
        column = 0
        while index < len(lines) and lines[index] not in DAY_MARKERS and not TIME_BAND.fullmatch(lines[index]):
            rate = lines[index]
            if rate == "-" or NUMBER.fullmatch(rate):
                column += 1
                if rate != "-":
                    result.append(make_entry(
                        source_hash,
                        f"{fragment['locator']};day={day};time={time_band};column={column}",
                        f"{region} station column {column} {day} {time_band}",
                        region=region,
                        station_column=column,
                        station_name="VISUAL_LOGO_COLUMN",
                        day_scope=day,
                        time_band=time_band,
                        rate=normalize_number(rate),
                        currency="ZAR",
                        ambiguity="Station identity is a source-visible logo-only column.",
                    ))
            index += 1
    return result


def sabc_tv_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    result = sabc_tv_packages(make_entry, source_hash, source)
    schedule_pages = {10, 11, 12, 15, 16, 17, 20, 21, 22, 24, 25, 26}
    for table in source.get("tables", []):
        if int(table.get("page") or 0) in schedule_pages:
            result.extend(sabc_schedule_table(make_entry, source_hash, table))
    result.extend(sabc_t_rate_entries(make_entry, source_hash, source))
    if len(result) != 3351:
        raise ValueError(f"SABC TV enumeration expected 3351 entries, found {len(result)}")
    return result


def sabc_tv_packages(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    fixed = [
        (2, "Dominance Reach 4-week package", "490000", "100", "1000000", "76000"),
        (2, "Builder Reach 2-week package", "250000", "50", "400000", "38000"),
        (2, "Boost Reach 1-week package", "150000", "25", "240000", "19000"),
        (4, "Lots of Spots 4-week SABC1 SABC2 and S3", "600100", "170", "NOT_STATED", "3289450"),
        (5, "Channel package 4-week SABC Sport", "100035", "285", "NOT_STATED", "2215000"),
        (6, "Channel package 4-week SABC News", "275040", "240", "NOT_STATED", "1416000"),
        (7, "Weekly S3 and SABC News Channel", "130020", "60", "NOT_STATED", "403000"),
        (7, "Reach Maximiser weekly SABC News Channel", "105540", "30", "NOT_STATED", "312500"),
    ]
    return [
        make_entry(
            source_hash,
            f"source-map:page={page};package={index}",
            name,
            package_name=name,
            rate=rate,
            currency="ZAR",
            quantity_spots=spots,
            guaranteed_digital_impressions=impressions,
            included_or_exposure_value=extra,
            source_context=page_text(source, page),
        )
        for index, (page, name, rate, spots, impressions, extra) in enumerate(fixed, start=1)
    ]


def sabc_schedule_table(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    table: dict[str, Any],
) -> list[dict[str, Any]]:
    rows = table.get("rows", [])
    headers = [clean(cell) for cell in rows[0]] if rows else []
    result = []
    for row_number, row in enumerate(rows[1:], start=2):
        time_slot = clean(row[0]) if row else "NOT_STATED"
        for column, cell in enumerate(row[1:], start=2):
            text = clean(cell)
            for occurrence, match in enumerate(MONEY.finditer(text), start=1):
                programme = clean(text[:match.start()]) or "NOT_STATED"
                date = headers[column - 1] if column <= len(headers) else "NOT_STATED"
                result.append(make_entry(
                    source_hash,
                    f"{table['locator']};row={row_number};column={column};rate={occurrence}",
                    f"{date} {programme} {time_slot}",
                    date=date,
                    time_slot=time_slot,
                    programme=programme,
                    rate=normalize_number(match.group(0)),
                    currency="ZAR",
                    source_cell=text,
                ))
    return result


def sabc_t_rate_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    table = next(item for item in source["tables"] if int(item.get("page") or 0) == 27)
    groups = ((0, (1, 2, 3, 4, 5)), (7, (8, 9, 10, 11, 12)), (14, (15, 16, 18, 19, 20)))
    durations = (30, 25, 20, 15, 10)
    result = []
    for row_number, row in enumerate(table["rows"][1:], start=2):
        for block, (code_column, rate_columns) in enumerate(groups, start=1):
            code = clean(row[code_column]) if code_column < len(row) else ""
            for duration, column in zip(durations, rate_columns):
                rate = clean(row[column]) if column < len(row) else ""
                if not NUMBER.fullmatch(rate):
                    continue
                identity = code or f"SOURCE_CODE_MISSING_ROW_{row_number}_BLOCK_{block}"
                result.append(make_entry(
                    source_hash,
                    f"{table['locator']};row={row_number};block={block};duration={duration}",
                    f"{identity} {duration} seconds",
                    t_rate_code=identity,
                    duration_seconds=duration,
                    rate=normalize_number(rate),
                    currency="ZAR",
                    ambiguity="T-rate code absent in source table extraction" if not code else "NONE",
                ))
    return result


def sabc_radio_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    result = []
    station_pages = {5, 6, 7, *range(9, 21), *range(22, 26)}
    for page in sorted(station_pages):
        tables = [item for item in source["tables"] if int(item.get("page") or 0) == page]
        result.extend(sabc_station_table(
            make_entry, source_hash, page, page_station(source, page), tables
        ))
    for table in source.get("tables", []):
        page = int(table.get("page") or 0)
        if 27 <= page <= 32:
            result.extend(commercial_rows(make_entry, source_hash, table))
    result.extend(surcharge_rows(make_entry, source_hash, source))
    return result


def sabc_station_table(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    page: int,
    station: str,
    tables: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    rate_table = tables[0]
    multiplier = tables[1]["rows"] if len(tables) > 1 else []
    result = []
    days = ("MONDAY_FRIDAY", "SATURDAY", "SUNDAY")
    for row_number, row in enumerate(rate_table.get("rows", [])[1:], start=2):
        for block, day in enumerate(days):
            time_column = block * 2
            rate_column = time_column + 1
            time_band = clean(row[time_column]) if time_column < len(row) else ""
            rate = clean(row[rate_column]) if rate_column < len(row) else ""
            if not time_band or not NUMBER.fullmatch(rate):
                continue
            result.append(make_entry(
                source_hash,
                f"{rate_table['locator']};row={row_number};day={day}",
                f"{station} {day} {time_band}",
                station=station,
                day_scope=day,
                time_band=time_band,
                rate=normalize_number(rate),
                currency="ZAR",
                duration_multiplier_table=multiplier,
            ))
    return result


def commercial_rows(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    table: dict[str, Any],
) -> list[dict[str, Any]]:
    rows = table.get("rows", [])
    headers = [clean(cell) for cell in rows[0]] if rows else []
    result = []
    for row_number, row in enumerate(rows[1:], start=2):
        cells = [clean(cell) for cell in row]
        rates = MONEY.findall(" | ".join(cells))
        if not rates:
            continue
        identity = next((cell for cell in cells if cell and not MONEY.fullmatch(cell)), "NOT_STATED")
        result.append(make_entry(
            source_hash,
            f"{table['locator']};row={row_number}",
            identity,
            source_fields=dict(zip(headers, cells)),
            source_rates=rates,
            currency="ZAR",
        ))
    return result


def surcharge_rows(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    table = next(item for item in source["tables"] if int(item.get("page") or 0) == 33)
    return [
        make_entry(
            source_hash,
            f"{table['locator']};row={number}",
            clean(row[0]),
            source_fields=[clean(cell) for cell in row],
            rate="PERCENTAGE_SURCHARGE",
        )
        for number, row in enumerate(table["rows"][1:], start=2)
        if row and clean(row[0])
    ]


def page_station(source: dict[str, Any], page: int) -> str:
    text = next(
        (clean(item.get("text")) for item in source["fragments"]
         if int(item.get("ordinal") or 0) == page and item.get("kind") == "TEXT"),
        f"Station page {page}",
    )
    match = re.match(r"(.{2,60}?(?:FM|fm|Africa|2000|RSG|SAfm|5FM))\b", text)
    return clean(match.group(1)) if match else clean(text.split(" is ", 1)[0])


def page_fragments(source: dict[str, Any], page: int) -> list[dict[str, Any]]:
    return [
        item for item in source.get("fragments", [])
        if int(item.get("ordinal") or 0) == page and item.get("kind") == "TEXT"
    ]


def page_text(source: dict[str, Any], page: int) -> str:
    return "\n".join(clean(item.get("text")) for item in page_fragments(source, page))


def normalize_number(value: str) -> str:
    raw = re.sub(r"[^0-9,.]", "", value)
    if "," in raw and "." not in raw and len(raw.rsplit(",", 1)[-1]) == 2:
        return raw.replace(".", "").replace(",", ".")
    return raw.replace(",", "")


def clean(value: Any) -> str:
    return re.sub(r"\s+", " ", str(value or "")).strip()
