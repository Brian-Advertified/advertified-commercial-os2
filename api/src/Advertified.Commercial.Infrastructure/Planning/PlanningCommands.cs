using System.Text.Json;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningCommands(
    PlanningRecordStore store,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider,
    PlanningPolicy planningPolicy,
    IPlanningAgentClient planningAgent) : IPlanningCommands
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);
    private static readonly string[] UnconfirmedSupplyUncertainty =
        ["Supplier availability is not confirmed for the full flight."];

    public Task<CommandResult<AudienceDefinitionSetView>> GenerateAudiencesAsync(
        Guid briefVersionId,
        CommandEnvelope<GenerateAudiencesCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.PlanGenerate,
            token => GenerateAudiencesOutcomeAsync(briefVersionId, envelope, token),
            CommandOutcomeFactory.ToResult<AudienceDefinitionSetView>, cancellationToken);

    public Task<CommandResult<MediaMixVersionView>> GenerateMediaMixAsync(
        Guid briefVersionId,
        CommandEnvelope<GenerateMediaMixCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.PlanGenerate,
            token => GenerateMediaMixOutcomeAsync(briefVersionId, envelope, token),
            CommandOutcomeFactory.ToResult<MediaMixVersionView>, cancellationToken);

    public Task<CommandResult<MediaMixVersionView>> UpdateMediaMixAsync(
        Guid mixVersionId,
        CommandEnvelope<UpdateMediaMixCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.PlanEdit,
            token => UpdateMediaMixOutcomeAsync(mixVersionId, envelope, token),
            CommandOutcomeFactory.ToResult<MediaMixVersionView>, cancellationToken);

    public Task<CommandResult<MediaMixVersionView>> ApproveMediaMixAsync(
        Guid mixVersionId,
        CommandEnvelope<ApproveMediaMixCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.PlanApprove,
            token => ApproveMediaMixOutcomeAsync(mixVersionId, envelope, token),
            CommandOutcomeFactory.ToResult<MediaMixVersionView>, cancellationToken);

    public Task<CommandResult<InventoryShortlistVersionView>> GenerateShortlistAsync(
        Guid briefVersionId,
        CommandEnvelope<GenerateShortlistCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.PlanGenerate,
            token => GenerateShortlistOutcomeAsync(briefVersionId, envelope, token),
            CommandOutcomeFactory.ToResult<InventoryShortlistVersionView>, cancellationToken);

    public Task<CommandResult<InventoryShortlistVersionView>> SelectShortlistAsync(
        Guid shortlistVersionId,
        CommandEnvelope<SelectShortlistCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.PlanEdit,
            token => SelectShortlistOutcomeAsync(shortlistVersionId, envelope, token),
            CommandOutcomeFactory.ToResult<InventoryShortlistVersionView>, cancellationToken);

    public Task<CommandResult<MediaPlanVersionView>> GenerateMediaPlanAsync(
        Guid briefVersionId,
        CommandEnvelope<GenerateMediaPlanCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.PlanGenerate,
            token => GenerateMediaPlanOutcomeAsync(briefVersionId, envelope, token),
            CommandOutcomeFactory.ToResult<MediaPlanVersionView>, cancellationToken);

    public Task<CommandResult<MediaPlanVersionView>> ResolvePlanObjectionAsync(
        Guid planVersionId,
        string objectionCode,
        CommandEnvelope<ResolvePlanObjectionCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.PlanEdit,
            token => ResolvePlanObjectionOutcomeAsync(
                planVersionId, objectionCode, envelope, token),
            CommandOutcomeFactory.ToResult<MediaPlanVersionView>, cancellationToken);

    public Task<CommandResult<MediaPlanVersionView>> ApproveMediaPlanAsync(
        Guid planVersionId,
        CommandEnvelope<ApproveMediaPlanCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.PlanApprove,
            token => ApproveMediaPlanOutcomeAsync(planVersionId, envelope, token),
            CommandOutcomeFactory.ToResult<MediaPlanVersionView>, cancellationToken);

    private async Task<CommandResult<TView>> DispatchAsync<TCommand, TView>(
        CommandEnvelope<TCommand> envelope,
        Advertified.Commercial.Domain.Governance.PermissionCode permission,
        Func<CancellationToken, Task<CommandOutcome>> execute,
        Func<CommandReceipt, CommandResult<TView>> map,
        CancellationToken cancellationToken)
        where TCommand : notnull
        where TView : notnull
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, permission, execute, cancellationToken);
        return map(receipt);
    }

    private static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, StoredJson)
        ?? throw new InvalidOperationException("Stored planning JSON is invalid.");

    private static string Write<T>(T value) => JsonSerializer.Serialize(value, StoredJson);
}
