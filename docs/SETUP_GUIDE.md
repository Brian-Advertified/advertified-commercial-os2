# Advertified Unified - Setup Guide

## Prerequisites

Ensure you have the following installed:

- **Node.js** 20+ (Check with `node --version`)
- **.NET SDK** 8.0+ (Check with `dotnet --version`)
- **Python** 3.10+ (Check with `python --version`)
- **Docker** and **Docker Compose** (Check with `docker --version` and `docker-compose --version`)
- **Git** (Check with `git --version`)

## Initial Setup

### 1. Clone the Repository

```bash
git clone <repository-url>
cd advertified-commercial-os2
```

### 2. Environment Configuration

Copy the example environment file and configure it:

```bash
cp infrastructure/env.example .env
```

Edit the `.env` file with your local development settings. At minimum, update:
- Database passwords
- Storage credentials
- JWT secrets
- API keys (when ready)

### 3. Start Infrastructure Services

Start the Docker Compose services:

```bash
cd infrastructure
docker-compose up -d
```

This will start:
- PostgreSQL with PostGIS and pgvector
- MinIO (S3-compatible storage)
- Redis (caching and queues)
- Mailhog (email testing)

Verify services are running:

```bash
docker-compose ps
```

### 4. Install Dependencies

#### Web Application (React)

```bash
cd web
npm install
cd ..
```

#### Commercial API (.NET)

```bash
cd api
dotnet restore
cd ..
```

#### Agent Runtime (Python)

```bash
cd agent-runtime
pip install -r requirements.txt
cd ..
```

### 5. Database Initialization

The database will be automatically initialized when PostgreSQL starts using the init scripts in `infrastructure/init-scripts/`. This includes:
- Creating required extensions (uuid-ossp, postgis, vector)
- Setting up base schemas
- Creating development tenant and admin user

To verify the database is ready:

```bash
docker-compose exec postgres psql -U advertified -d advertified -c "\dt"
```

### 6. Start Development Services

#### Terminal 1: Web Application

```bash
cd web
npm run dev
```

The web application will be available at `http://localhost:5173`

#### Terminal 2: Commercial API

```bash
cd api
dotnet run
```

The API will be available at `http://localhost:5000`

#### Terminal 3: Agent Runtime

```bash
cd agent-runtime
python main.py
```

The agent runtime will be available at `http://localhost:8000`

### 7. Verify Setup

Test each service:

```bash
# Test web application
curl http://localhost:5173

# Test API health
curl http://localhost:5000/health

# Test agent runtime health
curl http://localhost:8000/health

# Test database connection
docker-compose exec postgres pg_isready -U advertified
```

## Development Workflow

### Running Tests

```bash
# Web application tests
cd web
npm test

# API tests
cd api
dotnet test

# Agent runtime tests
cd agent-runtime
pytest
```

### Database Migrations

```bash
cd api
dotnet ef migrations add MigrationName
dotnet ef database update
```

### Code Quality

```bash
# Web application linting
cd web
npm run lint

# API code analysis
cd api
dotnet format

# Python linting
cd agent-runtime
black .
flake8 .
```

## Troubleshooting

### Docker Issues

If Docker services fail to start:

```bash
# Check logs
docker-compose logs

# Restart services
docker-compose restart

# Rebuild containers
docker-compose up -d --build
```

### Port Conflicts

If ports are already in use, modify the port mappings in:
- `infrastructure/docker-compose.yml` (infrastructure services)
- `.env` file (application services)
- `web/vite.config.ts` (frontend dev server)

### Database Connection Issues

Ensure PostgreSQL is running and accessible:

```bash
docker-compose exec postgres psql -U advertified -d advertified
```

Check connection string in `.env` file matches Docker configuration.

### Python Dependencies

If you encounter Python dependency issues:

```bash
cd agent-runtime
pip install --upgrade pip
pip install -r requirements.txt --force-reinstall
```

## Next Steps

After setup is complete:

1. Review the [Capability Ledger](../docs/CAPABILITY_LEDGER.md) to track implementation progress
2. Start with [Gate 1: Architecture Guardrails](../docs/IMPLEMENTATION_PLAN.md)
3. Follow the implementation gates in order as specified in the build specification

## Additional Resources

- [Build Specification](../docs/ADVERTIFIED_UNIFIED_STRATEGY.md)
- [Architecture Decision Records](../docs/adr/)
- [API Documentation](http://localhost:5000/docs) (when API is running)
- [Agent Runtime Documentation](http://localhost:8000/docs) (when runtime is running)