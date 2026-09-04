"""Deterministic physical inventory transcription from immutable source maps.

The transcriber groups raw physical facts into inventory rows. It does not use
Bedrock and does not infer missing prices, dates, buying bases, or availability.
"""

from __future__ import annotations

import hashlib
import json
import re
from decimal import Decimal, InvalidOperation
from typing import Any, Iterable

MONEY = re.compile(r"(?<![A-Za-z])(?:ZAR|R)\s*\d[\d\s.,\u00a0]*", re.I)
TIME = re.compile(r"\b\d{1,2}:\d{2}\s*[-–]\s*\d{1,2}:\d{2}\b")
CODE = re.compile(r"\b(?=[A-Z0-9 -]{4,20}\b)(?=[A-Z0-9 -]*[A-Z])(?=[A-Z0-9 -]*\d)[A-Z]{1,8}[- ]?\d{2,8}[A-Z]{0,5}\b")
SITE_LABEL = re.compile(r"(?is)\b(?:site\s*(?:number|no\.?|code)|screen\s*(?:number|no\.?))\s*[:#-]?\s*([A-Z0-9][A-Z0-9 _/-]{2,24})")
DIMENSION = re.compile(r"(?i)\b\d+(?:[.,]\d+)?\s*m?\s*[x×]\s*\d+(?:[.,]\d+)?\s*m?\b")
DECIMAL_GPS = re.compile(r"(?<!\d)([-+]?(?:[1-8]?\d(?:\.\d+)?|90(?:\.0+)?))\s*[,; ]+\s*([-+]?(?:1[0-7]\d(?:\.\d+)?|\d?\d(?:\.\d+)?|180(?:\.0+)?))(?!\d)")
UNKNOWN_RATE = re.compile(r"(?i)\b(?:rate|price|cost)\s*(?::|is)?\s*(?:on\s+request|tbc|poa)\b")

IDENTITY_HEADERS = {
    "name", "product", "productname", "platform", "adunit", "site",
    "sitename", "sitenumber", "sitecode", "code", "location", "placement",
    "package", "packagename", "programme", "program", "title", "element",
}
RATE_HEADERS = {
    "rate", "rates", "price", "cost", "baseprice", "ratecard",
    "discountedrate", "netrate", "netrates", "packagecost", "value",
}
KV_LABELS = {
    "description", "area", "cityprov", "cityprovince", "trafficcount",
    "impacts", "impressions", "frequency", "type", "format", "driversside",
    "gps", "gpscoordinate", "ratecard", "discountedrate", "printing",
    "flighting", "targetmall", "ara", "notes", "sitenumber", "sitecode",
    "size", "illuminated", "production", "audiencereach", "audience",
    "availability", "siteinfo", "location", "province", "city",
}
NOISE = re.compile(r"(?i)\b(?:telephone|phone|tel\.?|fax|vat\s*(?:number|no\.)|registration\s+number|liability|penalty)\b")
NARRATIVE = re.compile(r"(?i)\b(?:about\s+us|who\s+we\s+are|introduction|contact\s+us|thank\s+you|terms\s+and\s+conditions)\b")


def transcribe_document(document: dict[str, Any], source_map: dict[str, Any]) -> dict[str, Any]:
    source_hash = str(document["sha256"])
    file_name = str(document["relativePath"])
    rows: list[dict[str, Any]] = []
    for table in source_map.get("tables") or []:
        rows.extend(transcribe_table(file_name, source_hash, table))
    rows.extend(transcribe_fragments(file_name, source_hash, source_map.get("fragments") or []))
    rows = merge_and_deduplicate(rows)
    return {
        "schemaVersion": "advertified.inventory-physical-transcription.v1",
        "sourceHash": source_hash,
        "fileName": file_name,
        "documentClass": str(source_map.get("format") or document.get("documentClass") or ""),
        "supplierName": supplier_for(file_name),
        "channelHint": channel_for(file_name),
        "sourceMapExtractorVersion": source_map.get("extractorVersion"),
        "sourceMapCounts": source_map.get("counts") or {},
        "rowCount": len(rows),
        "rows": rows,
    }


