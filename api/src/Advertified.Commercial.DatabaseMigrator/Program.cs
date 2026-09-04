using Advertified.Commercial.DatabaseMigrator;
using Npgsql;

const string ApplyArgument = "--apply";
const string ConnectionVariable = "ADVERTIFIED_MIGRATION_CONNECTION_STRING";

if (args.Length != 1 || !string.Equals(args[0], ApplyArgument, StringComparison.Ordinal))
{
    Console.Error.WriteLine(
        $"No database change was made. Pass {ApplyArgument} after explicit operator approval.");
    return 2;
}

var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        $"No database change was made. Set {ConnectionVariable} to a migration-only connection.");
    return 2;
}

try
{
    var result = await new DatabaseMigrationOperation(TimeProvider.System)
        .ApplyAsync(connectionString);
    Console.WriteLine(
        $"Applied {result.AppliedMigrations.Count} migration(s); " +
        $"synchronised {result.MasterData.CollectionCount} master-data collections.");
    return 0;
}
catch (PostgresException exception)
{
    Console.Error.WriteLine(
        $"Migration failed safely (PostgreSQL SQLSTATE {exception.SqlState}; " +
        $"schema {exception.SchemaName ?? "none"}; table {exception.TableName ?? "none"}; " +
        $"constraint {exception.ConstraintName ?? "none"}). No success was recorded.");
    return 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine(
        $"Migration failed safely ({exception.GetType().Name}: " +
        $"{exception.Message}). No success was recorded.");
    return 1;
}
