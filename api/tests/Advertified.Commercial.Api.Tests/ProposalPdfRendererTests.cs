using System.Text;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Infrastructure.Proposal;
using UglyToad.PdfPig;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class ProposalPdfRendererTests
{
    [Theory]
    [InlineData("Recommended OOH plan", "Recommended Outdoor advertising plan")]
    [InlineData("Your Advertified DOOH proposal", "Your Advertified Digital screens proposal")]
    [InlineData("OOH_SITE", "OOH_SITE")]
    public void ClientTemplatesExplainChannelAcronyms(string template, string expected) =>
        Assert.Equal(expected, ProposalMediaLabels.ClientText(template));

    [Fact]
    public void RenderPaginatesLongContentAndBrandsEveryPage()
    {
        var proposal = new ProposalVersionView(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "Long commercial proposal",
            string.Join(' ', Enumerable.Repeat(
                "Approved evidence guides this recommendation.", 180)),
            string.Join(' ', Enumerable.Repeat(
                "Rates and availability remain subject to confirmation.", 180)),
            new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
            "APPROVED",
            [],
            null,
            null,
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SELF",
            null,
            null,
            null,
            null,
            null,
            null,
            "CURRENT",
            [],
            Branding("Agency One", "Client One", "UNBRANDED_AUTHORISED"),
            1,
            DateTimeOffset.UtcNow);

        var document = ProposalPdfRenderer.Render(proposal);
        var rendered = ReadPdf(document.Content);

        Assert.True(rendered.Pages.Count > 1);
        Assert.All(rendered.Pages, page => AssertContainsNormalized("PROPOSAL FOR Client One", page));
        AssertContainsNormalized("Unbranded proposal authorised", rendered.Text);
        Assert.All(rendered.Pages, page => AssertContainsNormalized("Confidential proposal", page));
    }

    [Fact]
    public void RenderAcceptsCommercialTermsWithNoConditions()
    {
        var terms = new InventoryCommercialTermsValues(
            null, null, null, null, null, null, null, [], [], null!, null,
            null, null, null);
        var inventory = new ProposalInventoryLineView(
            InventoryTenantId: Guid.NewGuid(),
            SupplierName: null,
            MarketplaceListingVersionId: Guid.NewGuid(),
            InventoryProductId: Guid.NewGuid(),
            ProductVersionId: Guid.NewGuid(),
            RateId: Guid.NewGuid(),
            AvailabilityId: null,
            Name: "Sandton digital screen",
            Channel: "DOOH",
            Geography: "Sandton",
            RunningPeriods: [],
            Quantity: 1,
            ClientPriceMinor: 7_829_200,
            FeesMinor: 0,
            VatMinor: 0,
            Availability: "AVAILABLE",
            RateFreshness: "CURRENT",
            SupplyConfidence: "CONFIRMED",
            SupplySource: "SUPPLIER",
            LastConfirmedAtUtc: null,
            Uncertainties: [],
            CommercialTerms: terms);
        var option = new ProposalOptionView(
            Guid.NewGuid(), "Integrated route 1", "Build premium awareness",
            Guid.NewGuid(), 1, 7_829_200, "ZAR", 1, ["DOOH"], [],
            ["Sandton digital screen"], [inventory]);
        var proposal = new ProposalVersionView(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            "Commercial proposal", "Approved objective", "Approved terms",
            new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
            "APPROVED", [option], null, null, null, Guid.NewGuid(), null,
            "SELF", null, null, null, null, null, null, "CURRENT", [],
            Branding("Agency One", "Client One", "READY"), 1,
            DateTimeOffset.UtcNow);

        var document = ProposalPdfRenderer.Render(proposal);
        var rendered = ReadPdf(document.Content);

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(document.Content, 0, 5));
        AssertContainsNormalized("Digital screens", rendered.Text);
        Assert.DoesNotContain("DOOH", rendered.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Commercialcondition:", Normalize(rendered.Text), StringComparison.OrdinalIgnoreCase);
        AssertContainsNormalized("OOH site schedule", rendered.Text);
    }

    private static (IReadOnlyList<string> Pages, string Text) ReadPdf(byte[] content)
    {
        using var pdf = PdfDocument.Open(content);
        var pages = pdf.GetPages().Select(page => page.Text).ToArray();
        return (pages, string.Join("\n", pages));
    }

    private static void AssertContainsNormalized(string expected, string actual) =>
        Assert.Contains(Normalize(expected), Normalize(actual), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value) =>
        new(value.Where(character => !char.IsWhiteSpace(character) && character != '·').ToArray());

    private static ProposalBrandingView Branding(
        string agency,
        string client,
        string status) => new(
            status, agency, client, "#5C2EF2", null, null, null,
            status == ProposalBrandingStatuses.UnbrandedApproved ? Guid.NewGuid() : null,
            status == ProposalBrandingStatuses.UnbrandedApproved ? DateTimeOffset.UtcNow : null,
            status == ProposalBrandingStatuses.UnbrandedApproved ? "Client assets unavailable." : null);
}
