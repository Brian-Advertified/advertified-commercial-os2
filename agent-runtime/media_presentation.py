"""Client-facing channel wording; canonical media classifications remain unchanged."""

from master_data_codes import Channels


CHANNEL_LABELS = {
    Channels.OOH: "Outdoor advertising",
    Channels.DOOH: "Digital screens",
}


def channel_labels(channels: tuple[str, ...]) -> str:
    return ", ".join(CHANNEL_LABELS.get(channel, channel) for channel in channels)


CLIENT_WORDING_INSTRUCTION = (
    "Use client-facing channel wording: "
    + "; ".join(f"{code.value} means {label}" for code, label in CHANNEL_LABELS.items())
    + ". Preserve original quotations and source evidence."
)
