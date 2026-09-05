using System.Text.Json;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Brief;

public interface ISuppliedBriefInterpretationStore
{
    Task<SuppliedBriefReservation> ReserveAsync(SuppliedBriefAgentInput input, Guid id,
        Guid? parentId, CancellationToken cancellationToken);
    Task CompleteAsync(SuppliedBriefAgentInput input, SuppliedBriefUnderstandingView result,
        CancellationToken cancellationToken);
    Task FailAsync(SuppliedBriefAgentInput input, CancellationToken cancellationToken);
    Task RejectAsync(SuppliedBriefAgentInput input, SuppliedBriefValidationException failure,
        CancellationToken cancellationToken);
}

public sealed record SuppliedBriefReservation(
    SuppliedBriefInterpretationReference Reference, SuppliedBriefUnderstandingView? Retained);

public sealed partial class SuppliedBriefInterpretationStore(
    BriefRecordStore store, TimeProvider timeProvider) : ISuppliedBriefInterpretationStore
{
    private const string StepCode = "SUPPLIED_BRIEF_UNDERSTANDING";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SuppliedBriefReservation> ReserveAsync(SuppliedBriefAgentInput input, Guid id,
        Guid? parentId, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || parentId == Guid.Empty || parentId == id)
            throw new ArgumentException("Distinct nonempty interpretation references are required.");
        await using var transaction = await store.BeginSessionAsync(new ActorId(input.ActorId),
            new TenantId(input.TenantId), cancellationToken);
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 0))", cancellationToken);
        var hash = OpportunityCommandSupport.Hash(JsonSerializer.Serialize(new {
            input.SourceTitle, input.SourceContent, input.Clarifications, ParentId = parentId }, JsonOptions));
        var existing = await FindAsync(input, id, cancellationToken);
        if (existing is not null) return Replay(existing, hash);
        var parent = parentId.HasValue ? await FindAsync(input, parentId.Value, cancellationToken) : null;
        if (parentId.HasValue && (parent is null || parent.SourceHash != OpportunityCommandSupport.Hash(input.SourceContent)))
            throw new UnauthorizedAccessException("The original supplied Brief must match its retained parent.");
        var reference = new SuppliedBriefInterpretationReference(id, parentId,
            checked((parent?.Version ?? 0) + 1), OpportunityCommandSupport.Hash(input.SourceContent));
        await InsertAsync(input, reference, hash, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(reference, null);
    }

    private static SuppliedBriefReservation Replay(InterpretationRow row, string inputHash)
    {
        if (row.InputHash != inputHash) throw new VersionConflictException();
        if (row.OutputJson is null)
            throw new InvalidOperationException("The retained interpretation has no validated result. Reconcile its run before retrying.");
        var result = JsonSerializer.Deserialize<SuppliedBriefUnderstandingView>(row.OutputJson, JsonOptions)
            ?? throw new InvalidOperationException("The retained interpretation is invalid.");
        return new(new(row.Id, row.ParentId, row.Version, row.SourceHash), result);
    }

    private Task<InterpretationRow?> FindAsync(SuppliedBriefAgentInput input, Guid id,
        CancellationToken cancellationToken) => store.DbContext.Database.SqlQuery<InterpretationRow>($"""
        SELECT source.id AS "Id", source.parent_id AS "ParentId", source.version_no AS "Version",
            source.source_hash AS "SourceHash", source.input_hash AS "InputHash",
            CASE WHEN step.status_code = {MasterDataCodes.LifecycleStatuses.Completed}
                THEN step.output_json::text END AS "OutputJson"
        FROM commercial.supplied_brief_interpretations source
        LEFT JOIN commercial.agent_run_steps step ON step.tenant_id = source.tenant_id AND step.run_id = source.id
        WHERE source.tenant_id = {input.TenantId} AND source.actor_id = {input.ActorId} AND source.id = {id}
        """).SingleOrDefaultAsync(cancellationToken);

    private Task<int> InsertAsync(SuppliedBriefAgentInput input, SuppliedBriefInterpretationReference reference,
        string inputHash, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var clarifications = JsonSerializer.Serialize(input.Clarifications, JsonOptions);
        return store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.supplied_brief_interpretations
                (id, tenant_id, actor_id, parent_id, version_no, source_title, source_content,
                 source_hash, input_hash, clarifications_json, created_at_utc)
            VALUES ({reference.Id}, {input.TenantId}, {input.ActorId}, {reference.ParentId}, {reference.Version},
                {input.SourceTitle}, {input.SourceContent}, {reference.SourceHash}, {inputHash}, {clarifications}::jsonb, {now});
            INSERT INTO commercial.agent_runs
                (id, tenant_id, supplied_brief_interpretation_id, run_kind_code, status_code, input_version,
                 requested_by, correlation_id, current_step_code, attempts, version, created_at_utc, updated_at_utc)
            VALUES ({reference.Id}, {input.TenantId}, {reference.Id}, {MasterDataCodes.AgentTypes.BriefDrafting},
                {MasterDataCodes.LifecycleStatuses.Running}, {reference.Version}, {input.ActorId}, {reference.Id},
                {StepCode}, 1, 1, {now}, {now});
            INSERT INTO commercial.agent_run_steps
                (id, tenant_id, run_id, step_code, agent_code, status_code, input_hash,
                 attempt_count, created_at_utc, updated_at_utc)
            VALUES ({reference.Id}, {input.TenantId}, {reference.Id}, {StepCode}, {MasterDataCodes.AgentTypes.BriefDrafting},
                {MasterDataCodes.LifecycleStatuses.Running}, {inputHash}, 1, {now}, {now});
            """, cancellationToken);
    }

    private sealed record InterpretationRow(Guid Id, Guid? ParentId, int Version,
        string SourceHash, string InputHash, string? OutputJson);
}
