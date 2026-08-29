"""Provider interface and zero-cost deterministic implementation."""

from __future__ import annotations

from collections.abc import Sequence
from typing import Generic, Protocol, TypeVar

from contracts import AgentInvocationEnvelope, AgentOutputEnvelope, EvaluationFixture

ArtifactT = TypeVar("ArtifactT")


class GenerationProvider(Protocol, Generic[ArtifactT]):
    async def invoke(
        self,
        invocation: AgentInvocationEnvelope,
    ) -> AgentOutputEnvelope[ArtifactT]:
        """Return one schema-valid proposal output."""


class DeterministicFixtureNotFoundError(LookupError):
    """Raised when a deterministic invocation has no exact fixture."""


class DeterministicProvider(Generic[ArtifactT]):
    def __init__(self, fixtures: Sequence[EvaluationFixture[ArtifactT]]) -> None:
        keyed_fixtures = {
            self._key(fixture.invocation): fixture
            for fixture in fixtures
        }
        if len(keyed_fixtures) != len(fixtures):
            raise ValueError("Each deterministic invocation must have exactly one fixture.")
        self._fixtures = keyed_fixtures

    async def invoke(
        self,
        invocation: AgentInvocationEnvelope,
    ) -> AgentOutputEnvelope[ArtifactT]:
        fixture = self._fixtures.get(self._key(invocation))
        if fixture is None:
            raise DeterministicFixtureNotFoundError(
                "No exact deterministic fixture exists for this invocation."
            )

        if fixture.output.usage.tool_calls > invocation.tool_policy.max_tool_calls:
            raise ValueError("Deterministic output exceeds the invocation tool budget.")

        return fixture.output.model_copy(deep=True)

    @staticmethod
    def _key(invocation: AgentInvocationEnvelope) -> str:
        return invocation.model_dump_json()
