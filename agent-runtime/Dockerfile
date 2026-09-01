FROM python:3.12.14-slim-trixie@sha256:e5c9fa26ffb76e11e0f054f30dc2523a2f9693f0c36c0cf1e39b27e152d899fc

ENV PIP_DISABLE_PIP_VERSION_CHECK=1 \
    PIP_NO_CACHE_DIR=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

COPY agent-runtime/requirements.lock ./requirements.lock
RUN python -m pip install --require-hashes --no-cache-dir -r requirements.lock \
    && python -m pip check \
    && groupadd --gid 10001 advertified \
    && useradd --uid 10001 --gid advertified --no-create-home --shell /usr/sbin/nologin advertified \
    && dpkg --purge --force-remove-essential gzip

COPY agent-runtime/*.py ./

USER 10001:10001

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD ["python", "-c", "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8080/health/live', timeout=2).read()"]

CMD ["python", "-m", "uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8080"]
