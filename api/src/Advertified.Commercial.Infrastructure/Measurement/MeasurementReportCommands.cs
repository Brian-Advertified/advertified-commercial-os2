using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Delivery;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Measurement;

public sealed class MeasurementReportCommands(
    MeasurementReportRecordStore store,
    PerformanceEvidenceRecordStore evidenceStore,
    DeliveryProofRecordStore proofStore,
    IMeasurementAgentClient agentClient,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider) : IMeasurementReportCommands
{
    public async Task<CommandResult<MeasurementReportView>> GenerateAsync(
        Guid campaignId,
        CommandEnvelope<GenerateMeasurementReportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.MeasurementReportGenerate,
            token => GenerateOutcomeAsync(campaignId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<MeasurementReportView>(receipt);
    }

    public async Task<CommandResult<MeasurementReportView>> ReviewAsync(
        Guid campaignId,
        Guid reportId,
        CommandEnvelope<ReviewMeasurementReportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.MeasurementReportReview,
            token => ReviewOutcomeAsync(campaignId, reportId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<MeasurementReportView>(receipt);
    }

    private async Task<CommandOutcome> GenerateOutcomeAsync(
        Guid campaignId,
        CommandEnvelope<GenerateMeasurementReportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var source = await store.FindSourceAsync(
            campaignId, envelope.Command.ApproverUserId, cancellationToken)
            ?? throw new MeasurementReportBlockedException();
        var input = await BuildInputAsync(source, envelope, cancellationToken);
        var proposal = await agentClient.InterpretAsync(input, cancellationToken);
        MeasurementAgentValidation.Validate(input, proposal);
        var prepared = Prepare(input, proposal);
        var now = timeProvider.GetUtcNow();
        await store.InsertTraceAndReportAsync(
            prepared, source, envelope, now, cancellationToken);
        var view = await store.GetViewAsync(
            prepared.ReportId, evidenceStore, cancellationToken)
            ?? throw new InvalidOperationException("Measurement report was not persisted.");
        return Outcome(
            envelope, view, MasterDataReferences.CommercialActions.MeasurementReportGenerated,
            MasterDataReferences.CommercialEventTypes.MeasurementReportGenerated, now);
    }

    private async Task<CommandOutcome> ReviewOutcomeAsync(
        Guid campaignId,
        Guid reportId,
        CommandEnvelope<ReviewMeasurementReportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var report = await store.FindAsync(reportId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Measurement report access denied.");
        if (report.CampaignId != campaignId ||
            report.ApproverUserId != envelope.ActorId.Value)
            throw new UnauthorizedAccessException("Measurement report access denied.");
        if (report.Version != envelope.ExpectedVersion) throw new VersionConflictException();
        if (report.Status != MasterDataCodes.LifecycleStatuses.ReviewRequired)
            throw new MeasurementReportBlockedException();
        var reason = ReviewReason(envelope.Command.Reason);
        var decision = envelope.Command.Approved
            ? MasterDataCodes.LifecycleStatuses.Approved
            : MasterDataCodes.LifecycleStatuses.Rejected;
        var now = timeProvider.GetUtcNow();
        await store.ReviewAsync(report, envelope, decision, reason, now, cancellationToken);
        var view = await store.GetViewAsync(reportId, evidenceStore, cancellationToken)
            ?? throw new InvalidOperationException("Measurement report was not persisted.");
        return Outcome(
            envelope, view, MasterDataReferences.CommercialActions.MeasurementReportReviewed,
            MasterDataReferences.CommercialEventTypes.MeasurementReportReviewed, now);
    }

    private async Task<MeasurementAgentInput> BuildInputAsync(
        MeasurementReportSourceRow source,
        CommandEnvelope<GenerateMeasurementReportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var plan = JsonSerializer.Deserialize<string[]>(source.MeasurementPlanJson) ?? [];
        var proofs = (await proofStore.ListCampaignAsync(source.CampaignId, cancellationToken))
            .Where(item => item.Status == MasterDataCodes.LifecycleStatuses.Approved)
            .Select(item => new MeasurementProofInput(item.Id, item.Version)).ToArray();
        var evidence = (await evidenceStore.ListCampaignViewsAsync(
                source.CampaignId, cancellationToken))
            .Where(item => item.Status == MasterDataCodes.LifecycleStatuses.Approved)
            .Select(ToAgentEvidence).ToArray();
        if (plan.Length == 0 || plan.Any(string.IsNullOrWhiteSpace) ||
            proofs.Length == 0 || evidence.Length == 0)
            throw new MeasurementReportBlockedException();
        return new MeasurementAgentInput(
            source.TenantId, envelope.ActorId.Value, Guid.NewGuid(), Guid.NewGuid(),
            envelope.CorrelationId.Value, source.CampaignId, source.CampaignVersion,
            plan, proofs, evidence);
    }

    private static MeasurementEvidenceInput ToAgentEvidence(PerformanceEvidenceView evidence) =>
        new(evidence.Id, evidence.Version, evidence.QualityStatus, evidence.Methodology,
            evidence.Limitations, evidence.Metrics.Select(metric =>
                new MeasurementMetricFactInput(
                    metric.Id, evidence.Id, metric.MetricType, metric.Value, metric.Unit,
                    metric.PeriodStart, metric.PeriodEnd, metric.SourceLocator)).ToArray());

    private static PreparedMeasurementReport Prepare(
        MeasurementAgentInput input,
        MeasurementAgentProposal proposal)
    {
        var interpretationJson = JsonSerializer.Serialize(
            proposal.Interpretation, MeasurementReportRecordStore.StoredJson);
        var traceOutputJson = JsonSerializer.Serialize(
            proposal, MeasurementReportRecordStore.StoredJson);
        var evidenceVersionsJson = JsonSerializer.Serialize(
            input.EvidenceSets.Select(item => new MeasurementEvidenceVersion(
                item.Id, item.Version)), MeasurementReportRecordStore.StoredJson);
        return new PreparedMeasurementReport(
            Guid.NewGuid(), input.RunId, input.StepId,
            input.EvidenceSets.SelectMany(item => item.Metrics).Select(item => item.Id).ToArray(),
            evidenceVersionsJson, interpretationJson, traceOutputJson,
            JsonSerializer.Serialize(
                proposal.Interpretation.Limitations, MeasurementReportRecordStore.StoredJson),
            Hash(JsonSerializer.Serialize(input, MeasurementReportRecordStore.StoredJson)),
            Hash(traceOutputJson), proposal);
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string ReviewReason(string reason)
    {
        var value = reason?.Trim() ?? string.Empty;
        if (value.Length is < 10 or > 1000) throw new ArgumentException("Review reason invalid.");
        return value;
    }

    private static CommandOutcome Outcome<TCommand>(
        CommandEnvelope<TCommand> envelope,
        MeasurementReportView view,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset now)
        where TCommand : notnull => CommandOutcomeFactory.Create(
            envelope, view, view.Id, view.Version,
            MasterDataReferences.CommercialResourceTypes.MeasurementReport,
            action, eventType, now);
}
