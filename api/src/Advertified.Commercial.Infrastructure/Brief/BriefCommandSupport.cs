using System.Text.Json;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Brief;

internal static class BriefCommandSupport
{
    internal static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    internal static string Json<T>(IReadOnlyList<T> values) =>
        JsonSerializer.Serialize(values, StoredJson);

    internal static string[] Strings(
        IReadOnlyList<string> values,
        string parameterName,
        int maximumItems = 100)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        if (values.Count > maximumItems)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
        return values
            .Select(value => OpportunityCommandSupport.Required(value, 1000, parameterName))
            .ToArray();
    }

    internal static async Task<ValidatedBriefVersion> ValidateAsync(
        GovernanceDbContext dbContext,
        CreateBriefVersionCommand command,
        CancellationToken cancellationToken)
    {
        var problem = OpportunityCommandSupport.Required(
            command.BusinessProblem, 4000, nameof(command.BusinessProblem));
        var objective = OpportunityCommandSupport.Required(
            command.Objective, 4000, nameof(command.Objective));
        var timing = OpportunityCommandSupport.Required(
            command.Timing, 2000, nameof(command.Timing));
        ValidateMoney(command);
        var currency = command.Currency?.Trim().ToUpperInvariant();
        var vat = command.VatStatus?.Trim().ToUpperInvariant();
        await EnsureMoneyCodesAsync(dbContext, currency, vat, cancellationToken);
        var unknowns = ValidateUnknowns(command.Unknowns);
        if (command.BudgetUnknown && !unknowns.Any(item =>
                string.Equals(item.FieldPath, "budget", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("An unknown budget must be labelled explicitly.");
        }
        var assumptions = ValidateAssumptions(command.Assumptions);
        var conflicts = await ValidateConflictsAsync(
            dbContext, command.Conflicts, cancellationToken);
        return new ValidatedBriefVersion(
            problem, objective, Strings(command.Audiences, nameof(command.Audiences)),
            Strings(command.Geographies, nameof(command.Geographies)), timing, currency, vat,
            Strings(command.Constraints, nameof(command.Constraints)),
            Strings(command.Measurement, nameof(command.Measurement)),
            Strings(command.Facts, nameof(command.Facts)), unknowns, assumptions, conflicts);
    }

    private static void ValidateMoney(CreateBriefVersionCommand command)
    {
        if (command.BudgetMinor is < 0 || command.FeesMinor is < 0 ||
            (command.BudgetUnknown &&
                (command.BudgetMinor.HasValue || !string.IsNullOrWhiteSpace(command.Currency))) ||
            (!command.BudgetUnknown &&
                (!command.BudgetMinor.HasValue || string.IsNullOrWhiteSpace(command.Currency))))
        {
            throw new ArgumentException("Budget must be typed or explicitly unknown.");
        }
    }

    private static async Task EnsureMoneyCodesAsync(
        GovernanceDbContext dbContext,
        string? currency,
        string? vat,
        CancellationToken cancellationToken)
    {
        if (currency is not null)
        {
            await OpportunityCommandSupport.EnsureCodeAsync(
                dbContext, "currencies", currency, cancellationToken);
        }
        if (vat is not null)
        {
            await OpportunityCommandSupport.EnsureCodeAsync(
                dbContext, "vatStatuses", vat, cancellationToken);
        }
    }

    private static BriefUnknownInput[] ValidateUnknowns(IReadOnlyList<BriefUnknownInput> values) =>
        values.Select(item => new BriefUnknownInput(
            OpportunityCommandSupport.Required(item.FieldPath, 200, nameof(values)),
            OpportunityCommandSupport.Required(item.Question, 1000, nameof(values)),
            item.IsBlocking)).ToArray();

    private static BriefAssumptionInput[] ValidateAssumptions(
        IReadOnlyList<BriefAssumptionInput> values) => values.Select(item =>
            new BriefAssumptionInput(
                OpportunityCommandSupport.Required(item.FieldPath, 200, nameof(values)),
                OpportunityCommandSupport.Required(item.Value, 1000, nameof(values)),
                OpportunityCommandSupport.Required(item.Impact, 1000, nameof(values)),
                OpportunityCommandSupport.Required(item.ValidationNeeded, 1000, nameof(values))))
            .ToArray();

    private static async Task<BriefConflictInput[]> ValidateConflictsAsync(
        GovernanceDbContext dbContext,
        IReadOnlyList<BriefConflictInput> values,
        CancellationToken cancellationToken)
    {
        var result = new List<BriefConflictInput>(values.Count);
        foreach (var item in values)
        {
            var severity = OpportunityCommandSupport.Required(
                item.Severity, 100, nameof(values)).ToUpperInvariant();
            await OpportunityCommandSupport.EnsureCodeAsync(
                dbContext, "criticSeverities", severity, cancellationToken);
            if (item.Resolved && string.IsNullOrWhiteSpace(item.Resolution))
            {
                throw new ArgumentException("A resolved conflict requires a resolution.");
            }
            result.Add(new BriefConflictInput(
                OpportunityCommandSupport.Required(item.FieldPath, 200, nameof(values)),
                OpportunityCommandSupport.Required(item.Description, 1000, nameof(values)),
                severity,
                item.Resolved,
                OpportunityCommandSupport.Optional(item.Resolution, 1000, nameof(values))));
        }
        return result.ToArray();
    }
}

internal sealed record ValidatedBriefVersion(
    string BusinessProblem,
    string Objective,
    string[] Audiences,
    string[] Geographies,
    string Timing,
    string? Currency,
    string? VatStatus,
    string[] Constraints,
    string[] Measurement,
    string[] Facts,
    BriefUnknownInput[] Unknowns,
    BriefAssumptionInput[] Assumptions,
    BriefConflictInput[] Conflicts);
