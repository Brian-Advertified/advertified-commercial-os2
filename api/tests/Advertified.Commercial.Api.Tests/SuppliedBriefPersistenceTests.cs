using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Brief;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class SuppliedBriefPersistenceTests
{
    [Fact]
    public async Task RetainedInterpretationReplaysWithOneUsageEntryAndRejectsChangedInput()
    {
        var database = Environment.GetEnvironmentVariable("PGDATABASE") ?? "";
        Assert.StartsWith("advertified_brief_test_", database);
        var connection = new NpgsqlConnectionStringBuilder {
            Host = Environment.GetEnvironmentVariable("PGHOST"), Database = database,
            Username = Environment.GetEnvironmentVariable("PGUSER"),
            Password = Environment.GetEnvironmentVariable("PGPASSWORD") }.ConnectionString;
        await using var db = new GovernanceDbContext(new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connection).Options);
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        await SeedAsync(db, tenant, actor);
        var store = new SuppliedBriefInterpretationStore(new BriefRecordStore(db), TimeProvider.System);
        var input = new SuppliedBriefAgentInput(tenant, actor, "Synthetic source",
            "  Media: OOH billboards and radio\r\n", []);
        var id = Guid.NewGuid();
        var reserved = await store.ReserveAsync(input, id, null, default);
        Assert.Null(reserved.Retained);
        var retainedInput = input with { Interpretation = reserved.Reference };
        var output = SuppliedBriefAgentFixture.Create(retainedInput);
        output = output with { Interpretation = reserved.Reference };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.CompleteAsync(
            retainedInput with { ActorId = Guid.NewGuid() }, output, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.FailAsync(
            retainedInput with { ActorId = Guid.NewGuid() }, default));
        await store.CompleteAsync(retainedInput, output, default);
        var replay = await store.ReserveAsync(input, id, null, default);
        Assert.Equal(reserved.Reference, replay.Retained!.Interpretation);
        Assert.Equal(output.Draft.Objective, replay.Retained.Draft.Objective);
        Assert.Equal(1, await db.Database.SqlQuery<int>($"""
            SELECT count(*)::integer AS "Value" FROM commercial.ai_usage_ledger WHERE run_id = {id}
            """).SingleAsync());
        await Assert.ThrowsAsync<VersionConflictException>(() => store.ReserveAsync(
            input with { SourceContent = "changed" }, id, null, default));
        var correction = await store.ReserveAsync(input with {
            Clarifications = [new("objective", "Clarified objective")] }, Guid.NewGuid(), id, default);
        Assert.Equal(2, correction.Reference.Version);
        Assert.Equal(id, correction.Reference.ParentId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.ReserveAsync(
            input with { ActorId = Guid.NewGuid() }, Guid.NewGuid(), id, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.ReserveAsync(
            input with { TenantId = Guid.NewGuid() }, Guid.NewGuid(), id, default));
    }

    [Fact]
    public async Task RejectedInterpretationRetainsReceiptWithoutBecomingReplayable()
    {
        await using var db = new GovernanceDbContext(new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(JourneyConnection()).Options);
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        await SeedAsync(db, tenant, actor);
        var store = new SuppliedBriefInterpretationStore(new BriefRecordStore(db), TimeProvider.System);
        var input = new SuppliedBriefAgentInput(tenant, actor, "Rejected source", "Unclear campaign", []);
        var reservation = await store.ReserveAsync(input, Guid.NewGuid(), null, default);
        input = input with { Interpretation = reservation.Reference };
        var output = SuppliedBriefAgentFixture.Create(input);
        var failure = new SuppliedBriefValidationException(output.Usage, "{\"ungrounded\":true}",
            new InvalidOperationException("Synthetic grounding failure"));
        await store.RejectAsync(input, failure, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.ReserveAsync(input, reservation.Reference.Id, null, default));
        await Assert.ThrowsAsync<VersionConflictException>(() => store.RejectAsync(input, failure, default));
        Assert.Equal(1, await db.Database.SqlQuery<int>($"""
            SELECT count(*)::integer AS "Value" FROM commercial.ai_usage_ledger
            WHERE run_id = {reservation.Reference.Id}
            """).SingleAsync());
        Assert.Equal(MasterDataCodes.LifecycleStatuses.Failed, await db.Database.SqlQuery<string>($"""
            SELECT status_code AS "Value" FROM commercial.agent_run_steps
            WHERE run_id = {reservation.Reference.Id} AND output_json IS NOT NULL
            """).SingleAsync());
    }

    private static Task<int> SeedAsync(GovernanceDbContext db, Guid tenant, Guid actor) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.tenants (id,type_code,legal_name,trading_name,slug,status_code,
                timezone,currency_code,vat_status_code,settings_json,version,created_at_utc,updated_at_utc)
            VALUES ({tenant},{MasterDataCodes.TenantTypes.Agency},'Synthetic persistence','Synthetic persistence',
                {tenant.ToString()},{MasterDataCodes.LifecycleStatuses.Active},'Africa/Johannesburg',
                {MasterDataCodes.Currencies.Zar},{MasterDataCodes.VatStatuses.Registered},{"{}"}::jsonb,1,now(),now());
            INSERT INTO commercial.users (id,email,display_name,status_code,mfa_enabled,version,created_at_utc,updated_at_utc)
            VALUES ({actor},{actor.ToString() + "@example.test"},'Synthetic owner',
                {MasterDataCodes.LifecycleStatuses.Active},true,1,now(),now());
            """);
}
