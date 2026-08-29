# ADR-0009: .NET 10 and C# 14 Commercial API baseline

## Status

Accepted for the local redevelopment baseline — Brian Rabuthu, 2026-08-29.

## Context

The repository originally targeted .NET 8. Brian Rabuthu subsequently directed that the
clean redevelopment use .NET 10 and C# 14. This owner direction supersedes the earlier
runtime version while preserving the Commercial API ownership and technology boundaries.

## Decision owner and reviewers

| Responsibility | Actual name | Decision/date |
|---|---|---|
| Accountable owner | Brian Rabuthu | Accepted, 2026-08-29 |
| Engineering reviewer | Not required for local-only retarget | Independent review before publication |
| Operations reviewer | Not required for local-only retarget | Independent review before deployment |

## Decision

- All authored C# projects target `net10.0` and explicitly use C# language version 14.0.
- The repository SDK baseline is the .NET 10.0.1xx feature band with patch roll-forward.
- Framework-coupled ASP.NET Core, EF Core, Npgsql and OpenAPI packages use stable
  .NET 10-compatible versions.
- CI installs the .NET 10 SDK and builds/tests the same target used locally.
- Earlier .NET 8 evidence remains historical; it does not verify the retargeted tree.

## Non-decisions

This does not change PostgreSQL 16, React 19.2, Python 3.12 compatibility, authentication,
tenant isolation, migration approval, production topology, or any product gate.

## Verification

- `dotnet --info` resolves the approved .NET 10 SDK;
- every C# project reports `net10.0` and C# 14;
- Release API and migration-runner builds pass with warnings treated as errors;
- the complete C# test suite and architecture suite pass;
- generated OpenAPI matches the retained v1 contract.

## Decision record

- Directed by: Brian Rabuthu
- Decision date: 2026-08-29
- Supersedes: the .NET 8 runtime outcome recorded in ADR-0004
- Superseded by: none
