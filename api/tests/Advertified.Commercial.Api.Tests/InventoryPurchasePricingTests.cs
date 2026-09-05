using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.CommercialSettings;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryPurchasePricingTests
{
    private static readonly PlanningPolicy Policy = PlanningPolicy.Load();
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);
    private static readonly MediaRunningPeriodView[] Periods = [new(new(2026, 9, 1), new(2026, 9, 30))];

    [Fact]
    public async Task PurchaseContractAndMigrationAreExportedFromCompiledSource()
    {
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseDeterministicInventoryProtection());
        using var client = factory.CreateClient();
        var json = await client.GetStringAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(json);
        var schema = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(schema.GetProperty("MediaAllocationInput").GetProperty("properties").TryGetProperty("purchases", out _));
        Assert.True(schema.GetProperty("BookingView").GetProperty("properties").TryGetProperty("purchase", out _));
        var directory = Path.Combine(Path.GetTempPath(), "advertified-contracts");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "advertified-commercial-api.v1.json"), json);
        var migration = new Advertified.Commercial.Infrastructure.Migrations.PurchaseQuantitySnapshots();
        await File.WriteAllTextAsync(Path.Combine(directory, "purchase-pricing-migration.sql"),
            string.Join(Environment.NewLine, migration.UpOperations
                .OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Select(item => item.Sql)));
    }

    [Theory]
    [InlineData(MasterDataCodes.RateTypes.Cpm, 100000, 1000000L)]
    [InlineData(MasterDataCodes.RateTypes.Cpc, 120, 1200000L)]
    [InlineData(MasterDataCodes.RateTypes.Cpl, 15, 150000L)]
    [InlineData(MasterDataCodes.RateTypes.Cpa, 10, 100000L)]
    [InlineData(MasterDataCodes.RateTypes.SpotRate, 40, 400000L)]
    [InlineData(MasterDataCodes.RateTypes.PackageRate, 3, 30000L)]
    public void ExactBuyingBasisPricesWithoutCalendarSubstitution(string basis, int count, long expected)
    {
        var inventory = Inventory(basis);
        var purchase = Purchase(inventory, count);
        var actual = SupplierRateCalculator.Calculate(inventory, Periods, Policy, purchase);
        Assert.Equal(expected, actual.PayableMinor);
        Assert.Equal(count, actual.Quantity);
        Assert.Throws<UnpriceableRateException>(() => SupplierRateCalculator.Calculate(inventory, Periods, Policy));
        Assert.Throws<UnpriceableRateException>(() => SupplierRateCalculator.Calculate(inventory, Periods, Policy,
            purchase with { RateId = Guid.NewGuid() }));
    }

    [Fact]
    public void MinimumQuantityChargesAndVatReconcileThroughPlanOwner()
    {
        var inventory = Inventory(MasterDataCodes.RateTypes.Cpm, minimum: 200000, production: 20000, installation: 10000)
            with { VatTreatment = MasterDataCodes.VatTreatments.Exclusive };
        var scheduled = new ScheduledInventory(inventory, Periods, Purchase(inventory, 100000));
        var client = new CommercialPolicyRow(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            0, 500, 0, MasterDataCodes.VatStatuses.Registered, 1500, false,
            MasterDataCodes.Currencies.Zar, long.MaxValue, true, Guid.NewGuid(), DateTimeOffset.UtcNow, 1);
        var result = PlanAmounts.Calculate([scheduled], client, Policy);
        var line = Assert.Single(result.Lines);
        // 2,000,000 media + 30,000 charges; supplier VAT 304,500; client fee 116,725; client VAT 367,684.
        Assert.Equal(200000, line.Quantity);
        Assert.Equal(2334500, line.SupplierCostMinor);
        Assert.Equal(116725, result.FeesMinor);
        Assert.Equal(367684, result.VatMinor);
        Assert.Equal(2818909, result.TotalMinor);
    }

    [Fact]
    public void MonthRequiresSuppliedBillingDaysAndPackagesRequireContents()
    {
        var month = Inventory(MasterDataCodes.RateTypes.MonthRate);
        Assert.Throws<UnpriceableRateException>(() => SupplierRateCalculator.Calculate(month, Periods, Policy));
        month = Inventory(MasterDataCodes.RateTypes.MonthRate, billingDays: 28);
        Assert.Equal(2, SupplierRateCalculator.Calculate(month, Periods, Policy).Quantity);
        var package = Inventory(MasterDataCodes.RateTypes.PackageRate) with { CommercialTermsJson = null };
        Assert.Throws<UnpriceableRateException>(() => SupplierRateCalculator.Calculate(package, Periods, Policy, Purchase(package, 2)));
    }

    [Fact]
    public void InvalidCountDenominatorPeriodAndOverflowCannotProduceMoney()
    {
        var inventory = Inventory(MasterDataCodes.RateTypes.Cpm);
        var purchase = Purchase(inventory, 1);
        foreach (var invalid in new[] { purchase with { Quantity = 0 }, purchase with { Quantity = -1 },
                     purchase with { Denominator = 0 }, purchase with { Denominator = 1 } })
            Assert.Throws<UnpriceableRateException>(() => SupplierRateCalculator.Calculate(inventory, Periods, Policy, invalid));
        Assert.Throws<UnpriceableRateException>(() => SupplierRateCalculator.Calculate(inventory, [], Policy, purchase));
        Assert.Throws<UnpriceableRateException>(() => SupplierRateCalculator.Calculate(
            inventory with { EffectiveTo = new(2026, 9, 15) }, Periods, Policy, purchase));
        Assert.Throws<OverflowException>(() => SupplierRateCalculator.Calculate(
            inventory with { RateAmountMinor = long.MaxValue }, Periods, Policy, Purchase(inventory, int.MaxValue)));
    }

    private static InventoryPurchaseQuantity Purchase(PlanningInventoryRow item, int count) =>
        new(item.InventoryTenantId, item.ProductId, item.ProductVersionId, item.RateId!.Value, item.RateType!, count);

    private static PlanningInventoryRow Inventory(string basis, int? minimum = null, long? production = null,
        long? installation = null, int? billingDays = null)
    {
        var terms = new InventoryCommercialTermsValues(null, null, null, production, installation, minimum,
            null, ["Supplier-defined deliverable"], [], [], null, null, null, null, billingDays);
        return new(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Synthetic placement",
            MasterDataCodes.Channels.Digital, MasterDataCodes.InventoryProductTypes.DigitalPlacement,
            "Gauteng", null, null, Guid.NewGuid(), basis, MasterDataCodes.Currencies.Zar, 10000,
            new(2026, 1, 1), new(2026, 12, 31), "synthetic:rate", null,
            MasterDataCodes.AvailabilityStatuses.Available, DateTimeOffset.UtcNow, null, "synthetic:availability",
            "[]", null, MasterDataCodes.VatStatuses.Registered, null, MasterDataCodes.VatTreatments.Inclusive,
            JsonSerializer.Serialize(terms, StoredJson), null, null, null);
    }
}
