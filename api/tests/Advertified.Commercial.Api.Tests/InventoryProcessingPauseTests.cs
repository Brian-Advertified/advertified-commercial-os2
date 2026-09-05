using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryProcessingPauseTests
{
    [Fact]
    public async Task DefaultPauseRejectsEveryProcessingCommandBeforeAnyDependencyIsUsed()
    {
        var commands = new InventoryCommands(null!, null!, null!,
            Options.Create(new InventoryProtectionOptions()), null!, null!, null!,
            Options.Create(new InventoryEmbeddingOptions()), null!, null!, null!,
            Options.Create(new InventoryProcessingOptions()));
        Func<Task>[] requests = [
            () => commands.CreateAsync(null!, default),
            () => commands.ExecuteAsync(Guid.NewGuid(), null!, default),
            () => commands.RetryExtractionAsync(Guid.NewGuid(), null!, default),
            () => commands.ReprojectExtractionAsync(Guid.NewGuid(), null!, default),
            () => commands.PublishAsync(Guid.NewGuid(), null!, default),
            () => commands.SubmitEmbeddingAsync(Guid.NewGuid(), null!, default),
        ];
        foreach (var request in requests)
            await Assert.ThrowsAsync<InventoryProcessingPausedException>(request);
    }

    [Fact]
    public async Task PausedProcessorDoesNotReadSourceClaimOrCallProvider()
    {
        var processor = new InventoryExtractionAttemptProcessor(null!, null!, null!, null!,
            Options.Create(new InventoryProcessingOptions()), null!, null!);
        await processor.ProcessAsync(null!, default);
    }
}
