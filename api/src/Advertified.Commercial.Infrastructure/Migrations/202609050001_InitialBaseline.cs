using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Advertified.Commercial.Infrastructure.Migrations;

[DbContext(typeof(GovernanceDbContext))]
[Migration("202609050001_InitialBaseline")]
public sealed class InitialBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Extension provisioning remains a database-host prerequisite for PostGIS,
        // pgcrypto and vector. pg_trgm is installable by the migration role.
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
        using var stream = typeof(InitialBaseline).Assembly.GetManifestResourceStream(
            "Advertified.Database.InitialBaseline.sql") ??
            throw new InvalidOperationException("The initial database schema resource is missing.");
        using var reader = new StreamReader(stream);
        migrationBuilder.Sql(reader.ReadToEnd());
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP SCHEMA commercial CASCADE; DROP SCHEMA governance CASCADE;");
    }
}