def transcribe_table(file_name: str, source_hash: str, table: dict[str, Any]) -> list[dict[str, Any]]:
    rows = normalized_rows(table)
    if not rows:
        return []
    locator = str(table.get("locator") or "")
    ordinal = source_ordinal(locator, table)
    if is_vertical(rows):
        return vertical_rows(file_name, source_hash, rows, locator, ordinal)
    radio = radio_rows(file_name, source_hash, rows, locator, ordinal)
    if radio:
        return radio
    headered = headered_rows(file_name, source_hash, rows, locator, ordinal)
    if headered:
        return headered
    return headerless_rows(file_name, source_hash, rows, locator, ordinal)


def vertical_rows(
    file_name: str, source_hash: str, rows: list[list[str]], locator: str, ordinal: int | None
) -> list[dict[str, Any]]:
    pairs = [(normalize(row[0]), row[1].strip(), index + 1) for index, row in enumerate(rows) if len(row) >= 2 and row[0].strip()]
    values = {key: value for key, value, _ in pairs if value}
    identity = first(values, "sitenumber", "sitecode", "location", "area", "description")
    if not identity:
        return []
    evidence = [ev(key, value, f"{locator};row={index};column=2") for key, value, index in pairs if value]
    preferred_rate = first(values, "discountedrate", "ratecard", "price", "cost")
    rate_raw = preferred_rate if preferred_rate and (MONEY.search(preferred_rate) or is_unknown(preferred_rate)) else None
    return [make_row(
        file_name, source_hash, locator, ordinal,
        identity=identity,
        rate_raw=rate_raw,
        geography=join_values(values, "area", "cityprov", "cityprovince", "city", "province"),
        description=first(values, "description", "siteinfo"),
        dimensions=first(values, "size", "dimensions", "format"),
        format_raw=first(values, "format", "type"),
        latitude=coordinate(values, 0),
        longitude=coordinate(values, 1),
        availability=first(values, "availability"),
        evidence=evidence,
        extras=values,
    )]


def radio_rows(
    file_name: str, source_hash: str, rows: list[list[str]], locator: str, ordinal: int | None
) -> list[dict[str, Any]]:
    header_index = next((i for i, row in enumerate(rows[:6]) if sum(normalize(v) in {"timeband", "netrate", "netrates"} for v in row) >= 2), None)
    if header_index is None:
        return []
    header = rows[header_index]
    day_groups = ("MONDAY_FRIDAY", "SATURDAY", "SUNDAY")
    result: list[dict[str, Any]] = []
    pair = 0
    column = 0
    while column + 1 < len(header):
        if normalize(header[column]) == "timeband" and normalize(header[column + 1]) in {"netrate", "netrates"}:
            day_group = day_groups[pair] if pair < 3 else f"DAY_GROUP_{pair + 1}"
            for row_index, row in enumerate(rows[header_index + 1:], start=header_index + 2):
                time_band = cell(row, column)
                raw_rate = cell(row, column + 1)
                if not TIME.search(time_band) or not numeric_rate(raw_rate):
                    continue
                result.append(make_row(
                    file_name, source_hash,
                    f"{locator};row={row_index};column={column + 2}", ordinal,
                    identity=f"{day_group} - {time_band}",
                    rate_raw=raw_rate,
                    placement="Radio spot",
                    daypart=time_band,
                    evidence=[
                        ev("dayGroup", day_group, locator),
                        ev("daypart", time_band, f"{locator};row={row_index};column={column + 1}"),
                        ev("rate", raw_rate, f"{locator};row={row_index};column={column + 2}"),
                    ],
                    extras={"dayGroup": day_group, "implicitCurrency": "ZAR"},
                ))
            pair += 1
            column += 2
        else:
            column += 1
    return result


