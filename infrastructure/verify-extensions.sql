DO $verification$
DECLARE
    missing_extensions text;
BEGIN
    SELECT string_agg(required.name, ', ' ORDER BY required.name)
    INTO missing_extensions
    FROM (VALUES ('pgcrypto'), ('postgis'), ('vector')) AS required(name)
    LEFT JOIN pg_extension installed ON installed.extname = required.name
    WHERE installed.extname IS NULL;

    IF missing_extensions IS NOT NULL THEN
        RAISE EXCEPTION 'Missing required extensions: %', missing_extensions;
    END IF;
END
$verification$;

SELECT extname, extversion
FROM pg_extension
WHERE extname IN ('pgcrypto', 'postgis', 'vector')
ORDER BY extname;
