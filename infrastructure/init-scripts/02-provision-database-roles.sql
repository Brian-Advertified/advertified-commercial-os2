-- Pre-migration, non-login group roles required by the governed database boundary.
-- Login credentials remain an environment-specific provisioning responsibility.

DO $roles$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_roles WHERE rolname = 'advertified_migrator'
    ) THEN
        CREATE ROLE advertified_migrator
            NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_roles WHERE rolname = 'advertified_app'
    ) THEN
        CREATE ROLE advertified_app
            NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_roles WHERE rolname = 'advertified_worker'
    ) THEN
        CREATE ROLE advertified_worker
            NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_roles
        WHERE rolname IN ('advertified_migrator', 'advertified_app', 'advertified_worker')
          AND (
              rolcanlogin
              OR rolsuper
              OR rolcreatedb
              OR rolcreaterole
              OR rolinherit
              OR rolbypassrls
          )
    ) THEN
        RAISE EXCEPTION 'Advertified group roles are not least privilege';
    END IF;

    EXECUTE format(
        'GRANT CREATE ON DATABASE %I TO advertified_migrator',
        current_database()
    );
END
$roles$;

GRANT USAGE, CREATE ON SCHEMA public TO advertified_migrator;
