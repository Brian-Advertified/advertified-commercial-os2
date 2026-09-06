using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryInterpretationRevision
{
    internal static string Revision(
        InventoryExtractionResult extraction) =>
        extraction.CanonicalOutputHash;

    internal static InventoryExtractionResult Correct(
        InventoryExtractionResult retained,
        ReviewInventoryCandidateCommand command,
        Guid actorId,
        DateTimeOffset now,
        InventoryCodeSets codes)
    {
        _ = retained;
        _ = command;
        _ = actorId;
        _ = now;
        _ = codes;
        throw new ArgumentException(
            "Legacy C# document-schema correction is unavailable. " +
            "Correct individual candidate facts or rerun the versioned " +
            "Python projector.");
    }
}
