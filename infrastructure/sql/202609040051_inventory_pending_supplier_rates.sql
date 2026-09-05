-- Retired non-EF repair, never an applied-migration record.
-- Pricing-policy changes require an explicit reviewed EF migration and owner authority.
DO $retired$
BEGIN
    RAISE EXCEPTION 'Retired schema repair: no schema changes were made';
END
$retired$;
