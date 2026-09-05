"""Bound admitted bodies and SDK work together; never queue buffered requests."""

from contextlib import asynccontextmanager

import anyio
from fastapi import HTTPException, Request

# Technical transport limits, not commercial budgets. Each process admits at
# most four bodies/workers. Provider-side budgets remain independently enforced.
MAX_REQUEST_BYTES = 8 * 1024 * 1024
BODY_TIMEOUT_SECONDS = 30
execution_slots = anyio.CapacityLimiter(4)


@asynccontextmanager
async def admitted_request(request: Request):
    try:
        execution_slots.acquire_nowait()
    except anyio.WouldBlock as error:
        raise HTTPException(429, "Agent execution capacity is busy.",
                            headers={"Retry-After": "1"}) from error
    try:
        body = bytearray()
        try:
            with anyio.fail_after(BODY_TIMEOUT_SECONDS):
                async for chunk in request.stream():
                    if len(chunk) > MAX_REQUEST_BYTES - len(body):
                        raise HTTPException(413, "Agent request is too large.")
                    body.extend(chunk)
        except TimeoutError as error:
            raise HTTPException(408, "Agent request body timed out.") from error
        # Cancellation of a waiter is not cancellation of an accepted remote
        # call. Hold the admission slot until the bounded SDK call really ends.
        with anyio.CancelScope(shield=True):
            yield bytes(body)
    finally:
        execution_slots.release()
