using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Commercial;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryInterpretationRevision
{
    private const string Projector = "advertified-interpretation-review";
    private const string ProjectorVersion = "1.0";
    internal static string Revision(InventoryExtractionResult extraction) =>
        extraction.Document.DiscoveredSchema is { } schema
            ? InventoryAcceptancePolicy.MappingRevision(schema) : extraction.CanonicalOutputHash;

    internal static InventoryExtractionResult Correct(InventoryExtractionResult retained,
        ReviewInventoryCandidateCommand command, Guid actorId, DateTimeOffset now, InventoryCodeSets codes)
    {
        var previous = retained.Document.DiscoveredSchema;
        if (command.ExpectedMappingRevision != Revision(retained))
            throw new VersionConflictException();
        if (command.CorrectedValues is not null || command.CorrectedSchema is null ||
            string.IsNullOrWhiteSpace(command.Notes) || command.Notes.Length > 2000)
            throw new ArgumentException("Correct the source mappings and supply a reason; canonical value overrides are not interpretation corrections.");
        var proposed = command.CorrectedSchema with
        {
            Provenance = previous?.Provenance ?? new InventorySchemaProvenance(
                "human-source-review", "inventory-interpretation-review/1.0", null, null, 0, null),
            Correction = new(actorId, now, command.ExpectedMappingRevision, command.Notes.Trim()),
        };
        var structure = InventoryDocumentStructureReader.Read(retained.SourceHash, retained.ProviderJson);
        var rows = InventorySchemaBatchProjection.Project(structure, proposed,
            InventoryCandidateNormalizer.CanonicalMeanings, InventorySchemaExtractionStep.GovernedCodes(codes));
        return InventoryExtractionContract.Create(Projector, ProjectorVersion + ":" + InventoryAcceptancePolicy.MappingRevision(proposed),
            retained.SchemaVersion, retained.SourceHash, retained.ProviderJson, rows, proposed);
    }
}