def headered_rows(
    file_name: str, source_hash: str, rows: list[list[str]], locator: str, ordinal: int | None
) -> list[dict[str, Any]]:
    selected: tuple[int, int] | None = None
    for index, row in enumerate(rows[:10]):
        headers = [normalize(value) for value in row]
        score = sum(value in IDENTITY_HEADERS or value in RATE_HEADERS for value in headers)
        if selected is None or score > selected[1]:
            selected = (index, score)
    if selected is None or selected[1] < 2:
        return []
    header_index = selected[0]
    headers = [normalize(value) for value in rows[header_index]]
    identity_index = index_of(headers, IDENTITY_HEADERS)
    rate_indices = [index for index, value in enumerate(headers) if value in RATE_HEADERS]
    if identity_index is None or not rate_indices:
        return []
    result: list[dict[str, Any]] = []
    for row_number, data in enumerate(rows[header_index + 1:], start=header_index + 2):
        identity = cell(data, identity_index)
        if not useful_identity(identity):
            continue
        rate_cells = [(index, cell(data, index)) for index in rate_indices if cell(data, index)]
        if not rate_cells:
            continue
        for rate_index, rate_value in rate_cells:
            names = split_lines(identity)
            rates = split_rate_values(rate_value)
            pairs = align(names, rates)
            for item_index, (name, raw_rate) in enumerate(pairs, start=1):
                if not (MONEY.search(raw_rate) or numeric_rate(raw_rate) or is_unknown(raw_rate)):
                    continue
                row_locator = f"{locator};row={row_number};column={rate_index + 1};item={item_index}"
                extras = {headers[index]: cell(data, index) for index in range(min(len(headers), len(data))) if cell(data, index)}
                result.append(make_row(
                    file_name, source_hash, row_locator, ordinal,
                    identity=name,
                    rate_raw=raw_rate,
                    geography=join_values(extras, "geography", "location", "city", "province", "country"),
                    description=extras.get("description"),
                    placement=extras.get("placement") or extras.get("adunit"),
                    format_raw=extras.get("format"),
                    dimensions=extras.get("dimensions") or extras.get("size"),
                    evidence=[ev(headers[index], cell(data, index), f"{locator};row={row_number};column={index + 1}") for index in range(min(len(headers), len(data))) if cell(data, index)],
                    extras=extras,
                ))
    return result


def headerless_rows(
    file_name: str, source_hash: str, rows: list[list[str]], locator: str, ordinal: int | None
) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    for row_number, data in enumerate(rows, start=1):
        joined = " | ".join(data)
        rates = [match.group(0).strip() for match in MONEY.finditer(joined)]
        if not rates:
            continue
        identity = next((value for value in data if useful_identity(value) and not MONEY.fullmatch(value.strip())), "")
        if not identity or NOISE.search(identity):
            continue
        for index, raw_rate in enumerate(rates, start=1):
            result.append(make_row(
                file_name, source_hash, f"{locator};row={row_number};price={index}", ordinal,
                identity=identity,
                rate_raw=raw_rate,
                description=joined,
                evidence=[ev("row", joined, f"{locator};row={row_number}")],
                extras={},
            ))
    return result


def transcribe_fragments(file_name: str, source_hash: str, fragments: list[dict[str, Any]]) -> list[dict[str, Any]]:
    grouped: dict[int, list[dict[str, Any]]] = {}
    for item in fragments:
        ordinal = as_int(item.get("ordinal")) or as_int(item.get("page")) or ordinal_from_locator(str(item.get("locator") or ""))
        if ordinal is not None:
            grouped.setdefault(ordinal, []).append(item)
    result: list[dict[str, Any]] = []
    for ordinal, items in sorted(grouped.items()):
        result.extend(page_card_rows(file_name, source_hash, items, ordinal))
        result.extend(adjacent_price_rows(file_name, source_hash, items, ordinal))
        result.extend(unpriced_location_rows(file_name, source_hash, items, ordinal))
    return result


