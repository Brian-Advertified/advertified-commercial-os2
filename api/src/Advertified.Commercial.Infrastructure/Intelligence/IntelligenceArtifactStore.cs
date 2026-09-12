using System.Text.Json;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Intelligence;

internal static partial class IntelligenceArtifactStore
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    private static void ValidateDraft(IntelligenceArtifactDraft draft)
    {
        if (draft.SubjectId == Guid.Empty || draft.SubjectVersion <= 0 ||
            string.IsNullOrWhiteSpace(draft.SubjectType) ||
            string.IsNullOrWhiteSpace(draft.ServiceCode) ||
            string.IsNullOrWhiteSpace(draft.ArtifactSchemaVersion) ||
            string.IsNullOrWhiteSpace(draft.ArtifactJson) ||
            string.IsNullOrWhiteSpace(draft.InputHash) || draft.InputHash.Length != 64 ||
            draft.Invocations.Count == 0)
            throw new ArgumentException("The intelligence artifact draft is incomplete.");

        ValidateArtifactJson(draft.ArtifactJson);
        foreach (var invocation in draft.Invocations)
            ValidateInvocation(invocation);

        if (draft.Dependencies.Any(item => item.ResourceId == Guid.Empty ||
            item.ResourceVersion <= 0 || string.IsNullOrWhiteSpace(item.ResourceType) ||
            string.IsNullOrWhiteSpace(item.PurposeCode)))
            throw new ArgumentException("The intelligence artifact dependency is invalid.");

        if (draft.Evidence.Any(item => string.IsNullOrWhiteSpace(item.FieldPath) ||
            string.IsNullOrWhiteSpace(item.ClassificationCode) ||
            item.EvidenceItemId.HasValue == item.ReferenceObservationId.HasValue))
            throw new ArgumentException("The intelligence artifact evidence binding is invalid.");
    }

    private static void ValidateInvocation(IntelligenceInvocationUsage invocation)
    {
        if (string.IsNullOrWhiteSpace(invocation.OperationCode) ||
            string.IsNullOrWhiteSpace(invocation.Provider) ||
            string.IsNullOrWhiteSpace(invocation.Model) ||
            string.IsNullOrWhiteSpace(invocation.CacheStatus) ||
            invocation.IncrementalCostMinor < 0 || invocation.InputTokens < 0 ||
            invocation.OutputTokens < 0 || invocation.IncrementalCostUsdMicros < 0)
            throw new ArgumentException("The intelligence invocation usage is invalid.");

        if (invocation.Provider == AgentProviderMetadata.DeterministicProvider)
        {
            if (invocation.Model != AgentProviderMetadata.FixtureModel ||
                invocation.IncrementalCostMinor != 0 ||
                invocation.ProviderRequestId is not null ||
                invocation.InputTokens != 0 || invocation.OutputTokens != 0 ||
                invocation.IncrementalCostUsdMicros != 0 ||
                invocation.CacheStatus != AgentProviderMetadata.FixtureCacheStatus)
                throw new ArgumentException("Deterministic intelligence usage must be zero-cost fixture usage.");
            return;
        }

        if (string.IsNullOrWhiteSpace(invocation.ProviderRequestId) ||
            invocation.OutputTokens <= 0 || invocation.IncrementalCostUsdMicros <= 0 ||
            invocation.CacheStatus is not (AgentProviderMetadata.LiveCacheStatus or
                AgentProviderMetadata.CacheHitStatus))
            throw new ArgumentException("Live intelligence usage is incomplete.");
    }

    private static void ValidateArtifactJson(string artifactJson)
    {
        using var document = JsonDocument.Parse(artifactJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("The intelligence artifact payload must be a JSON object.");
    }

    private static void EnsureTransaction(GovernanceDbContext dbContext)
    {
        if (dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Intelligence artifacts require the active governed transaction.");
    }

    private sealed class IntelligenceArtifactRow
    {
        public Guid Id { get; set; }
        public string SubjectType { get; set; } = string.Empty;
        public Guid SubjectId { get; set; }
        public long SubjectVersion { get; set; }
        public string ServiceCode { get; set; } = string.Empty;
        public string ArtifactSchemaVersion { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public string ArtifactJson { get; set; } = string.Empty;
        public string UnknownsJson { get; set; } = "[]";
        public string AssumptionsJson { get; set; } = "[]";
        public string InputHash { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Guid? SupersedesArtifactId { get; set; }
        public Guid CreatedBy { get; set; }
        public Guid? ApprovedBy { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset? ApprovedAtUtc { get; set; }
        public long Version { get; set; }
    }

    private sealed class IntelligenceInvocationRow
    {
        public int SequenceNo { get; set; }
        public string OperationCode { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public long IncrementalCostMinor { get; set; }
        public string CacheStatus { get; set; } = string.Empty;
        public string? ProviderRequestId { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public long IncrementalCostUsdMicros { get; set; }
    }

    private sealed class IntelligenceEvidenceRow
    {
        public string FieldPath { get; set; } = string.Empty;
        public string ClassificationCode { get; set; } = string.Empty;
        public Guid? EvidenceItemId { get; set; }
        public Guid? ReferenceObservationId { get; set; }
        public string? Rationale { get; set; }
    }
}
