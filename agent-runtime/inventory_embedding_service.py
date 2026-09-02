"""Bounded, normalized inventory embeddings for semantic recall only."""

from __future__ import annotations

import hashlib
import json
import math
import os
import re
from decimal import ROUND_CEILING, Decimal
from typing import Annotated

from botocore.config import Config
from botocore.exceptions import BotoCoreError, ClientError
from botocore.session import get_session
from pydantic import BaseModel, Field, model_validator

TITAN_MODEL = "amazon.titan-embed-text-v2:0"
BEDROCK_REGION = "eu-west-1"
DIMENSIONS = 1024
PRICE_KEY = "ADVERTIFIED_BEDROCK_EMBEDDING_INPUT_PER_MILLION_USD"


class InventoryEmbeddingRequest(BaseModel):
    canonical_text: Annotated[str, Field(min_length=1, max_length=8_000)]
    model: str
    dimensions: int
    normalize: bool

    @model_validator(mode="after")
    def validate_policy(self) -> InventoryEmbeddingRequest:
        if (
            self.model != TITAN_MODEL
            or self.dimensions != DIMENSIONS
            or not self.normalize
        ):
            raise ValueError("The governed inventory embedding policy is required.")
        return self


class InventoryEmbeddingResponse(BaseModel):
    model: str
    region: str
    embedding: list[float]
    provider_request_id: str
    input_tokens: int
    incremental_cost_usd_micros: int


class InventoryEmbeddingProviderError(RuntimeError):
    """Raised when an embedding cannot be produced within the governed contract."""


def deterministic_embedding(
    request: InventoryEmbeddingRequest,
) -> InventoryEmbeddingResponse:
    vector = [0.0] * DIMENSIONS
    tokens = re.findall(r"[\w]+", request.canonical_text.lower())
    for token in tokens:
        digest = hashlib.sha256(token.encode("utf-8")).digest()
        index = int.from_bytes(digest[:4], "little") % DIMENSIONS
        vector[index] += 1.0 if digest[4] & 1 == 0 else -1.0
    _normalize(vector)
    return InventoryEmbeddingResponse(
        model="fixture-inventory-embedding-v1",
        region="local",
        embedding=vector,
        provider_request_id=f"fixture-{hashlib.sha256(request.canonical_text.encode()).hexdigest()}",
        input_tokens=len(tokens),
        incremental_cost_usd_micros=0,
    )


def bedrock_embedding(
    request: InventoryEmbeddingRequest,
) -> InventoryEmbeddingResponse:
    price = _configured_price()
    client = get_session().create_client(
        "bedrock-runtime",
        region_name=BEDROCK_REGION,
        config=Config(connect_timeout=10, read_timeout=30, retries={"max_attempts": 1}),
    )
    try:
        response = client.invoke_model(
            modelId=TITAN_MODEL,
            contentType="application/json",
            accept="application/json",
            body=json.dumps(
                {
                    "inputText": request.canonical_text,
                    "dimensions": DIMENSIONS,
                    "normalize": True,
                }
            ),
        )
        payload = json.loads(response["body"].read())
        vector = [float(value) for value in payload["embedding"]]
        tokens = int(payload["inputTextTokenCount"])
        request_id = str(response["ResponseMetadata"]["RequestId"])
    except (BotoCoreError, ClientError, KeyError, TypeError, ValueError) as error:
        raise InventoryEmbeddingProviderError(
            "Bedrock inventory embedding generation failed safely."
        ) from error
    if len(vector) != DIMENSIONS or not all(math.isfinite(value) for value in vector):
        raise InventoryEmbeddingProviderError("Bedrock returned an invalid embedding.")
    magnitude = math.sqrt(sum(value * value for value in vector))
    if magnitude < 0.999 or magnitude > 1.001:
        raise InventoryEmbeddingProviderError("Bedrock returned an unnormalized embedding.")
    cost = int((Decimal(tokens) * price).to_integral_value(rounding=ROUND_CEILING))
    return InventoryEmbeddingResponse(
        model=TITAN_MODEL,
        region=BEDROCK_REGION,
        embedding=vector,
        provider_request_id=request_id,
        input_tokens=tokens,
        incremental_cost_usd_micros=cost,
    )


def _configured_price() -> Decimal:
    try:
        price = Decimal(os.environ.get(PRICE_KEY, ""))
    except Exception as error:
        raise InventoryEmbeddingProviderError(
            "Inventory embedding pricing is not configured."
        ) from error
    if price <= 0:
        raise InventoryEmbeddingProviderError(
            "Inventory embedding pricing must be positive."
        )
    return price


def _normalize(vector: list[float]) -> None:
    magnitude = math.sqrt(sum(value * value for value in vector))
    if magnitude == 0:
        vector[0] = 1.0
        return
    for index, value in enumerate(vector):
        vector[index] = value / magnitude
