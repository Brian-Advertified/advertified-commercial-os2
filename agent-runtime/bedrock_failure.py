"""Safe, structured evidence for a rejected Bedrock result."""

from __future__ import annotations

import logging

from contracts import ProviderUsage

logger = logging.getLogger(__name__)


class BedrockProviderError(RuntimeError):
    """Raised when Bedrock cannot return validated proposal data."""

    def __init__(
        self,
        message: str,
        *,
        stage: str = "PRE_INFERENCE",
        acceptance: str = "NOT_ACCEPTED",
        usage: ProviderUsage | None = None,
        rejected_output: object | None = None,
    ) -> None:
        super().__init__(message)
        self.stage = stage
        self.acceptance = acceptance
        self.usage = usage
        self.rejected_output = rejected_output

    def detail(self) -> dict[str, object]:
        return {
            "code": "BEDROCK_RESULT_REJECTED",
            "message": str(self),
            "stage": self.stage,
            "provider_acceptance": self.acceptance,
            "usage": (self.usage.model_dump(mode="json") if self.usage else None),
            "rejected_output": self.rejected_output,
        }


def safe_client_error(response: dict[str, object]) -> BedrockProviderError:
    provider_error = response.get("Error", {})
    code = str(provider_error.get("Code", "ClientError"))
    safe_code = code if code.replace("_", "").isalnum() else "ClientError"
    detail = str(provider_error.get("Message", ""))
    safe_detail = "".join(
        value for value in detail if value.isalnum() or value in " .,_:-()/"
    )[:240]
    logger.warning(
        "Bedrock client rejection: code=%s detail=%s",
        safe_code[:80],
        safe_detail or "unavailable",
    )
    return BedrockProviderError(
        f"Bedrock request failed safely ({safe_code[:80]})."
    )


def safe_boto_error(name: str) -> BedrockProviderError:
    safe_name = name if name.replace("_", "").isalnum() else "BotoCoreError"
    return BedrockProviderError(
        f"Bedrock request failed safely ({safe_name[:80]})."
    )
