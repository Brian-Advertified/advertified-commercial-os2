import asyncio

import anyio
import pytest
from fastapi import HTTPException

import runtime_admission as admission


class Body:
    def __init__(self, chunks):
        self.chunks = chunks
        self.read = False

    async def stream(self):
        self.read = True
        for chunk in self.chunks:
            yield chunk


def test_capacity_rejects_before_reading_and_stream_limit_needs_no_length(monkeypatch):
    async def scenario():
        monkeypatch.setattr(admission, "execution_slots", anyio.CapacityLimiter(1))
        monkeypatch.setattr(admission, "MAX_REQUEST_BYTES", 4)
        async with admission.admitted_request(Body([b"12"])) as body:
            assert body == b"12"
            denied = Body([b"large"])
            async def reject_another_request():
                with pytest.raises(HTTPException) as error:
                    async with admission.admitted_request(denied):
                        pytest.fail("Busy runtime admitted a body")
                assert error.value.status_code == 429
            await asyncio.create_task(reject_another_request())
            assert not denied.read
        with pytest.raises(HTTPException) as error:
            async with admission.admitted_request(Body([b"123", b"45"])):
                pytest.fail("Oversized streamed body admitted")
        assert error.value.status_code == 413
        async with admission.admitted_request(Body([b"1234"])) as body:
            assert body == b"1234"
    asyncio.run(scenario())