def page_card_rows(file_name: str, source_hash: str, items: list[dict[str, Any]], ordinal: int) -> list[dict[str, Any]]:
    text = "\n".join(str(item.get("text") or "") for item in items)
    site_match = SITE_LABEL.search(text)
    codes = [clean(match.group(1).splitlines()[0]) for match in [site_match] if match]
    if not codes and (DIMENSION.search(text) or DECIMAL_GPS.search(text)):
        codes = [clean(match.group(0)) for item in items for match in CODE.finditer(str(item.get("text") or "")) if valid_code(match.group(0))]
    codes = unique(codes)
    if not codes:
        return []
    rate = preferred_page_rate(items)
    location = first_page_value(items, "location", "area", "city", "province")
    description = first_page_value(items, "site info", "description")
    dimensions = next((match.group(0) for match in DIMENSION.finditer(text)), None)
    latitude, longitude = decimal_coordinates(text)
    availability = first_page_value(items, "availability")
    evidence = [ev("pageText", str(item.get("text") or ""), str(item.get("locator") or "")) for item in items if str(item.get("text") or "").strip()]
    return [make_row(
        file_name, source_hash, f"physical:ordinal={ordinal};site={index}", ordinal,
        identity=code,
        rate_raw=rate,
        geography=location,
        description=description,
        dimensions=dimensions,
        latitude=latitude,
        longitude=longitude,
        availability=availability,
        evidence=evidence,
        extras={},
    ) for index, code in enumerate(codes, start=1)]


def adjacent_price_rows(file_name: str, source_hash: str, items: list[dict[str, Any]], ordinal: int) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    ordered = sorted(items, key=lambda item: as_int(item.get("number")) or 0)
    for index, item in enumerate(ordered):
        text = str(item.get("text") or "").strip()
        if not text or NOISE.search(text):
            continue
        for match in MONEY.finditer(text):
            prefix = text[:match.start()].strip(" :|-–—\n\t")
            identity = prefix.splitlines()[-1].strip() if prefix else ""
            if not useful_identity(identity):
                identity = previous_identity(ordered, index)
            if not useful_identity(identity):
                continue
            result.append(make_row(
                file_name, source_hash, f"{item.get('locator')};price={match.start() + 1}", ordinal,
                identity=identity,
                rate_raw=match.group(0),
                description=text,
                evidence=[ev("text", text, str(item.get("locator") or ""))],
                extras={},
            ))
    return result


def unpriced_location_rows(file_name: str, source_hash: str, items: list[dict[str, Any]], ordinal: int) -> list[dict[str, Any]]:
    if channel_for(file_name) not in {"OOH", "DOOH"}:
        return []
    result = []
    for item in items:
        text = "\n".join(part.strip() for part in str(item.get("text") or "").splitlines() if part.strip())
        if "\n" not in text or len(text) > 180 or NARRATIVE.search(text) or MONEY.search(text):
            continue
        parts = text.splitlines()
        if len(parts) > 3 or not useful_identity(text):
            continue
        first_letters = [char for char in parts[0] if char.isalpha()]
        if first_letters and sum(char.isupper() for char in first_letters) / len(first_letters) < 0.55:
            continue
        result.append(make_row(
            file_name, source_hash, str(item.get("locator") or ""), ordinal,
            identity=text,
            rate_raw="RATE_ON_REQUEST",
            geography=parts[0],
            evidence=[ev("location", text, str(item.get("locator") or ""))],
            extras={},
        ))
    return result


