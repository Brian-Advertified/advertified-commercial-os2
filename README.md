# Advertified Unified

Production build specification for Advertified Unified - a marketing intelligence and campaign management platform using Amazon Bedrock AgentCore.

## Architecture Overview

This system implements a comprehensive marketing platform with the following components:

- **Web Application**: React 19.2.0/TypeScript/Vite - Authenticated user interface
- **Commercial API**: C#/.NET ASP.NET Core - Canonical business state and operations
- **Agent Runtime**: Python/FastAPI with Amazon Bedrock AgentCore - AI agent orchestration
- **Workers**: C# and Python workers for background processing
- **Database**: PostgreSQL/PostGIS/pgvector - Commercial data and geographic queries
- **Storage**: S3-compatible storage - Files, assets, and documents

## Getting Started

### Prerequisites

- Node.js 20+ (for web application)
- .NET 8.0 SDK (for Commercial API)
- Python 3.10+ (for agent runtime)
- Docker and Docker Compose (for local development)
- AWS CLI configured with appropriate credentials
- PostgreSQL 15+ with PostGIS and pgvector extensions

### Local Development Setup

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd advertified-commercial-os2
   ```

2. **Install dependencies**
   ```bash
   # Web application
   cd web
   npm install
   cd ..

   # Commercial API
   cd api
   dotnet restore
   cd ..

   # Agent runtime
   cd agent-runtime
   pip install -r requirements.txt
   cd ..
   ```

3. **Start local development environment**
   ```bash
   docker-compose up -d
   ```

4. **Run database migrations**
   ```bash
   cd api
   dotnet ef database update
   cd ..
   ```

5. **Start services**
   ```bash
   # Terminal 1: Web application
   cd web
   npm run dev

   # Terminal 2: Commercial API
   cd api
   dotnet run

   # Terminal 3: Agent runtime
   cd agent-runtime
   uvicorn main:app --reload
   ```

## Project Structure

```
advertified-commercial-os2/
├── web/                  # React frontend application
├── api/                   # C#/.NET Commercial API
├── agent-runtime/         # Python/FastAPI AgentCore runtime
├── workers/               # Background workers (C# and Python)
├── shared/                # Shared contracts and schemas
├── infrastructure/        # Docker, deployment, and ops configs
├── tests/                 # Test suites
├── docs/                  # Documentation
└── README.md
```

## Development Guidelines

### Key Principles

1. **SOLID Principles**: Single responsibility, open/closed, Liskov substitution, interface segregation, dependency inversion
2. **Separation of Concerns**: Clear boundaries between web, API, agents, and workers
3. **No Dead Code**: Remove unused code immediately
4. **Test Coverage**: Add test cases for each function
5. **Loose Coupling**: Modules should be independent and interchangeable

### File Size Limits

- Maximum 400 lines per source file (enforced by CI)
- Prefer functions under 40 lines (60-line hard limit)
- Cyclomatic complexity target <= 10 per function

### Technology Constraints

- **Web**: React 19.2.0, TypeScript, Vite (no Tailwind without ADR)
- **API**: C#/.NET ASP.NET Core (no Python business API)
- **Runtime**: Python/FastAPI with AgentCore (no separate A2A containers)
- **Database**: PostgreSQL/PostGIS/pgvector (no SQLite)
- **AI**: AWS Bedrock only (no Explee or unapproved providers)

## Implementation Gates

The system is built through 13 ordered gates:

1. **Gate 0**: Repository baseline
2. **Gate 1**: Architecture guardrails
3. **Gate 2**: Canonical foundation
4. **Gate 3**: Authenticated shell
5. **Gate 4**: Evidence and opportunity
6. **Gate 5**: Canonical Brief
7. **Gate 6**: Inventory truth
8. **Gate 7**: Planning
9. **Gate 8**: Proposal
10. **Gate 9**: Rapid OOH
11. **Gate 10**: Supplier marketplace
12. **Gate 11**: Campaign delivery
13. **Gate 12**: Hardening
14. **Gate 13**: Production launch

## AI Agents

The system uses 11 specialized agents orchestrated through Amazon Bedrock AgentCore:

1. **Opportunity Intelligence**: What credible advertising opportunity exists?
2. **Business Interpretation**: What does this business sell, to whom, and in what buying context?
3. **Strategy**: What growth and communications strategy follows from the evidence?
4. **Brief Drafting**: How does approved evidence become a complete campaign brief?
5. **Audience**: Which audiences are plausible and why?
6. **Inventory Intelligence**: Which verified products are eligible and valuable?
7. **Media Planning**: How should channels, budget and flighting work together?
8. **Critic & Readiness**: What is weak, unsupported, contradictory or unsafe?
9. **Proposal Narrative**: How should the approved plan be explained to the client?
10. **Creative**: What concepts and adaptations could support the approved plan?
11. **Measurement**: What changed and what should be learned?

## Documentation

- Build specification: See `docs/ADVERTIFIED_UNIFIED_STRATEGY.md`
- API documentation: Auto-generated OpenAPI at `/api/v1/docs`
- Architecture decisions: `docs/adr/`
- Implementation status: `docs/CAPABILITY_LEDGER.md`

## License

Proprietary - All rights reserved