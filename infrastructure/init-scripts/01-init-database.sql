-- Advertified Unified - Database Initialization Script
-- This script sets up the PostgreSQL database with required extensions

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "postgis";
CREATE EXTENSION IF NOT EXISTS "vector";

-- Create the vector extension for pgvector if not available
-- Note: pgvector is included in the Docker image we're using

-- Create default schema if it doesn't exist
CREATE SCHEMA IF NOT EXISTS public;

-- Set default privileges for future objects
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO advertified;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO advertified;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT EXECUTE ON FUNCTIONS TO advertified;

-- Create a function to update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Create initial tenant for development
INSERT INTO tenants (id, type, legal_name, trading_name, slug, status, timezone, currency, vat_status, created_at, updated_at)
VALUES (
    '00000000-0000-0000-0000-000000000001',
    'agency',
    'Advertified Development',
    'Advertified Dev',
    'advertified-dev',
    'active',
    'Africa/Johannesburg',
    'ZAR',
    'registered',
    NOW(),
    NOW()
) ON CONFLICT (slug) DO NOTHING;

-- Create initial admin user for development
INSERT INTO users (id, email, display_name, status, last_login_at, created_at)
VALUES (
    '00000000-0000-0000-0000-000000000001',
    'admin@advertified.com',
    'System Administrator',
    'active',
    NOW(),
    NOW()
) ON CONFLICT (email) DO NOTHING;

-- Create admin membership for development tenant
INSERT INTO memberships (id, tenant_id, user_id, role, status, invited_by, invited_at, accepted_at)
VALUES (
    '00000000-0000-0000-0000-000000000001',
    '00000000-0000-0000-0000-000000000001',
    '00000000-0000-0000-0000-000000000001',
    'platform_admin',
    'active',
    '00000000-0000-0000-0000-000000000001',
    NOW(),
    NOW()
) ON CONFLICT (tenant_id, user_id) DO NOTHING;

-- Grant necessary permissions
GRANT ALL PRIVILEGES ON DATABASE advertified TO advertified;
GRANT ALL PRIVILEGES ON SCHEMA public TO advertified;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO advertified;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO advertified;