def make_row(
    file_name: str,
    source_hash: str,
    locator: str,
    ordinal: int | None,
    *,
    identity: str,
    rate_raw: str | None,
    geography: str | None = None,
    description: str | None = None,
    placement: str | None = None,
    format_raw: str | None = None,
    dimensions: str | None = None,
    daypart: str | None = None,
    latitude: str | None = None,
    longitude: str | None = None,
    availability: str | None = None,
    evidence: list[dict[str, Any]],
    extras: dict[str, Any],
) -> dict[str, Any]:
    supplier = supplier_for(file_name)
    channel = channel_for(file_name)
    row_id = hashlib.sha256(f"{source_hash}\n{locator}\n{identity}\n{rate_raw or ''}".encode()).hexdigest()
    amount = parse_amount_minor(rate_raw, implicit_zar=extras.get("implicitCurrency") == "ZAR")
    ambiguity = []
    if rate_raw and rate_raw != "RATE_ON_REQUEST" and amount is None and not is_unknown(rate_raw):
        ambiguity.append("AMBIGUOUS_RATE")
    if not rate_raw:
        rate_raw = "RATE_ON_REQUEST"
    return {
        "physicalRowId": row_id,
        "productCode": "ADV-" + row_id[:16].upper(),
        "sourceHash": source_hash,
        "sourceLocator": locator,
        "sourceOrdinal": ordinal,
        "supplierName": supplier,
        "channelHint": channel,
        "identityRaw": clean(identity),
        "geographyRaw": clean_optional(geography),
        "descriptionRaw": clean_optional(description),
        "placementRaw": clean_optional(placement),
        "formatRaw": clean_optional(format_raw),
        "dimensionsRaw": clean_optional(dimensions),
        "daypartRaw": clean_optional(daypart),
        "latitudeRaw": clean_optional(latitude),
        "longitudeRaw": clean_optional(longitude),
        "rateRaw": clean(rate_raw),
        "rateAmountMinor": amount,
        "currency": "ZAR" if amount is not None or (rate_raw and re.search(r"(?i)(?:ZAR|R)", rate_raw)) else None,
        "buyingBasisRaw": None,
        "availabilityRaw": clean_optional(availability),
        "ambiguityCodes": ambiguity,
        "evidence": evidence,
        "sourceExtras": extras,
    }


