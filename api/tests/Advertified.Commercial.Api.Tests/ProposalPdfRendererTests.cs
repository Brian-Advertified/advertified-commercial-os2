using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Infrastructure.Proposal;
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
        var text = Encoding.ASCII.GetString(document.Content);
        var pageCountMatch = Regex.Match(text, @"/Type /Pages /Kids \[[^\]]+\] /Count (\d+)");

        Assert.True(pageCountMatch.Success);
        var pageCount = int.Parse(pageCountMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.True(pageCount > 1);
        Assert.Equal(pageCount, Regex.Count(text, "PROPOSAL FOR Client One"));
        Assert.Contains("Unbranded proposal authorised", text);
        Assert.Equal(pageCount, Regex.Count(text, "Confidential proposal"));
    }

    [Fact]
    public void RenderAcceptsCommercialTermsWithNoConditions()
    {
        var terms = new InventoryCommercialTermsValues(
            null, null, null, null, null, null, null, [], [], null!, null,
            null, null, null);
        var inventory = new ProposalInventoryLineView(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            null, "Sandton digital screen", "DOOH", "Sandton", [], 1,
            7_829_200, 0, 0, "AVAILABLE", "CURRENT", "CONFIRMED", "SUPPLIER",
            null, [], CommercialTerms: terms);
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
        var text = Encoding.ASCII.GetString(document.Content);

        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains("Channels: Digital screens", text);
        Assert.DoesNotContain("Channels: DOOH", text);
        Assert.DoesNotContain("Commercial conditions:", text);
    }

    private static ProposalBrandingView Branding(
        string agency,
        string client,
        string status) => new(
            status, agency, client, "#5C2EF2", null, null, null,
            status == ProposalBrandingStatuses.UnbrandedApproved ? Guid.NewGuid() : null,
            status == ProposalBrandingStatuses.UnbrandedApproved ? DateTimeOffset.UtcNow : null,
            status == ProposalBrandingStatuses.UnbrandedApproved ? "Client assets unavailable." : null);
}
