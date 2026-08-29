# Local setup guide

The canonical setup and verification commands are maintained in the repository `README.md`. Use that file so commands do not drift.

## Important local defaults

| Service | Loopback port |
|---|---:|
| PostgreSQL | 55432 |
| Redis | 56379 |
| MinIO API | 59000 |
| MinIO console | 59001 |
| MailHog SMTP | 51025 |
| MailHog UI | 58025 |

Copy `infrastructure/env.example` to `infrastructure/.env` and change its local-only passwords. Never commit `.env`.

## Healthy infrastructure

From the repository root:

```powershell
docker compose -f infrastructure/docker-compose.yml up -d --build --wait
docker compose -f infrastructure/docker-compose.yml ps
```

All four services must report healthy. PostgreSQL health asserts major version 16 and the presence of `pgcrypto`, `postgis`, and `vector`.

## Stop without deleting data

```powershell
docker compose -f infrastructure/docker-compose.yml down
```

The supported workflow does not delete volumes.

## Troubleshooting

- Port conflict: keep the os2 defaults or set unused loopback ports in `infrastructure/.env`.
- Web dependency drift: delete no source files; run `npm ci` from `web`.
- API build ambiguity: target the explicit project path shown in `README.md`.
- Python import issue: activate the repository `.venv` and install `agent-runtime/requirements-dev.txt`.
- Provider request: stop. Live provider use is outside the current gate.