def merge_and_deduplicate(rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    seen: set[tuple[Any, ...]] = set()
    for row in rows:
        identity = normalize(str(row.get("identityRaw") or ""))
        if not identity:
            continue
        key = (
            row.get("sourceOrdinal"),
            identity,
            normalize(str(row.get("rateRaw") or "")),
            row.get("rateAmountMinor"),
        )
        if key in seen:
            continue
        seen.add(key)
        result.append(row)
    return sorted(result, key=lambda item: (item.get("sourceOrdinal") or 0, str(item.get("sourceLocator") or ""), str(item.get("identityRaw") or "")))


def supplier_for(file_name: str) -> str:
    mappings = [
        (r"\balgoa\s+fm\b", "Algoa FM"), (r"\barena[- ]|business day|daily dispatch|sowetan|sunday times|the herald", "Arena Holdings"),
        (r"blackspace", "BlackSpace"), (r"dstv|\bdms\b|digital rates & packages", "DStv Media Sales"), (r"digital screens concept", "Jit TV"),
        (r"eleven8", "Eleven8"), (r"emedia", "eMedia"), (r"ignition tv", "Ignition TV"), (r"insight outdoor", "Insight Outdoor ZA"),
        (r"\bjac\b|jacaranda", "Jacaranda FM"), (r"jcdecaux", "JCDecaux ZA"), (r"jit tv", "Jit TV"), (r"jozi fm", "Jozi FM"),
        (r"kena outdoor", "Kena Outdoor"), (r"mamg", "MAMG"), (r"media deck", "Volt Africa"), (r"primedia broadcasting", "Primedia Broadcasting"),
        (r"primedia outdoor", "Primedia Outdoor ZA"), (r"relativ media", "Relativ Media ZA"), (r"reveel", "Reveel"), (r"\brsd\b", "Roadside Digital"),
        (r"sabc", "SABC"), (r"sb outdoor", "SB Outdoor"), (r"smile\s*90", "Smile 90.4FM"), (r"summit ooh", "Summit OOH Media"),
        (r"home channel", "The Home Channel"), (r"virgin active", "Virgin Active"), (r"\by packages\b", "YFM"), (r"direct kaya|kaya packages", "Kaya 959"),
    ]
    return next((supplier for pattern, supplier in mappings if re.search(pattern, file_name, re.I)), "UNKNOWN_SUPPLIER")


def channel_for(file_name: str) -> str:
    checks = [
        (r"\bFM\b|radio|jac rate|kaya|y packages", "RADIO"),
        (r"\bTV\b|television|home channel|emedia|ignition", "TV"),
        (r"arena-|business day rate|daily dispatch|sowetan|sunday times|the herald", "PRINT"),
        (r"outdoor|\bOOH\b|billboard|roadside|site inventory|screens concept|jcdecaux|reveel|relativ|virgin active", "OOH"),
        (r"digital|media deck|dstv|\bdms\b|eleven8|mamg", "DIGITAL"),
    ]
    return next((channel for pattern, channel in checks if re.search(pattern, file_name, re.I)), "UNKNOWN_CHANNEL")


def normalized_rows(table: dict[str, Any]) -> list[list[str]]:
    result = []
    for row in table.get("rows") or []:
        values = []
        for item in row:
            if isinstance(item, dict):
                value = item.get("value") if item.get("value") is not None else item.get("cachedValue")
            else:
                value = item
            values.append(str(value or "").strip())
        if any(values):
            result.append(values)
    return result


def is_vertical(rows: list[list[str]]) -> bool:
    pairs = [row for row in rows if len(row) >= 2 and row[0].strip()]
    labels = {normalize(row[0]) for row in pairs}
    return len(pairs) >= 4 and len(labels & KV_LABELS) >= 4 and len(pairs) / len(rows) >= 0.6


def preferred_page_rate(items: list[dict[str, Any]]) -> str | None:
    labelled: list[tuple[int, str]] = []
    all_rates: list[str] = []
    for index, item in enumerate(items):
        text = str(item.get("text") or "")
        for match in MONEY.finditer(text):
            raw = match.group(0).strip()
            all_rates.append(raw)
            context = text[max(0, match.start() - 80):match.end() + 40]
            priority = 0 if re.search(r"(?i)discount", context) else 1 if re.search(r"(?i)rate\s*card|price", context) else 3
            labelled.append((priority, raw))
        if re.search(r"(?i)discounted\s+rate|rate\s+card", text) and index + 1 < len(items):
            next_text = str(items[index + 1].get("text") or "")
            match = MONEY.search(next_text)
            if match:
                labelled.append((0 if "discount" in text.lower() else 1, match.group(0).strip()))
    return min(labelled, default=(9, all_rates[0] if all_rates else None), key=lambda item: item[0])[1]


def first_page_value(items: list[dict[str, Any]], *labels: str) -> str | None:
    for index, item in enumerate(items):
        text = str(item.get("text") or "").strip()
        lowered = text.lower()
        for label in labels:
            if label in lowered:
                parts = [part.strip(" :") for part in text.splitlines() if part.strip(" :")]
                if len(parts) >= 2:
                    return parts[-1]
                if index + 1 < len(items):
                    return clean_optional(str(items[index + 1].get("text") or ""))
    return None


def previous_identity(items: list[dict[str, Any]], index: int) -> str:
    for previous in range(index - 1, max(-1, index - 4), -1):
        lines = split_lines(str(items[previous].get("text") or ""))
        if lines and useful_identity(lines[-1]):
            return lines[-1]
    return ""


def coordinate(values: dict[str, str], index: int) -> str | None:
    raw = first(values, "gps", "gpscoordinate", "coordinates")
    if not raw:
        return None
    pair = decimal_coordinates(raw)
    return pair[index]


def decimal_coordinates(raw: str) -> tuple[str | None, str | None]:
    match = DECIMAL_GPS.search(raw)
    return (match.group(1), match.group(2)) if match else (None, None)


def parse_amount_minor(raw: str | None, *, implicit_zar: bool) -> int | None:
    if not raw or is_unknown(raw):
        return None
    text = raw.upper().replace("ZAR", "").strip()
    explicit = text.startswith("R")
    if explicit:
        text = text[1:].strip()
    if not explicit and not implicit_zar:
        return None
    text = text.replace("\u00a0", "").replace(" ", "")
    if re.fullmatch(r"\d+,\d{1,2}", text):
        return None
    if "," in text and "." not in text:
        tail = text.rsplit(",", 1)[1]
        text = text.replace(",", "." if len(tail) in {1, 2} else "")
    elif "," in text and "." in text:
        text = text.replace(",", "")
    try:
        return int((Decimal(text) * 100).quantize(Decimal("1")))
    except (InvalidOperation, ValueError):
        return None


def is_unknown(value: str) -> bool:
    return value.strip().upper() in {"TBC", "POA", "RATE_ON_REQUEST"} or bool(UNKNOWN_RATE.search(value))


def numeric_rate(value: str) -> bool:
    return bool(re.fullmatch(r"\d[\d\s.,]*", value.strip()))


def useful_identity(value: str) -> bool:
    return bool(value and value.strip() and any(ch.isalpha() for ch in value) and not NOISE.search(value) and normalize(value) not in RATE_HEADERS)


def split_rate_values(value: str) -> list[str]:
    lines = split_lines(value)
    return lines if len(lines) > 1 else [value.strip()]


def split_lines(value: str) -> list[str]:
    return [part.strip() for part in re.split(r"[\r\n]+", value) if part.strip()]


def align(names: list[str], rates: list[str]) -> list[tuple[str, str]]:
    if len(names) == len(rates):
        return list(zip(names, rates, strict=True))
    return [(" | ".join(names), rate) for rate in rates]


def join_values(values: dict[str, str], *keys: str) -> str | None:
    selected = unique([values[key] for key in keys if values.get(key)])
    return " | ".join(selected) if selected else None


def first(values: dict[str, str], *keys: str) -> str | None:
    return next((values[key] for key in keys if values.get(key)), None)


def ev(field: str, raw: str, locator: str) -> dict[str, Any]:
    return {"field": field, "raw": raw, "sourceLocator": locator}


def source_ordinal(locator: str, table: dict[str, Any]) -> int | None:
    return ordinal_from_locator(locator) or as_int(table.get("page")) or as_int(table.get("slide"))


def ordinal_from_locator(locator: str) -> int | None:
    match = re.search(r"(?:page|slide)=(\d+)", locator, re.I)
    return int(match.group(1)) if match else None


def index_of(headers: list[str], options: set[str]) -> int | None:
    return next((index for index, value in enumerate(headers) if value in options), None)


def cell(row: list[str], index: int | None) -> str:
    return row[index].strip() if index is not None and index < len(row) else ""


def normalize(value: str) -> str:
    return "".join(ch.lower() for ch in value if ch.isalnum())


def clean(value: str) -> str:
    return " ".join(value.strip(" :#-\t").split())


def clean_optional(value: str | None) -> str | None:
    cleaned = clean(value) if value else ""
    return cleaned or None


def unique(values: Iterable[str]) -> list[str]:
    result, seen = [], set()
    for value in values:
        key = normalize(value)
        if key and key not in seen:
            seen.add(key)
            result.append(value)
    return result


def valid_code(value: str) -> bool:
    compact = normalize(value)
    return 4 <= len(compact) <= 18 and any(ch.isalpha() for ch in compact) and any(ch.isdigit() for ch in compact)


def as_int(value: Any) -> int | None:
    try:
        return int(value) if value is not None else None
    except (TypeError, ValueError):
        return None
