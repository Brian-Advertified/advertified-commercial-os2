FROM pgvector/pgvector:0.8.6-pg16-bookworm

RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        postgresql-16-postgis-3 \
        postgresql-16-postgis-3-scripts \
    && rm -rf /var/lib/apt/lists/*
