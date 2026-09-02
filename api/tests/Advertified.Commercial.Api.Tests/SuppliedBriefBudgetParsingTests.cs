using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Brief;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class SuppliedBriefBudgetParsingTests
{
    [Theory]
    [InlineData("Budget: R 125,000.50", 12_500_050L, MasterDataCodes.Currencies.Zar)]
    [InlineData("Budget: 25 thousand rand", 2_500_000L, MasterDataCodes.Currencies.Zar)]
    [InlineData("Budget: USD 12,345.67", 1_234_567L, MasterDataCodes.Currencies.Usd)]
    [InlineData("Budget: usd 500.25", 50_025L, MasterDataCodes.Currencies.Usd)]
    [InlineData("Budget: $12.5k", 1_250_000L, MasterDataCodes.Currencies.Usd)]
    [InlineData("Budget: GBP 8,000", 800_000L, MasterDataCodes.Currencies.Gbp)]
    [InlineData("Budget: £1.25m", 125_000_000L, MasterDataCodes.Currencies.Gbp)]
    [InlineData("Budget: 2,500.25 EUR", 250_025L, MasterDataCodes.Currencies.Eur)]
    [InlineData("Budget: €9k", 900_000L, MasterDataCodes.Currencies.Eur)]
    public async Task UnderstandAsyncReturnsGovernedCurrencyAndIsoMinorUnits(
        string budgetLine,
        long expectedMinor,
        string expectedCurrency)
    {
        var result = await UnderstandAsync(budgetLine);

        Assert.Equal(expectedMinor, result.Draft.BudgetMinor);
        Assert.False(result.Draft.BudgetUnknown);
        Assert.Equal(expectedCurrency, result.Draft.Currency);
        Assert.DoesNotContain(result.Questions, question => question.FieldPath == "budget");
    }

    [Fact]
    public async Task UnderstandAsyncUsesGovernedDefaultForUnmarkedBudgetField()
    {
        var result = await UnderstandAsync("Budget: 10,000");

        Assert.Equal(1_000_000L, result.Draft.BudgetMinor);
        Assert.False(result.Draft.BudgetUnknown);
        Assert.Equal(MasterDataCodes.Currencies.Zar, result.Draft.Currency);
    }

    [Fact]
    public async Task UnderstandAsyncUsesGovernedDefaultForBareBudgetClarification()
    {
        var result = await UnderstandAsync(
            "Budget was not supplied.",
            [new BriefClarificationInput("budget", "7500.25")]);

        Assert.Equal(750_025L, result.Draft.BudgetMinor);
        Assert.False(result.Draft.BudgetUnknown);
        Assert.Equal(MasterDataCodes.Currencies.Zar, result.Draft.Currency);
    }

    [Theory]
    [InlineData("Budget: USD 10,000 or GBP 8,000")]
    [InlineData("Budget: JPY 10,000")]
    [InlineData("Budget was not supplied.")]
    public async Task UnderstandAsyncKeepsAmbiguousUnsupportedOrMissingBudgetUnknown(
        string budgetLine)
    {
        var result = await UnderstandAsync(budgetLine);

        Assert.Null(result.Draft.BudgetMinor);
        Assert.True(result.Draft.BudgetUnknown);
        Assert.Null(result.Draft.Currency);
        Assert.Contains(result.Questions, question =>
            question.FieldPath == "budget" && question.IsBlocking);
    }

    [Theory]
    [InlineData("JPY", 0, "Budget: JPY 1,234", 1_234L)]
    [InlineData("KWD", 3, "Budget: KWD 1.234", 1_234L)]
    public async Task ParserUsesGovernedMinorUnitDigits(
        string currency,
        int minorUnitDigits,
        string budgetLine,
        long expectedMinor)
    {
        var policy = SuppliedBriefAgentPolicy.Load() with
        {
            ActiveCurrencies =
            [
                new BriefCurrencyPolicy(currency, minorUnitDigits, [currency]),
            ],
            DefaultCurrency = currency,
        };
        var result = await UnderstandAsync(budgetLine, policy: policy);

        Assert.Equal(expectedMinor, result.Draft.BudgetMinor);
        Assert.Equal(currency, result.Draft.Currency);
    }

    [Fact]
    public async Task InclusiveVatWordingDoesNotInventARegistrationStatus()
    {
        var result = await UnderstandAsync("Budget: R 100,000 including VAT");

        Assert.Equal(10_000_000L, result.Draft.BudgetMinor);
        Assert.Null(result.Draft.VatStatus);
    }

    [Fact]
    public async Task ExplicitVatRegistrationIsRetained()
    {
        var result = await UnderstandAsync("Budget: R 100,000. Client is VAT registered.");

        Assert.Equal(MasterDataCodes.VatStatuses.Registered, result.Draft.VatStatus);
    }

    private static Task<SuppliedBriefUnderstandingView> UnderstandAsync(
        string budgetLine,
        IReadOnlyList<BriefClarificationInput>? clarifications = null,
        SuppliedBriefAgentPolicy? policy = null)
    {
        var client = new DeterministicSuppliedBriefAgentClient(
            policy ?? SuppliedBriefAgentPolicy.Load());
        var content = $"""
            Client: Market Neutral Foods
            Objective: Build qualified awareness
            Audience: Procurement leaders
            Geography: Configured market
            Timing: October 2026
            Media: radio
            {budgetLine}
            """;
        return client.UnderstandAsync(
            new SuppliedBriefAgentInput(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Supplied campaign Brief",
                content,
                clarifications ?? Array.Empty<BriefClarificationInput>()),
            CancellationToken.None);
    }
}
