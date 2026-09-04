"""Safe, structured evidence for a rejected Bedrock result."""

from __future__ import annotations

from contracts import ProviderUsage


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
